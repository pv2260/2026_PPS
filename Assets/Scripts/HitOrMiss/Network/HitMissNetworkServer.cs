using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace HitOrMiss.Network
{
    /// <summary>
    /// Local-network HTTP + WebSocket bridge between the Unity headset client
    /// and a separate clinician GUI (browser-based, served from
    /// <c>StreamingAssets/clinician/</c>).
    ///
    /// Built on <see cref="MiniHttpServer"/> (custom TcpListener-based) instead
    /// of the BCL's <see cref="System.Net.HttpListener"/>, because Unity's
    /// Mono runtime stubs <c>HttpListener.AcceptWebSocketAsync</c> with a
    /// <c>NotImplementedException</c>. Side benefit: no longer requires
    /// <c>netsh urlacl</c> on Windows — TcpListener doesn't go through the
    /// kernel HTTP driver.
    ///
    /// Endpoints (all rooted at <c>http://&lt;headset-ip&gt;:7777</c>):
    ///
    ///   GET  /                          — redirects to /clinician/index.html
    ///   GET  /clinician/{file}          — static SPA assets
    ///   GET  /api/status                — current session state
    ///   GET  /api/sessions              — list of past session folders
    ///   GET  /api/sessions/{id}/{kind}  — metadata|trials|eyetracking|session|progress
    ///   POST /api/session/start         — body: <see cref="StartSessionRequest"/>
    ///   POST /api/session/pause
    ///   POST /api/session/resume
    ///   POST /api/session/stop
    ///   WS   /ws                        — server-pushed events
    ///
    /// Threading model: <see cref="MiniHttpServer"/> calls our request and
    /// WebSocket callbacks on background tasks. Anything that touches Unity
    /// API surface goes through <see cref="m_MainThreadQueue"/>, drained in
    /// <see cref="Update"/>.
    /// </summary>
    public class HitMissNetworkServer : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] int m_Port = Protocol.DefaultPort;
        [Tooltip("If true, listen on 0.0.0.0:port so other devices on the LAN can connect. TcpListener doesn't need urlacl on Windows.")]
        [SerializeField] bool m_BindToAllInterfaces = true;

        [Header("References / Task 2 (HitOrMiss)")]
        [Tooltip("Wire this when the server lives in Task2Scene. Leave empty when running Task 1.")]
        [SerializeField] HitOrMissAppController m_AppController;
        [Tooltip("Wire this when the server lives in Task2Scene. Leave empty when running Task 1.")]
        [SerializeField] TrajectoryTaskManager m_TaskManager;

        [Header("References / Task 1 (PPS)")]
        [Tooltip("Wire this when the server lives in Task1Scene. Leave empty when running Task 2.")]
        [SerializeField] HitOrMiss.Pps.PpsAppController m_PpsAppController;
        [Tooltip("Wire this when the server lives in Task1Scene. Leave empty when running Task 2.")]
        [SerializeField] HitOrMiss.Pps.PpsTaskManager m_PpsTaskManager;

        bool IsPpsMode => m_PpsAppController != null;

        MiniHttpServer m_Server;
        string m_ClinicianRoot;
        string m_LogsRoot;
        readonly ConcurrentQueue<Action> m_MainThreadQueue = new();
        readonly List<MiniWebSocket> m_WsConnections = new();
        readonly object m_WsLock = new();

        // Pre-loaded SPA bytes. Keyed by relative path under m_ClinicianRoot
        // (e.g. "index.html", "app.js"). Filled at OnEnable so the first
        // browser request doesn't pay a disk read on the Unity main thread.
        readonly Dictionary<string, byte[]> m_StaticCache =
            new(StringComparer.OrdinalIgnoreCase);

        public bool IsRunning => m_Server != null && m_Server.IsRunning;
        public string BaseUrl { get; private set; } = "";

        void OnEnable()
        {
            m_ClinicianRoot = Path.Combine(Application.streamingAssetsPath, "clinician");
            m_LogsRoot = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(m_LogsRoot);

            WarmStaticCache();

            try
            {
                m_Server = new MiniHttpServer
                {
                    OnHttpRequest = HandleHttp,
                    OnWebSocketConnected = HandleWebSocket,
                };
                m_Server.Start(m_Port, m_BindToAllInterfaces);

                BaseUrl = m_BindToAllInterfaces
                    ? $"http://{GetBestLocalIpv4()}:{m_Port}"
                    : $"http://localhost:{m_Port}";

                SubscribeToAppEvents();
                Debug.Log($"[HitMissNetworkServer] Listening at {BaseUrl} — open this in a browser on the same Wi-Fi to drive the session.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HitMissNetworkServer] Failed to start: {e.Message}");
            }
        }

        // Eager-loads every file under m_ClinicianRoot into memory so the
        // first browser request hits a dictionary lookup instead of a Unity
        // main-thread disk read. Big wins on initial page load right after
        // pressing Play in the editor.
        void WarmStaticCache()
        {
            m_StaticCache.Clear();
            if (!Directory.Exists(m_ClinicianRoot)) return;

            int count = 0;
            long bytes = 0;
            string rootFull = Path.GetFullPath(m_ClinicianRoot);
            foreach (var file in Directory.EnumerateFiles(m_ClinicianRoot, "*", SearchOption.AllDirectories))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(file);
                    string rel = Path.GetFullPath(file).Substring(rootFull.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    m_StaticCache[rel] = data;
                    count++;
                    bytes += data.LongLength;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HitMissNetworkServer] Failed to cache {file}: {e.Message}");
                }
            }
            Debug.Log($"[HitMissNetworkServer] Static cache warmed: {count} files, {bytes / 1024} KB.");
        }

        void OnDisable()
        {
            UnsubscribeFromAppEvents();
            try { m_Server?.Dispose(); } catch { }
            m_Server = null;
            lock (m_WsLock)
            {
                foreach (var c in m_WsConnections.ToArray())
                    try { c.Dispose(); } catch { }
                m_WsConnections.Clear();
            }
        }

        void Update()
        {
            while (m_MainThreadQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // ---- HTTP request dispatcher ----

        async Task HandleHttp(MiniHttpRequest req, MiniHttpResponse resp)
        {
            // Same-origin CORS allow-all. Safe for a LAN-local console.
            resp.Headers["access-control-allow-origin"] = "*";
            resp.Headers["access-control-allow-methods"] = "GET, POST, OPTIONS";
            resp.Headers["access-control-allow-headers"] = "Content-Type";

            if (req.Method == "OPTIONS")
            {
                resp.StatusCode = 204;
                return;
            }

            string path = req.Path;
            int qmark = path.IndexOf('?');
            if (qmark >= 0) path = path.Substring(0, qmark);

            if (path.StartsWith("/api/"))
            {
                await HandleApi(req, resp, path);
                return;
            }

            HandleStatic(req, resp, path);
        }

        async Task HandleApi(MiniHttpRequest req, MiniHttpResponse resp, string path)
        {
            // Sessions library: GET /api/sessions/{id}/{kind}
            if (req.Method == "GET" && path.StartsWith("/api/sessions/") && path.Length > "/api/sessions/".Length)
            {
                string rest = path.Substring("/api/sessions/".Length);
                int slash = rest.IndexOf('/');
                string sessionFolder = slash > 0 ? rest.Substring(0, slash) : rest;
                string kind = slash > 0 ? rest.Substring(slash + 1) : "metadata";
                ServeSessionFile(resp, sessionFolder, kind);
                return;
            }

            switch ($"{req.Method} {path}")
            {
                case "GET /api/status":
                    var status = await BuildStatusAsync();
                    resp.SetJson(JsonUtility.ToJson(status));
                    break;
                case "GET /api/sessions":
                    resp.SetJson(JsonUtility.ToJson(BuildSessionList()));
                    break;
                case "POST /api/session/start":
                    await HandleStart(req, resp);
                    break;
                case "POST /api/session/pause":
                    await HandleSimple(resp, c => c.PauseSession(), p => p.PauseSession());
                    break;
                case "POST /api/session/resume":
                    await HandleSimple(resp, c => c.ResumeSession(), p => p.ResumeSession());
                    break;
                case "POST /api/session/stop":
                    await HandleSimple(resp, c => c.StopSession(), p => p.RequestStop());
                    break;
                default:
                    resp.StatusCode = 404;
                    resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("not_found", $"{req.Method} {path}")));
                    break;
            }
        }

        async Task HandleStart(MiniHttpRequest req, MiniHttpResponse resp)
        {
            StartSessionRequest startReq;
            try { startReq = JsonUtility.FromJson<StartSessionRequest>(req.Body); }
            catch (Exception e)
            {
                resp.StatusCode = 400;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("bad_json", e.Message)));
                return;
            }

            var ack = await RunOnMainThread(() =>
            {
                if (IsPpsMode)
                {
                    if (m_PpsAppController.IsRunning)
                        return AckResponse.Fail("session_already_running");
                    m_PpsAppController.StartSession(startReq.metadata);
                    return AckResponse.Ok();
                }

                if (m_AppController == null) return AckResponse.Fail("no_app_controller");
                if (m_AppController.CurrentPhase != TaskPhase.Idle) return AckResponse.Fail("session_already_running");
                m_AppController.SetSessionMetadata(startReq.metadata);
                m_AppController.StartSession();
                return AckResponse.Ok();
            });
            resp.StatusCode = ack.ok ? 200 : 409;
            resp.SetJson(JsonUtility.ToJson(ack));
        }

        async Task HandleSimple(MiniHttpResponse resp, Action<HitOrMissAppController> action,
                                Action<HitOrMiss.Pps.PpsAppController> ppsAction = null)
        {
            var ack = await RunOnMainThread(() =>
            {
                if (IsPpsMode)
                {
                    if (ppsAction == null)
                        return AckResponse.Fail("not_supported_in_pps_mode",
                            "Task 1 doesn't support this command.");
                    ppsAction(m_PpsAppController);
                    return AckResponse.Ok();
                }
                if (m_AppController == null) return AckResponse.Fail("no_app_controller");
                action(m_AppController);
                return AckResponse.Ok();
            });
            resp.StatusCode = ack.ok ? 200 : 409;
            resp.SetJson(JsonUtility.ToJson(ack));
        }

        // ---- Status / sessions ----

        Task<ServerStatusEvent> BuildStatusAsync()
        {
            return RunOnMainThread(() =>
            {
                if (IsPpsMode)
                {
                    string ppsPhase = !m_PpsAppController.IsRunning
                        ? "Idle"
                        : (m_PpsAppController.IsPaused ? "PpsPaused" : "PpsRunning");
                    return new ServerStatusEvent
                    {
                        protocolVersion     = Protocol.Version,
                        phase               = ppsPhase,
                        participantId       = m_PpsAppController.ParticipantId,
                        currentBlockIndex   = m_PpsTaskManager != null ? m_PpsTaskManager.CurrentBlockIndex : -1,
                        isRunning           = m_PpsAppController.IsRunning,
                        isPaused            = m_PpsAppController.IsPaused,
                        isRecording         = m_PpsAppController.IsRecording,
                        trialsCompletedInBlock = m_PpsTaskManager != null ? m_PpsTaskManager.TrialsCompletedInBlock : 0,
                        totalTrialsInBlock     = m_PpsTaskManager != null ? m_PpsTaskManager.TotalTrialsInBlock     : 0,
                        taskKind               = TaskKind.Task1Pps.ToString(),
                    };
                }

                var s = new ServerStatusEvent
                {
                    protocolVersion = Protocol.Version,
                    phase = m_AppController != null ? m_AppController.CurrentPhase.ToString() : "Idle",
                    participantId = m_AppController != null ? m_AppController.ParticipantId : "",
                    currentBlockIndex = m_AppController != null ? m_AppController.CurrentBlockIndex : -1,
                    isRunning = m_TaskManager != null && m_TaskManager.IsRunning,
                    isPaused = m_TaskManager != null && m_TaskManager.IsPaused,
                    isRecording = m_AppController != null && m_AppController.IsRecording,
                    taskKind = TaskKind.Task2HitOrMiss.ToString(),
                };
                if (m_TaskManager != null)
                {
                    s.trialsCompletedInBlock = m_TaskManager.TrialsCompletedInBlock;
                    s.totalTrialsInBlock = m_TaskManager.TotalTrialsInBlock;
                }
                return s;
            });
        }

        SessionListResponse BuildSessionList()
        {
            var list = new List<SessionSummary>();
            if (Directory.Exists(m_LogsRoot))
            {
                foreach (var dir in Directory.EnumerateDirectories(m_LogsRoot))
                {
                    var folder = Path.GetFileName(dir);
                    var summary = new SessionSummary { sessionFolder = folder };

                    string metadataPath = Path.Combine(dir, "metadata.json");
                    if (File.Exists(metadataPath))
                    {
                        try
                        {
                            var md = JsonUtility.FromJson<SessionMetadata>(File.ReadAllText(metadataPath));
                            summary.participantId = md.participantId;
                            summary.sessionDate = md.sessionDate;
                            summary.sessionId = md.sessionId;
                        }
                        catch { /* leave fields blank */ }
                    }

                    string trialsPath = Path.Combine(dir, "trials.csv");
                    if (File.Exists(trialsPath))
                        summary.trialCount = Math.Max(0, File.ReadAllLines(trialsPath).Length - 1);

                    summary.hasProgressSnapshot = File.Exists(Path.Combine(dir, "progress.json"));
                    list.Add(summary);
                }
            }
            return new SessionListResponse
            {
                protocolVersion = Protocol.Version,
                sessions = list.OrderByDescending(s => s.sessionFolder).ToArray(),
            };
        }

        void ServeSessionFile(MiniHttpResponse resp, string sessionFolder, string kind)
        {
            if (string.IsNullOrEmpty(sessionFolder) || sessionFolder.Contains("..") || sessionFolder.Contains('/') || sessionFolder.Contains('\\'))
            {
                resp.StatusCode = 400;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("bad_session_id")));
                return;
            }

            string dir = Path.Combine(m_LogsRoot, sessionFolder);
            if (!Directory.Exists(dir))
            {
                resp.StatusCode = 404;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("session_not_found", sessionFolder)));
                return;
            }

            string filename = kind switch
            {
                "metadata"    => "metadata.json",
                "trials"      => "trials.csv",
                "eyetracking" => "eyetracking.csv",
                "session"     => "session.json",
                "progress"    => "progress.json",
                _             => null,
            };
            if (filename == null)
            {
                resp.StatusCode = 404;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("unknown_kind", kind)));
                return;
            }

            string filePath = Path.Combine(dir, filename);
            if (!File.Exists(filePath))
            {
                resp.StatusCode = 404;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("file_not_found", filename)));
                return;
            }

            ServeFile(resp, filePath);
        }

        // ---- Static file serving ----

        void HandleStatic(MiniHttpRequest req, MiniHttpResponse resp, string path)
        {
            if (path == "/" || path == "")
            {
                resp.StatusCode = 302;
                resp.Headers["location"] = "/clinician/index.html";
                return;
            }

            string rel;
            if (path.StartsWith("/clinician/")) rel = path.Substring("/clinician/".Length);
            else if (path == "/clinician") rel = "index.html";
            else
            {
                resp.StatusCode = 404;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("not_found", path)));
                return;
            }

            if (string.IsNullOrEmpty(rel)) rel = "index.html";

            // Try the warmed cache first. Hit = sub-microsecond serve, no
            // disk I/O on the Unity main thread. Miss = fall back to a disk
            // read (handles files added after Play, e.g. live editing).
            if (m_StaticCache.TryGetValue(rel, out var cachedBytes))
            {
                resp.StatusCode = 200;
                resp.Headers["content-type"] = MimeFor(rel);
                // Tell the browser it MAY cache but must revalidate. We don't
                // emit ETag yet, so this is effectively "always revalidate";
                // good for dev where SPA edits are frequent.
                resp.Headers["cache-control"] = "no-cache";
                resp.Body = cachedBytes;
                return;
            }

            string fullPath = Path.GetFullPath(Path.Combine(m_ClinicianRoot, rel));
            if (!fullPath.StartsWith(Path.GetFullPath(m_ClinicianRoot)))
            {
                resp.StatusCode = 403;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("forbidden")));
                return;
            }

            if (!File.Exists(fullPath))
            {
                resp.StatusCode = 404;
                resp.SetJson(JsonUtility.ToJson(AckResponse.Fail("not_found", rel)));
                return;
            }

            ServeFile(resp, fullPath);
        }

        static void ServeFile(MiniHttpResponse resp, string path)
        {
            // Read into memory for simplicity. Files served here (CSV, JSON,
            // small static SPA assets) are well under the few-MB threshold
            // where streaming would pay off.
            byte[] body = File.ReadAllBytes(path);
            resp.StatusCode = 200;
            resp.Headers["content-type"] = MimeFor(path);
            resp.Body = body;
        }

        static string MimeFor(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js"   => "text/javascript; charset=utf-8",
                ".css"  => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".csv"  => "text/csv; charset=utf-8",
                ".png"  => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg"  => "image/svg+xml",
                ".ico"  => "image/x-icon",
                _       => "application/octet-stream",
            };
        }

        // ---- WebSocket ----

        async Task HandleWebSocket(MiniHttpRequest req, MiniWebSocket ws)
        {
            int activeAfterAdd;
            lock (m_WsLock) { m_WsConnections.Add(ws); activeAfterAdd = m_WsConnections.Count; }
            Debug.Log($"[HitMissNetworkServer] WS connected from {req.RemoteEndPoint}. Active connections: {activeAfterAdd}");

            try
            {
                // Initial status push so the GUI shows live state immediately.
                try
                {
                    var status = await BuildStatusAsync();
                    await ws.SendTextAsync(JsonUtility.ToJson(WsEnvelope.For("server_status", status)));
                }
                catch (Exception e) { Debug.LogWarning($"[HitMissNetworkServer] initial status push failed: {e.Message}"); }

                // Keep the connection open until the peer disconnects. The
                // client doesn't send anything we care about today, but reading
                // is what detects a broken connection promptly.
                while (ws.IsOpen)
                {
                    string msg = await ws.ReceiveTextAsync();
                    if (msg == null) break;
                    // Ignore inbound messages for now.
                }
            }
            finally
            {
                int activeAfterRemove;
                lock (m_WsLock) { m_WsConnections.Remove(ws); activeAfterRemove = m_WsConnections.Count; }
                Debug.Log($"[HitMissNetworkServer] WS disconnected. Active connections: {activeAfterRemove}");
            }
        }

        void Broadcast<T>(string type, T payload)
        {
            string json = JsonUtility.ToJson(WsEnvelope.For(type, payload));
            MiniWebSocket[] snapshot;
            lock (m_WsLock) snapshot = m_WsConnections.ToArray();
            foreach (var c in snapshot)
            {
                _ = SafeSend(c, json);
            }
        }

        static async Task SafeSend(MiniWebSocket ws, string text)
        {
            try { await ws.SendTextAsync(text); }
            catch { /* connection probably gone; receive loop will tear it down */ }
        }

        // ---- App-controller event subscriptions ----

        void SubscribeToAppEvents()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused += OnSessionPaused;
                m_AppController.SessionResumed += OnSessionResumed;
                m_AppController.PhaseChanged += OnPhaseChanged;
                m_AppController.SessionStarted += OnSessionStarted;
                m_AppController.SessionEnded += OnSessionEnded;
            }
            if (m_TaskManager != null)
            {
                m_TaskManager.TrialSpawned += OnTrialSpawned;
                m_TaskManager.TrialJudged += OnTrialJudged;
                m_TaskManager.BlockStarted += OnBlockStarted;
                m_TaskManager.BlockEnded += OnBlockEnded;
            }
            if (m_PpsTaskManager != null)
            {
                m_PpsTaskManager.TrialStarted   += OnPpsTrialStarted;
                m_PpsTaskManager.TrialCompleted += OnPpsTrialCompleted;
            }
            if (m_PpsAppController != null)
            {
                m_PpsAppController.SessionStarted += OnSessionStarted;
                m_PpsAppController.SessionEnded   += OnSessionEnded;
                m_PpsAppController.SessionPaused  += OnSessionPaused;
                m_PpsAppController.SessionResumed += OnSessionResumed;
                m_PpsAppController.RecordingStarted += OnRecordingStarted;
            }
        }

        void UnsubscribeFromAppEvents()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused -= OnSessionPaused;
                m_AppController.SessionResumed -= OnSessionResumed;
                m_AppController.PhaseChanged -= OnPhaseChanged;
                m_AppController.SessionStarted -= OnSessionStarted;
                m_AppController.SessionEnded -= OnSessionEnded;
            }
            if (m_TaskManager != null)
            {
                m_TaskManager.TrialSpawned -= OnTrialSpawned;
                m_TaskManager.TrialJudged -= OnTrialJudged;
                m_TaskManager.BlockStarted -= OnBlockStarted;
                m_TaskManager.BlockEnded -= OnBlockEnded;
            }
            if (m_PpsTaskManager != null)
            {
                m_PpsTaskManager.TrialStarted   -= OnPpsTrialStarted;
                m_PpsTaskManager.TrialCompleted -= OnPpsTrialCompleted;
            }
            if (m_PpsAppController != null)
            {
                m_PpsAppController.SessionStarted -= OnSessionStarted;
                m_PpsAppController.SessionEnded   -= OnSessionEnded;
                m_PpsAppController.SessionPaused  -= OnSessionPaused;
                m_PpsAppController.SessionResumed -= OnSessionResumed;
                m_PpsAppController.RecordingStarted -= OnRecordingStarted;
            }
        }

        void OnRecordingStarted()
        {
            Broadcast("recording_started", new SimpleEvent { note = "recording" });
            _ = BroadcastStatusAsync();
        }

        // ---- PPS event handlers ----
        // Map PPS trial events onto the same wire types used by Task 2 so a
        // browser client doesn't need to know which task is running. Fields
        // that have no PPS analogue (category, speedMps as m/s, isSwitch)
        // are left at their defaults.

        void OnPpsTrialStarted(HitOrMiss.Pps.PpsTrialDefinition trial)
        {
            Broadcast("trial_started", new TrialStartedEvent
            {
                trialId            = trial.trialId,
                blockIndex         = trial.blockIndex,
                trialNumberInBlock = 0,
                category           = trial.modality.ToString(),
                trajectoryId       = trial.vibrationStage.ToString(),
                speedMps           = 0f,
                isSwitchTrial      = false,
            });
        }

        void OnPpsTrialCompleted(HitOrMiss.Pps.PpsTrialResult result)
        {
            Broadcast("trial_completed", new TrialCompletedEvent
            {
                trialId            = result.definition.trialId,
                blockIndex         = result.definition.blockIndex,
                trialNumberInBlock = 0,
                category           = result.definition.modality.ToString(),
                expected           = "",
                received           = "",
                result             = result.responded ? "Correct" : "NoResponse",
                isCorrect          = result.responded,
                reactionTimeMs     = result.reactionTimeMs,
                speedMps           = 0f,
                isSwitchTrial      = false,
            });
        }

        void OnPhaseChanged(TaskPhase phase)
        {
            Broadcast("phase_changed", new PhaseChangedEvent
            {
                phase = phase.ToString(),
                currentBlockIndex = m_AppController != null ? m_AppController.CurrentBlockIndex : -1,
            });
            _ = BroadcastStatusAsync();
        }

        void OnSessionStarted()
        {
            Broadcast("session_started", new SimpleEvent { note = "started" });
            _ = BroadcastStatusAsync();
        }

        void OnSessionEnded()
        {
            Broadcast("session_ended", new SimpleEvent { note = "ended" });
            _ = BroadcastStatusAsync();
        }

        void OnSessionPaused()
        {
            Broadcast("session_paused", new SimpleEvent { note = "paused" });
            _ = BroadcastStatusAsync();
        }

        void OnSessionResumed()
        {
            Broadcast("session_resumed", new SimpleEvent { note = "resumed" });
            _ = BroadcastStatusAsync();
        }

        void OnTrialSpawned(string trialId, TrialDefinition def)
        {
            Broadcast("trial_started", new TrialStartedEvent
            {
                trialId = trialId,
                blockIndex = def.blockIndex,
                trialNumberInBlock = def.trialIndexInBlock + 1,
                category = def.category.ToString(),
                trajectoryId = def.trajectoryId,
                speedMps = def.speed,
                isSwitchTrial = def.isSwitchTrial,
            });
        }

        void OnTrialJudged(TrialJudgement j)
        {
            Broadcast("trial_completed", new TrialCompletedEvent
            {
                trialId = j.trialId,
                blockIndex = j.blockIndex,
                trialNumberInBlock = j.trialNumberInBlock,
                category = j.category.ToString(),
                expected = j.expected.ToString(),
                received = j.received.ToString(),
                result = j.result.ToString(),
                isCorrect = j.isCorrect,
                reactionTimeMs = j.reactionTimeMs,
                speedMps = j.speedMps,
                isSwitchTrial = j.isSwitchTrial,
            });
        }

        void OnBlockStarted(int blockIndex)
        {
            Broadcast("phase_changed", new PhaseChangedEvent { phase = "Block", currentBlockIndex = blockIndex });
        }

        void OnBlockEnded(int blockIndex)
        {
            Broadcast("phase_changed", new PhaseChangedEvent { phase = "BlockEnded", currentBlockIndex = blockIndex });
            _ = BroadcastStatusAsync();
        }

        async Task BroadcastStatusAsync()
        {
            try
            {
                var s = await BuildStatusAsync();
                Broadcast("server_status", s);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ---- Helpers ----

        Task<T> RunOnMainThread<T>(Func<T> fn)
        {
            var tcs = new TaskCompletionSource<T>();
            m_MainThreadQueue.Enqueue(() =>
            {
                try { tcs.SetResult(fn()); }
                catch (Exception e) { tcs.SetException(e); }
            });
            return tcs.Task;
        }

        static string GetBestLocalIpv4()
        {
            try
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch { }
            return "localhost";
        }
    }
}
