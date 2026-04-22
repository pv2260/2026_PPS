using System;
using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField] Transform m_SpawnOrigin;

        [Header("Response")]
        [Tooltip("Extra seconds after ball vanishes during which the participant can still respond")]
        [SerializeField] float m_ResponseGracePeriod = 1.5f;

        [Header("Timing")]
        [Tooltip("Gap in seconds between the end of one trial trajectory and the next spawn")]
        [SerializeField] float m_InterTrialGap = 1.0f;

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
        float m_NextSpawnTime;
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
            m_NextSpawnTime = 0f;
            m_ActiveTrials.Clear();

            m_Running = true;
            m_InputSource?.Enable();

            m_MarkerEmitter?.Emit("block_start", "", blockIndex.ToString());
            BlockStarted?.Invoke(blockIndex);

            Debug.Log($"[TrajectoryTaskManager] Block {blockIndex + 1} started with {m_BlockTrials.Length} trials.");
        }

        public void StopBlock()
        {
            m_Running = false;
            m_InputSource?.Disable();

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

            float elapsed = Time.time - m_BlockStartTime;

            // Spawn next trial only after the previous one has had time to finish
            if (m_NextTrialIndex < m_BlockTrials.Length)
            {
                if (elapsed >= m_NextSpawnTime)
                {
                    var trial = m_BlockTrials[m_NextTrialIndex];
                    SpawnTrial(trial);
                    m_NextTrialIndex++;

                    // Schedule next spawn after this trial's travel duration plus a small gap
                    m_NextSpawnTime += trial.Duration + m_InterTrialGap;
                }
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
                m_MarkerEmitter?.Emit("block_end", "", m_CurrentBlock.ToString());
                BlockEnded?.Invoke(m_CurrentBlock);
            }
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
                speedCondition = trial.Definition.speedCondition,
                speedMetersPerSecond = trial.Definition.speed,
                expected = trial.Definition.expectedResponse,
                received = received,
                result = result,
                isCorrect = result == TrialResult.Correct,
                stimulusOnsetTime = trial.SpawnTime,
                responseTime = Time.timeAsDouble,
                reactionTimeMs = reactionMs,
                lateralOffsetMeters = trial.Definition.finalLateralOffset,
                approachAngleDeg = trial.Definition.approachAngleDeg,
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