using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace HitOrMiss
{
    /// <summary>
    /// Runtime scheduler for trajectory trials.
    /// Spawns looming objects on schedule, manages response windows,
    /// receives controller/keyboard input, scores judgements, and emits EEG markers.
    /// No feedback is shown to the participant.
    /// </summary>
    public class TrajectoryTaskManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] TrajectoryTaskAsset m_TaskAsset;
        [SerializeField] GameObject m_LoomingObjectPrefab;

        [Tooltip("Player anchor. Balls spawn at (position + forward × SpawnDistance) and travel toward this point.")]
        [SerializeField] Transform m_SpawnOrigin;

        [Tooltip("Optional: existing scene GameObject to use as the crosshair. If left empty, the manager instantiates CrosshairPrefab (or a built-in default) at the spawn point.")]
        [SerializeField] GameObject m_CrosshairTarget;

        [Tooltip("Optional prefab used as the crosshair. Auto-instantiated at the spawn point if CrosshairTarget is empty.")]
        [SerializeField] GameObject m_CrosshairPrefab;

        [Tooltip("Vertical offset added to the spawned/instantiated crosshair (meters). Use this to lift the crosshair to eye level relative to the player anchor.")]
        [SerializeField] float m_CrosshairHeightOffset = 1.5f;

        [Header("Response")]
        [Tooltip("Extra seconds after ball vanishes during which the participant can still respond")]
        [SerializeField] float m_ResponseGracePeriod = 1.5f;

        // Events
        public event Action<string, TrialDefinition> TrialSpawned;
        public event Action<TrialJudgement> TrialJudged;
        public event Action<int> BlockStarted;
        public event Action<int> BlockEnded;
        /// <summary>Fires every time a response is received (for UI indicator). Bool = was matched to a trial.</summary>
        public event Action<SemanticCommand, bool> ResponseIndicator;

        // Runtime state
        readonly List<RuntimeTrial> m_ActiveTrials = new();
        TrialDefinition[] m_BlockTrials;
        int m_NextTrialIndex;
        readonly List<TrialJudgement> m_AllResults = new();
        int m_CurrentBlock = -1;
        float m_BlockStartTime;
        float m_NextSpawnEarliest;
        bool m_Running;
        bool m_Paused;
        float m_LastSpawnEndTime;
        IResponseInputSource m_InputSource;
        EegMarkerEmitter m_MarkerEmitter;

        // Dedup window: collapses duplicate ResponseReceived events that fire
        // within k_DedupWindowSeconds of each other for the same command.
        // Quest Link can emit a controller-trigger event AND a hand-pinch
        // event (or two overlapping bindings on the same Action) for one
        // physical press — without dedup the second call generates a spurious
        // EEG marker and a "no active trial to match" warning.
        // 100 ms is well below any plausible deliberate double-tap and well
        // above inter-frame jitter for a single physical action.
        const float k_DedupWindowSeconds = 0.1f;
        float m_LastResponseTime = float.NegativeInfinity;
        SemanticCommand m_LastResponseCommand = SemanticCommand.None;

        public TrajectoryTaskAsset TaskAsset
        {
            get => m_TaskAsset;
            set => m_TaskAsset = value;
        }

        public IReadOnlyList<TrialJudgement> Results => m_AllResults;
        public bool IsRunning => m_Running;
        public bool IsPaused => m_Paused;
        public int CurrentBlock => m_CurrentBlock;
        public int TrialsCompletedInBlock { get; private set; }
        public int TotalTrialsInBlock => m_BlockTrials != null ? m_BlockTrials.Length : 0;
        public int NextTrialIndex => m_NextTrialIndex;

        public void SetInputSource(IResponseInputSource source)
        {
            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;

            m_InputSource = source;

            if (m_InputSource != null)
                m_InputSource.ResponseReceived += OnResponseReceived;
        }

        public void SetMarkerEmitter(EegMarkerEmitter emitter)
        {
            m_MarkerEmitter = emitter;
        }

        public void StartBlock(int blockIndex)
        {
            if (m_TaskAsset == null)
            {
                Debug.LogError("[TrajectoryTaskManager] No TaskAsset assigned.");
                return;
            }
            StartTrialList(blockIndex, m_TaskAsset.GenerateBlock(blockIndex));
        }

        /// <summary>
        /// Runs a pre-generated trial list (e.g. practice trials from
        /// <see cref="TrialGenerator.GeneratePracticeTrials"/>). Same lifecycle
        /// as <see cref="StartBlock"/> — events fire, the crosshair is
        /// enabled, and <see cref="IsRunning"/> goes true until every trial
        /// completes.
        /// </summary>
        public void StartTrialList(int blockIndex, TrialDefinition[] trials)
        {
            if (trials == null || trials.Length == 0)
            {
                Debug.LogWarning("[TrajectoryTaskManager] StartTrialList called with empty trial list.");
                return;
            }

            m_CurrentBlock = blockIndex;
            m_BlockTrials = trials;
            m_NextTrialIndex = 0;
            TrialsCompletedInBlock = 0;
            m_BlockStartTime = Time.time;
            m_NextSpawnEarliest = Time.time;
            m_LastSpawnEndTime = 0f;
            m_ActiveTrials.Clear();

            m_Running = true;
            m_InputSource?.Enable();
            EnsureCrosshair();
            SetCrosshairActive(true);

            m_MarkerEmitter?.Emit("block_start", "", blockIndex.ToString());
            BlockStarted?.Invoke(blockIndex);

            Debug.Log($"[TrajectoryTaskManager] Block {blockIndex + 1} started with {trials.Length} trials.");
        }

        public void StopBlock()
        {
            m_Running = false;
            m_Paused = false;
            m_InputSource?.Disable();
            SetCrosshairActive(false);

            // Resolve remaining active trials as NoResponse
            foreach (var trial in m_ActiveTrials)
            {
                if (!trial.Resolved)
                    ResolveTrial(trial, SemanticCommand.None, TrialResult.NoResponse, "block_stopped");
            }

            DespawnAll();
            m_MarkerEmitter?.Emit("block_end", "", m_CurrentBlock.ToString());
            BlockEnded?.Invoke(m_CurrentBlock);
        }

        /// <summary>
        /// Pauses the active block: stops spawning, freezes input, and discards
        /// in-flight balls without scoring them (the trial that was interrupted
        /// is treated as never having occurred — clinician will redo it on resume
        /// if needed). Block trial list, completed results, and next-trial cursor
        /// are preserved so see cref="ResumeBlock"/> can continue from where it left off.
        /// </summary>
        public void PauseBlock()
        {
            if (!m_Running || m_Paused) return;
            m_Paused = true;
            m_InputSource?.Disable();

            // Discard in-flight balls without scoring. They are not "no response" —
            // the trial was interrupted. They simply don't go into the log.
            DespawnAll();

            m_MarkerEmitter?.Emit("block_paused", "", m_CurrentBlock.ToString());
        }

        /// <summary>
        /// Resumes a paused block: re-enables input and starts spawning again
        /// from the next pending trial. Schedules the first post-resume spawn
        /// after one ITI so the participant has a beat to settle.
        /// </summary>
        public void ResumeBlock()
        {
            if (!m_Running || !m_Paused) return;
            m_Paused = false;
            m_InputSource?.Enable();
            m_NextSpawnEarliest = Time.time + NextItiSeconds();
            m_MarkerEmitter?.Emit("block_resumed", "", m_CurrentBlock.ToString());
        }

        void Update()
        {
            if (!m_Running || m_Paused) return;

            // Spawn next trial when its earliest spawn time has elapsed.
            // The earliest is set after each spawn to (just-spawned trial's
            // deadline + jittered ITI) so trials never overlap their response
            // windows and there's no fixed dead time.
            if (m_NextTrialIndex < m_BlockTrials.Length && Time.time >= m_NextSpawnEarliest)
            {
                var def = m_BlockTrials[m_NextTrialIndex];
                SpawnTrial(def);
                m_NextTrialIndex++;

                float deadline = def.Duration + m_ResponseGracePeriod;
                m_NextSpawnEarliest = Time.time + deadline + NextItiSeconds();
            }

            // Update active trials
            for (int i = m_ActiveTrials.Count - 1; i >= 0; i--)
            {
                var trial = m_ActiveTrials[i];
                float trialElapsed = Time.time - trial.SpawnTime;
                float duration = trial.Definition.Duration;
                float deadline = duration + m_ResponseGracePeriod;

                // Timeout: ball vanished AND grace period elapsed with no response
                if (!trial.Resolved && trialElapsed >= deadline)
                {
                    ResolveTrial(trial, SemanticCommand.None, TrialResult.NoResponse, "timeout");
                    m_MarkerEmitter?.Emit("trial_timeout", trial.Definition.trialId,
                        trial.Definition.category.ToString());
                }

                // Clean up after resolved and visual is done
                if (trial.Resolved && trialElapsed >= deadline)
                {
                    if (trial.ObjectController != null)
                        trial.ObjectController.Despawn();
                    m_ActiveTrials.RemoveAt(i);
                }
            }

            // Check if block is complete
            if (m_NextTrialIndex >= m_BlockTrials.Length && m_ActiveTrials.Count == 0)
            {
                m_Running = false;
                m_InputSource?.Disable();
                SetCrosshairActive(false);
                m_MarkerEmitter?.Emit("block_end", "", m_CurrentBlock.ToString());
                BlockEnded?.Invoke(m_CurrentBlock);
            }
        }

        void SetCrosshairActive(bool active)
        {
            if (m_CrosshairTarget != null) m_CrosshairTarget.SetActive(active);
        }

        /// <summary>
        /// Ensures a crosshair GameObject exists and is positioned at the spawn point
        /// (player anchor + forward × SpawnDistance, lifted by CrosshairHeightOffset).
        /// Order: 1) use the inspector-assigned scene GameObject if present;
        ///        2) instantiate CrosshairPrefab if assigned;
        ///        3) build a simple default (white cross of two thin quads) so the
        ///           participant always has a fixation marker.
        /// </summary>
        void EnsureCrosshair()
        {
            if (m_SpawnOrigin == null)
            {
                Debug.LogWarning("[TrajectoryTaskManager] EnsureCrosshair: m_SpawnOrigin is null. Assign a Transform — the player anchor — in the inspector.");
                return;
            }
            if (m_TaskAsset == null)
            {
                Debug.LogWarning("[TrajectoryTaskManager] EnsureCrosshair: m_TaskAsset is null. Cannot compute spawn point distance.");
                return;
            }

            if (m_CrosshairTarget != null)
            {
                Debug.Log($"[TrajectoryTaskManager] Reusing inspector-assigned crosshair: {m_CrosshairTarget.name}.");
                PositionCrosshair(m_CrosshairTarget.transform);
                return;
            }

            if (m_CrosshairPrefab != null)
            {
                m_CrosshairTarget = Instantiate(m_CrosshairPrefab, m_SpawnOrigin);
                m_CrosshairTarget.name = m_CrosshairPrefab.name + "(Crosshair)";
                Debug.Log($"[TrajectoryTaskManager] Instantiated crosshair from prefab: {m_CrosshairPrefab.name}.");
            }
            else
            {
                m_CrosshairTarget = BuildDefaultCrosshair();
                Debug.Log("[TrajectoryTaskManager] No CrosshairTarget or CrosshairPrefab assigned — built a default crosshair at the spawn point.");
            }

            PositionCrosshair(m_CrosshairTarget.transform);

            if (m_CrosshairTarget.GetComponent<BillboardToCamera>() == null)
                m_CrosshairTarget.AddComponent<BillboardToCamera>();

            Debug.Log($"[TrajectoryTaskManager] Crosshair placed at world {m_CrosshairTarget.transform.position}.");
        }

        void PositionCrosshair(Transform t)
        {
            if (m_SpawnOrigin == null || m_TaskAsset == null || t == null) return;
            Vector3 forward = m_SpawnOrigin.forward.sqrMagnitude > 0.0001f ? m_SpawnOrigin.forward.normalized : Vector3.forward;
            Vector3 pos = m_SpawnOrigin.position + forward * m_TaskAsset.SpawnDistance + Vector3.up * m_CrosshairHeightOffset;
            t.SetParent(m_SpawnOrigin, worldPositionStays: false);
            t.position = pos;
        }

        GameObject BuildDefaultCrosshair()
        {
            var root = new GameObject("DefaultCrosshair");
            root.transform.SetParent(m_SpawnOrigin, worldPositionStays: false);

            // White unlit cross built from two thin Quads.
            var horizontal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            horizontal.name = "Horizontal";
            DestroyImmediate(horizontal.GetComponent<Collider>());
            horizontal.transform.SetParent(root.transform, false);
            horizontal.transform.localScale = new Vector3(0.20f, 0.02f, 1f);

            var vertical = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vertical.name = "Vertical";
            DestroyImmediate(vertical.GetComponent<Collider>());
            vertical.transform.SetParent(root.transform, false);
            vertical.transform.localScale = new Vector3(0.02f, 0.20f, 1f);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                var mat = new Material(unlit) { color = Color.white };
                horizontal.GetComponent<Renderer>().sharedMaterial = mat;
                vertical.GetComponent<Renderer>().sharedMaterial = mat;
            }

            return root;
        }

        float NextItiSeconds()
        {
            float min = m_TaskAsset != null ? m_TaskAsset.ItiMinSeconds : 1.5f;
            float max = m_TaskAsset != null ? m_TaskAsset.ItiMaxSeconds : 2.5f;
            if (max <= min) return min;
            return UnityEngine.Random.Range(min, max);
        }

        void SpawnTrial(TrialDefinition trial)
        {
            var rt = new RuntimeTrial(trial) { SpawnTime = Time.time };

            // Capture the actual world spawn / end positions so they end up
            // in the trial CSV row (start_x..end_z columns).
            if (m_SpawnOrigin != null)
            {
                Vector3 forward = m_SpawnOrigin.forward.sqrMagnitude > 0.0001f
                    ? m_SpawnOrigin.forward.normalized
                    : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

                trial.spawnWorldPosition = m_SpawnOrigin.position + forward * trial.spawnDistance;
                trial.endWorldPosition   = m_SpawnOrigin.position + right   * trial.finalLateralOffset;
                rt.Definition = trial;
            }

            // ITI = how long since the previous trial's spawn ended. Approximated
            // here as (now - lastSpawnEnd); for the first trial of a block it's 0.
            float itiMs = m_LastSpawnEndTime > 0f
                ? Mathf.Max(0f, (Time.time - m_LastSpawnEndTime) * 1000f)
                : 0f;
            rt.InterTrialIntervalMs = itiMs;

            if (m_LoomingObjectPrefab != null && m_SpawnOrigin != null)
            {
                var go = Instantiate(m_LoomingObjectPrefab, m_SpawnOrigin.position, Quaternion.identity);
                var controller = go.GetComponent<TrajectoryObjectController>();
                if (controller == null)
                    controller = go.AddComponent<TrajectoryObjectController>();

                controller.Initialize(trial, m_SpawnOrigin.position, m_SpawnOrigin.forward);
                controller.Activate(Time.time);
                rt.ObjectController = controller;
                rt.BallMotionStartTime = Time.timeAsDouble;
            }

            m_ActiveTrials.Add(rt);
            m_LastSpawnEndTime = Time.time + trial.Duration + m_ResponseGracePeriod;

            m_MarkerEmitter?.Emit("trial_spawn", trial.trialId,
                trial.category.ToString(), trial.expectedResponse.ToString());
            TrialSpawned?.Invoke(trial.trialId, trial);
        }

        void OnResponseReceived(ResponseEvent response)
        {
            if (!m_Running || m_Paused) return;

            // Drop duplicate events for the same command inside the dedup
            // window (see k_DedupWindowSeconds). Done BEFORE marker emission
            // so the EEG stream isn't doubled either.
            if (response.command == m_LastResponseCommand
                && Time.time - m_LastResponseTime < k_DedupWindowSeconds)
                return;
            m_LastResponseTime = Time.time;
            m_LastResponseCommand = response.command;

            m_MarkerEmitter?.Emit(
                response.command == SemanticCommand.Hit ? "response_hit" : "response_miss",
                "", "", "", response.rawSource);

            // Find the most recent unresolved trial within its full response window
            // (travel time + grace period after vanish)
            RuntimeTrial bestTrial = null;
            float bestSpawnTime = float.MinValue;

            foreach (var trial in m_ActiveTrials)
            {
                if (trial.Resolved) continue;

                float trialElapsed = Time.time - trial.SpawnTime;
                float deadline = trial.Definition.Duration + m_ResponseGracePeriod;

                // Accept response during travel AND during grace period after vanish
                if (trialElapsed < 0f || trialElapsed > deadline)
                    continue;

                // Prefer the most recently spawned trial
                if (trial.SpawnTime > bestSpawnTime)
                {
                    bestSpawnTime = trial.SpawnTime;
                    bestTrial = trial;
                }
            }

            if (bestTrial == null)
            {
                Debug.LogWarning($"[Response] {response.command} pressed — no active trial to match.");
                ResponseIndicator?.Invoke(response.command, false);
                return;
            }

            bool correct = response.command == bestTrial.Definition.expectedResponse;
            TrialResult result = correct ? TrialResult.Correct : TrialResult.Incorrect;
            float rt = (Time.time - bestTrial.SpawnTime) * 1000f;

            Debug.Log($"[Response] {response.command} pressed | Trial {bestTrial.Definition.trialId} " +
                      $"({bestTrial.Definition.category}) | Expected: {bestTrial.Definition.expectedResponse} | " +
                      $"Result: {(correct ? "CORRECT" : "WRONG")} | RT: {rt:F0}ms");

            ResolveTrial(bestTrial, response.command, result, "");
            ResponseIndicator?.Invoke(response.command, true);
        }

        void ResolveTrial(RuntimeTrial trial, SemanticCommand received, TrialResult result, string failureReason)
        {
            if (trial.Resolved) return;
            trial.Resolved = true;
            TrialsCompletedInBlock++;

            bool noResponse = result == TrialResult.NoResponse;
            double responseTime = noResponse ? double.NaN : Time.timeAsDouble;
            double reactionMs = noResponse
                ? double.NaN
                : (Time.timeAsDouble - trial.SpawnTime) * 1000.0;

            float speed = trial.Definition.speed;
            float prev  = trial.Definition.prevSpeed;
            float speedChange = speed - prev;
            SpeedChangeDirection dir;
            if (Mathf.Approximately(prev, 0f) || Mathf.Approximately(speedChange, 0f))
                dir = SpeedChangeDirection.None;
            else if (speedChange > 0f)
                dir = SpeedChangeDirection.Increase;
            else
                dir = SpeedChangeDirection.Decrease;

            var judgement = new TrialJudgement
            {
                trialId = trial.Definition.trialId,
                blockIndex = m_CurrentBlock,
                category = trial.Definition.category,
                expected = trial.Definition.expectedResponse,
                received = received,
                result = result,
                isCorrect = result == TrialResult.Correct,

                trialNumberInBlock = trial.Definition.trialIndexInBlock + 1,
                runId = trial.Definition.runId,
                trialInRun = trial.Definition.trialInRun,
                runLength = trial.Definition.runLength,
                trialsSinceLastSwitch = trial.Definition.trialsSinceLastSwitch,
                isSwitchTrial = trial.Definition.isSwitchTrial,
                speedMps = speed,
                prevSpeedMps = prev,
                speedChange = speedChange,
                absSpeedChange = Mathf.Abs(speedChange),
                changeDirection = dir,

                trajectoryId = trial.Definition.trajectoryId,
                trajectoryAngleDeg = trial.Definition.trajectoryAngleDeg,
                startWorldPosition = trial.Definition.spawnWorldPosition,
                endWorldPosition = trial.Definition.endWorldPosition,
                lateralOffsetMeters = trial.Definition.finalLateralOffset,

                trialStartTime = trial.SpawnTime,
                ballMotionStartTime = trial.BallMotionStartTime,
                stimulusOnsetTime = trial.SpawnTime,
                responseTime = responseTime,
                reactionTimeMs = reactionMs,
                interTrialIntervalMs = trial.InterTrialIntervalMs,

                failureReason = failureReason,
            };

            m_AllResults.Add(judgement);

            string markerCode = judgement.isCorrect ? "trial_resolved_correct" : "trial_resolved_incorrect";
            if (result == TrialResult.NoResponse) markerCode = "trial_no_response";
            m_MarkerEmitter?.Emit(markerCode, trial.Definition.trialId,
                trial.Definition.category.ToString(),
                trial.Definition.expectedResponse.ToString(),
                received.ToString());

            // No feedback to participant - this is intentional per protocol
            TrialJudged?.Invoke(judgement);
        }

        void DespawnAll()
        {
            foreach (var trial in m_ActiveTrials)
            {
                if (trial.ObjectController != null)
                    trial.ObjectController.Despawn();
            }
            m_ActiveTrials.Clear();
        }

        void OnDestroy()
        {
            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;
        }

        class RuntimeTrial
        {
            public TrialDefinition Definition;
            public float SpawnTime;
            public double BallMotionStartTime = double.NaN;
            public float InterTrialIntervalMs;
            public bool Resolved;
            public TrajectoryObjectController ObjectController;

            public RuntimeTrial(TrialDefinition def)
            {
                Definition = def;
            }
        }
    }
}
