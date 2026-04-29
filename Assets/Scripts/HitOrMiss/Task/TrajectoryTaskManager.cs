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
        IResponseInputSource m_InputSource;
        EegMarkerEmitter m_MarkerEmitter;

        public TrajectoryTaskAsset TaskAsset
        {
            get => m_TaskAsset;
            set => m_TaskAsset = value;
        }

        public IReadOnlyList<TrialJudgement> Results => m_AllResults;
        public bool IsRunning => m_Running;
        public int CurrentBlock => m_CurrentBlock;
        public int TrialsCompletedInBlock { get; private set; }
        public int TotalTrialsInBlock => m_BlockTrials != null ? m_BlockTrials.Length : 0;

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

            m_CurrentBlock = blockIndex;
            m_BlockTrials = m_TaskAsset.GenerateBlock(blockIndex);
            m_NextTrialIndex = 0;
            TrialsCompletedInBlock = 0;
            m_BlockStartTime = Time.time;
            m_NextSpawnEarliest = Time.time; // first trial spawns next frame
            m_ActiveTrials.Clear();

            m_Running = true;
            m_InputSource?.Enable();
            EnsureCrosshair();
            SetCrosshairActive(true);

            m_MarkerEmitter?.Emit("block_start", "", blockIndex.ToString());
            BlockStarted?.Invoke(blockIndex);

            Debug.Log($"[TrajectoryTaskManager] Block {blockIndex + 1} started with {m_BlockTrials.Length} trials.");
        }

        public void StopBlock()
        {
            m_Running = false;
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

        void Update()
        {
            if (!m_Running) return;

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

            if (m_LoomingObjectPrefab != null && m_SpawnOrigin != null)
            {
                var go = Instantiate(m_LoomingObjectPrefab, m_SpawnOrigin.position, Quaternion.identity);
                var controller = go.GetComponent<TrajectoryObjectController>();
                if (controller == null)
                    controller = go.AddComponent<TrajectoryObjectController>();

                controller.Initialize(trial, m_SpawnOrigin.position, m_SpawnOrigin.forward);
                controller.Activate(Time.time);
                rt.ObjectController = controller;
            }

            m_ActiveTrials.Add(rt);

            m_MarkerEmitter?.Emit("trial_spawn", trial.trialId,
                trial.category.ToString(), trial.expectedResponse.ToString());
            TrialSpawned?.Invoke(trial.trialId, trial);
        }

        void OnResponseReceived(ResponseEvent response)
        {
            if (!m_Running) return;

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

            double reactionMs = result == TrialResult.NoResponse
                ? -1
                : (Time.timeAsDouble - trial.SpawnTime) * 1000.0;

            var judgement = new TrialJudgement
            {
                trialId = trial.Definition.trialId,
                blockIndex = m_CurrentBlock,
                category = trial.Definition.category,
                expected = trial.Definition.expectedResponse,
                received = received,
                result = result,
                isCorrect = result == TrialResult.Correct,
                stimulusOnsetTime = trial.SpawnTime,
                responseTime = Time.timeAsDouble,
                reactionTimeMs = reactionMs,
                lateralOffsetMeters = trial.Definition.finalLateralOffset,
                speedMps = trial.Definition.speed,
                failureReason = failureReason
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
            public bool Resolved;
            public TrajectoryObjectController ObjectController;

            public RuntimeTrial(TrialDefinition def)
            {
                Definition = def;
            }
        }
    }
}
