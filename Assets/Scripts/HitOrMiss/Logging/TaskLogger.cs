using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Single per-session data writer for both Task 1 (PPS) and Task 2
    /// (Hit-or-Miss). Selects file names, trials schema, and setup.json
    /// content from the <see cref="TaskKind"/> passed to BeginSession.
    ///
    /// Layout per session, under
    /// <c>{cwd}/Logger/{participantId}_{sessionId}/</c>:
    ///
    ///   metadata.json                                  — flat snapshot for server reloads
    ///   sub-{id}_session-{n}_setup.json                — nested spec; only active task's params
    ///   sub-{id}_session-{n}_task{1|2}_trials.csv      — per-task schema
    ///   sub-{id}_session-{n}_task{1|2}_eyetracking.csv — header-only stub
    ///   sub-{id}_session-{n}_task{1|2}_session.json    — final consolidated dump
    ///   progress.json                                  — written on Pause / mid-session Flush
    /// </summary>
    public class TaskLogger : MonoBehaviour
    {
        /// Pam added for logging system
        [Header("Shared Session Folder")]
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;
        string m_ParticipantId;
        string m_SessionId;
        ///


        SessionMetadata m_Metadata;
        bool m_MetadataExplicitlySet;

        TaskKind m_TaskKind = TaskKind.Task2HitOrMiss;

        string m_SessionDir;
        string m_TrialsCsvPath;
        string m_EyeCsvPath;
        string m_SetupJsonPath;
        string m_FinalJsonPath;

        StreamWriter m_TrialsWriter;

        // Each task has its own in-memory trial list because the schemas
        // and final session.json structures differ.
        readonly List<TrialJudgement> m_Task2Judgements = new();
        readonly List<HitOrMiss.Pps.PpsTrialResult> m_Task1Results = new();
        bool m_SessionOpen;

        public string ParticipantId
        {
            get => m_ParticipantId;
            set => m_ParticipantId = value;
        }

        /// <summary>Read-only view of the active session folder. Empty before BeginSession.</summary>
        public string SessionDirectory => m_SessionDir;

        /// <summary>Which task this logger is currently recording. Set by BeginSession.</summary>
        public TaskKind ActiveTaskKind => m_TaskKind;

        /// <summary>True between BeginSession and EndSession. Lets app
        /// controllers expose a real "currently recording" signal to the
        /// clinician UI instead of inferring it from session state.</summary>
        public bool IsSessionOpen => m_SessionOpen;

        /// <summary>
        /// Supplies the session metadata that will be written to
        /// <c>metadata.json</c> and stamped into each trial row. Must be called
        /// before <see cref="BeginSession(TaskKind, string)"/>; otherwise a
        /// default metadata record (using the current participant id) is used.
        /// </summary>
        public void SetMetadata(SessionMetadata metadata)
        {
            m_Metadata = metadata;
            m_MetadataExplicitlySet = true;

        }

        /// <summary>
        /// Back-compat overload. Defaults to Task 2 so existing Task 2 callers
        /// still work. New code should call the TaskKind overload explicitly.
        /// </summary>
        public void BeginSession(string taskName)
            => BeginSession(TaskKind.Task2HitOrMiss, taskName);

        /// <summary>
        /// Opens a new session folder and prepares writers for the given task.
        /// File names and the setup.json contents adapt so a Task 1 session
        /// never contains Task 2 parameters (and vice versa).
        /// </summary>
        public void BeginSession(TaskKind taskKind, string taskName)
        {
            if (m_SessionOpen)
            {
                Debug.LogWarning("[TaskLogger] BeginSession called while a session was still open. Closing previous session first.");
                EndSession();
            }

            m_TaskKind = taskKind;

            // Pam added for new logger system
            if (m_EegMarkerEmitter == null)
            {
                Debug.LogError("[TaskLogger] Missing EegMarkerEmitter reference. Cannot use shared session folder.");
                return;
            }

            if (string.IsNullOrEmpty(m_EegMarkerEmitter.SessionDirectory))
            {
                Debug.LogError("[TaskLogger] EegMarkerEmitter has not created a session directory yet. Call EegMarkerEmitter.BeginSession(...) before TaskLogger.BeginSession(...).");
                return;
            }

            m_ParticipantId = m_EegMarkerEmitter.ParticipantId;
            m_SessionId = m_EegMarkerEmitter.SessionId;
            m_SessionDir = m_EegMarkerEmitter.SessionDirectory;
            //

            string idSlug = m_ParticipantId.Replace(" ", "_");
            int sn = m_Metadata.sessionNumber > 0 ? m_Metadata.sessionNumber : 1;
            string taskSlug = TaskFileSlug(taskKind);

            // Claim a task-specific marker CSV before any trial markers are emitted.
            // The emitter opens one shared markers.csv per session with append:false,
            // so without this the second task to start in a session folder truncated
            // the first task's marker stream and its EEG triggers were lost.
            m_EegMarkerEmitter.BindTaskScope(taskSlug);

            m_TrialsCsvPath = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_{taskSlug}_trials.csv");
            m_EyeCsvPath    = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_{taskSlug}_eyetracking.csv");
            m_SetupJsonPath = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_setup.json");
            m_FinalJsonPath = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_{taskSlug}_session.json");

            EnsureMetadataDefaults(taskName);
            WriteMetadataJson();

            m_TrialsWriter = new StreamWriter(m_TrialsCsvPath, false, Encoding.UTF8);
            m_TrialsWriter.WriteLine(TrialsHeaderFor(taskKind));
            m_TrialsWriter.Flush();

            // Eye-tracking CSV is stubbed (header only) for now so the
            // EyeTrackingLogger has a known sibling file to append to once
            // wired up. Same header is fine for both tasks.
            using (var eye = new StreamWriter(m_EyeCsvPath, false, Encoding.UTF8))
            {
                eye.WriteLine("trial_id,block_number,timestamp,gaze_origin_x,gaze_origin_y,gaze_origin_z,gaze_dir_x,gaze_dir_y,gaze_dir_z,left_pupil_diam_mm,right_pupil_diam_mm");
            }

            m_Task1Results.Clear();
            m_Task2Judgements.Clear();
            m_SessionOpen = true;

            Debug.Log($"[TaskLogger] Session started ({taskKind}). Folder: {m_SessionDir}");
        }

        /// <summary>Task 2 trial row. Skips practice trials.</summary>
        public void LogTrial(TrialJudgement j)
        {
            if (!m_SessionOpen) return;
            if (m_TaskKind != TaskKind.Task2HitOrMiss)
            {
                Debug.LogWarning("[TaskLogger] LogTrial(TrialJudgement) called during a Task 1 session. Ignored.");
                return;
            }

            // Practice trials are not stored per spec. Practice carries
            // blockIndex == -1 or trialId starting with "PRACTICE_".
            if (j.blockIndex < 0 || (j.trialId != null && j.trialId.StartsWith("PRACTICE_")))
                return;

            m_Task2Judgements.Add(j);
            m_TrialsWriter.WriteLine(BuildTask2Row(j));
            m_TrialsWriter.Flush();
        }

        /// <summary>Task 1 trial row. Skips practice trials.</summary>
        public void LogTrial(HitOrMiss.Pps.PpsTrialResult result)
        {
            Debug.Log("[TaskLogger/PPS] LogTrial ENTER");

            if (!m_SessionOpen)
            {
                Debug.LogWarning("[TaskLogger/PPS] Session is not open. Skipping log.");
                return;
            }

            if (m_TaskKind != TaskKind.Task1Pps)
            {
                Debug.LogWarning($"[TaskLogger/PPS] Received PPS result while configured for {m_TaskKind}. Skipping log.");
                return;
            }

            Debug.Log($"[TaskLogger/PPS] result exists: {result}");

            if (m_Task1Results == null)
            {
                Debug.LogError("[TaskLogger/PPS] m_Task1Results is NULL.");
                return;
            }

            if (m_TrialsWriter == null)
            {
                Debug.LogError("[TaskLogger/PPS] m_TrialsWriter is NULL. Was StartSession/OpenSession called?");
                return;
            }

            try
            {
                Debug.Log("[TaskLogger/PPS] Adding result to memory list.");
                m_Task1Results.Add(result);

                Debug.Log("[TaskLogger/PPS] Building CSV row.");
                string row = result.ToCsvRow(
                    m_Metadata.participantId,
                    m_Metadata.sessionNumber
                );

                Debug.Log($"[TaskLogger/PPS] CSV row built: {row}");

                Debug.Log("[TaskLogger/PPS] Writing row.");
                m_TrialsWriter.WriteLine(row);

                Debug.Log("[TaskLogger/PPS] Flushing writer.");
                m_TrialsWriter.Flush();

                Debug.Log("[TaskLogger/PPS] LogTrial EXIT OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[TaskLogger/PPS] LogTrial crashed, skipping logging so task can continue:\n" + ex);
                return;
            }
        }

        public void EndSession()
        {
            if (!m_SessionOpen) return;

            m_TrialsWriter?.Close();
            m_TrialsWriter = null;

            string json = m_TaskKind == TaskKind.Task1Pps
                ? JsonUtility.ToJson(new Task1SessionLog
                {
                    metadata    = m_Metadata,
                    timestamp   = DateTime.Now.ToString("o"),
                    totalTrials = m_Task1Results.Count,
                    results     = m_Task1Results.ToArray(),
                }, true)
                : JsonUtility.ToJson(new Task2SessionLog
                {
                    metadata    = m_Metadata,
                    timestamp   = DateTime.Now.ToString("o"),
                    totalTrials = m_Task2Judgements.Count,
                    judgements  = m_Task2Judgements.ToArray(),
                }, true);

            json = StripInactiveTaskMetadata(json, m_TaskKind);

            File.WriteAllText(m_FinalJsonPath, json, Encoding.UTF8);

            m_SessionOpen = false;
            Debug.Log($"[TaskLogger] Session ended. Logs saved to {m_SessionDir}");
        }

        /// <summary>
        /// Flushes the trials CSV and writes a small progress snapshot so a
        /// paused or interrupted session can be inspected or resumed later.
        /// Called by the app controller on pause.
        /// </summary>
        public void Flush(int currentBlockIndex, int nextTrialIndex)
        {
            if (!m_SessionOpen) return;

            m_TrialsWriter?.Flush();

            int trialsCompleted = m_TaskKind == TaskKind.Task1Pps
                ? m_Task1Results.Count
                : m_Task2Judgements.Count;

            string progressPath = Path.Combine(m_SessionDir, "progress.json");
            string json = JsonUtility.ToJson(new ProgressSnapshot
            {
                participantId     = m_ParticipantId,
                sessionId         = m_SessionId,
                taskKind          = m_TaskKind.ToString(),
                timestamp         = DateTime.Now.ToString("o"),
                currentBlockIndex = currentBlockIndex,
                nextTrialIndex    = nextTrialIndex,
                trialsCompleted   = trialsCompleted,
            }, true);
            File.WriteAllText(progressPath, json, Encoding.UTF8);

            Debug.Log($"[TaskLogger] Flushed. Progress snapshot: {progressPath}");
        }

        void OnDestroy()
        {
            if (m_SessionOpen)
                EndSession();
        }

        // ---- Helpers ----

        static string TaskFileSlug(TaskKind kind) => kind switch
        {
            TaskKind.Task1Pps        => "task1",
            TaskKind.Task2HitOrMiss  => "task2",
            _                        => "task",
        };

        static string TrialsHeaderFor(TaskKind kind) => kind switch
        {
            TaskKind.Task1Pps        => HitOrMiss.Pps.PpsTrialResult.CsvHeader,
            TaskKind.Task2HitOrMiss  => BuildTask2Header(),
            _                        => "",
        };

        void EnsureMetadataDefaults(string taskName)
        {
            if (!m_MetadataExplicitlySet)
                m_Metadata = SessionMetadata.CreateDefault(m_ParticipantId);

            if (string.IsNullOrEmpty(m_Metadata.participantId))
                m_Metadata.participantId = m_ParticipantId;
            if (string.IsNullOrEmpty(m_Metadata.sessionId))
                m_Metadata.sessionId = m_SessionId;
            if (string.IsNullOrEmpty(m_Metadata.sessionDate))
                m_Metadata.sessionDate = DateTime.Now.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Removes the inactive task's metadata keys from a serialized session
        /// log.
        ///
        /// JsonUtility emits every field of SessionMetadata regardless of value,
        /// so a Task 1 session file would otherwise carry a full block of Task 2
        /// settings that were never used, written as zeros. In the archive those
        /// zeros are indistinguishable from values that were deliberately set,
        /// so the keys are dropped instead.
        ///
        /// Operates on the "metadata" object only. Trial rows are untouched.
        /// </summary>
        static string StripInactiveTaskMetadata(string json, TaskKind taskKind)
        {
            string prefix = taskKind == TaskKind.Task1Pps ? "\"task2" : "\"task1";

            string[] lines = json.Split('\n');

            int metaIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("\"metadata\"") && t.EndsWith("{"))
                {
                    metaIndex = i;
                    break;
                }
            }

            // No metadata block found: leave the document exactly as it was.
            if (metaIndex < 0) return json;

            int metaIndent  = IndentWidth(lines[metaIndex]);
            int childIndent = metaIndent + 4;

            var kept = new List<string>();
            for (int i = 0; i <= metaIndex; i++)
                kept.Add(lines[i]);

            int cursor = metaIndex + 1;

            while (cursor < lines.Length)
            {
                string line    = lines[cursor];
                string trimmed = line.Trim();
                int    indent  = IndentWidth(line);

                // Closing brace of the metadata object: done.
                if (indent == metaIndent && trimmed.StartsWith("}"))
                    break;

                if (indent == childIndent && trimmed.StartsWith(prefix))
                {
                    // Array or nested object: skip through its closing bracket.
                    if (trimmed.EndsWith("[") || trimmed.EndsWith("{"))
                    {
                        string closer = trimmed.EndsWith("[") ? "]" : "}";
                        cursor++;

                        while (cursor < lines.Length)
                        {
                            if (IndentWidth(lines[cursor]) == childIndent &&
                                lines[cursor].Trim().StartsWith(closer))
                                break;

                            cursor++;
                        }
                    }

                    cursor++;
                    continue;
                }

                kept.Add(line);
                cursor++;
            }

            // The removed keys may have been the last entries in the object,
            // which would leave a dangling comma on the entry before them.
            if (kept.Count > 0)
            {
                int    last    = kept.Count - 1;
                string trimEnd = kept[last].TrimEnd();

                if (trimEnd.EndsWith(","))
                    kept[last] = trimEnd.Substring(0, trimEnd.Length - 1);
            }

            for (int i = cursor; i < lines.Length; i++)
                kept.Add(lines[i]);

            return string.Join("\n", kept);
        }

        static int IndentWidth(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            return i;
        }

        void WriteMetadataJson()
        {
            // setup.json: only the active task's parameter block is included
            // so Task 1 sessions don't carry Task 2 settings (and vice versa).
            File.WriteAllText(m_SetupJsonPath, m_Metadata.ToSetupJson(m_TaskKind), Encoding.UTF8);
            // metadata.json: flat JsonUtility format for server-side reload
            // (sessions browser uses this to read back participant id /
            // session date when listing past sessions).
            string flatPath = Path.Combine(m_SessionDir, "metadata.json");
            File.WriteAllText(flatPath, JsonUtility.ToJson(m_Metadata, true), Encoding.UTF8);
        }

        // ---- Task 2 row + header ----

        // CSV column order matches the spec's Task 2 trials.csv layout
        // (TASK 2 — HIT OR MISS TASK LOGIC.txt).
        static string BuildTask2Header() =>
            "subject_id,session_number,block_number,trial_number," +
            "trial_type,correct_response," +
            "previous_speed,current_speed," +
            "speed_sequence,transition_status," +
            "trajectory_offset_cm," +
            "trial_trigger_code,response_trigger_code,trigger_timestamp," +
            "participant_response,reaction_time_ms,accuracy," +
            "trial_interrupted,timestamp";

        string BuildTask2Row(TrialJudgement j)
        {
            var inv = CultureInfo.InvariantCulture;
            string F(double v) => double.IsNaN(v) ? "" : v.ToString("F4", inv);
            string F2(double v) => double.IsNaN(v) ? "" : v.ToString("F2", inv);
            string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", ";");

            int blockNumber = j.blockIndex + 1;
            int accuracy = j.isCorrect ? 1 : 0;
            int interrupted = j.trialInterrupted ? 1 : 0;
            float trajectoryOffsetCm = -j.lateralOffsetMeters * 100f;
            string prevSpeed = j.hasPreviousSpeed ? j.previousSpeedLevel.ToCode() : "none";
            string timestamp = System.DateTime.Now.ToString("o");

            return string.Join(",",
                Esc(m_Metadata.participantId),
                m_Metadata.sessionNumber.ToString(inv),
                blockNumber.ToString(inv),
                j.trialNumberInBlock.ToString(inv),
                Esc(j.category.ToCode()),
                Esc(j.CorrectResponseCode),
                Esc(prevSpeed),
                Esc(j.currentSpeedLevel.ToCode()),
                Esc(j.SpeedSequenceCode),
                Esc(j.transitionStatus.ToCode()),
                F2(trajectoryOffsetCm),
                j.trialTriggerCode.ToString(inv),
                j.responseTriggerCode.ToString(inv),
                F(j.triggerTimestamp),
                Esc(j.ParticipantResponseSpecCode),
                F2(j.reactionTimeMs),
                accuracy.ToString(inv),
                interrupted.ToString(inv),
                Esc(timestamp)
            );
        }

        // ---- Session.json structs ----

        [Serializable]
        struct Task2SessionLog
        {
            public SessionMetadata metadata;
            public string timestamp;
            public int totalTrials;
            public TrialJudgement[] judgements;
        }

        [Serializable]
        struct Task1SessionLog
        {
            public SessionMetadata metadata;
            public string timestamp;
            public int totalTrials;
            public HitOrMiss.Pps.PpsTrialResult[] results;
        }

        [Serializable]
        struct ProgressSnapshot
        {
            public string participantId;
            public string sessionId;
            public string taskKind;
            public string timestamp;
            public int currentBlockIndex;
            public int nextTrialIndex;
            public int trialsCompleted;
        }
    }
}