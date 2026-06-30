using System;
using System.Collections;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CybersicknessTaskManager : MonoBehaviour
    {
        [Header("Task")]
        [SerializeField] CyberBugTaskAsset m_TaskAsset;
        [SerializeField] CyberFishController m_FishController;
        [SerializeField] CyberBugPredictionPanel m_PredictionPanel;

        [Header("Optional EEG")]
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;

        [Header("Testing")]
        [SerializeField] bool m_AllowKeyboardTesting = true;

        bool m_Running;
        bool m_WaitingForResponse;

        CyberYesNoResponse m_CurrentResponse;
        double m_ResponseTime;

        Coroutine m_BlockCoroutine;

        public bool IsRunning => m_Running;

        public event Action<CyberBugTrialResult> TrialCompleted;
        public event Action<int> BlockStarted;
        public event Action<int> BlockEnded;

        void Awake()
        {
            if (m_PredictionPanel != null)
                m_PredictionPanel.Hide();
        }

        void Update()
        {
            if (!m_AllowKeyboardTesting) return;

            if (m_WaitingForResponse)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Y))
                    PressYes();

                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.N))
                    PressNo();
            }
        }

        public void SetTaskAsset(CyberBugTaskAsset asset)
        {
            m_TaskAsset = asset;
        }

        public void StartBlock(int blockIndex)
        {
            if (m_TaskAsset == null)
            {
                Debug.LogError("[CybersicknessTaskManager] No CyberBugTaskAsset assigned.");
                return;
            }

            CyberBugTrialDefinition[] trials = m_TaskAsset.GenerateBlock(blockIndex);
            StartTrialList(blockIndex, trials);
        }

        public void StartTrialList(int blockIndex, CyberBugTrialDefinition[] trials)
        {
            if (trials == null || trials.Length == 0)
            {
                Debug.LogWarning("[CybersicknessTaskManager] Empty cyber bug trial list.");
                return;
            }

            if (m_BlockCoroutine != null)
                StopCoroutine(m_BlockCoroutine);

            m_BlockCoroutine = StartCoroutine(RunBlock(blockIndex, trials));
        }

        IEnumerator RunBlock(int blockIndex, CyberBugTrialDefinition[] trials)
        {
            m_Running = true;

            m_EegMarkerEmitter?.Emit("cyberbug_block_start", extra: (blockIndex + 1).ToString());
            BlockStarted?.Invoke(blockIndex);

            for (int i = 0; i < trials.Length; i++)
            {
                yield return RunTrial(trials[i]);

                if (m_TaskAsset != null && m_TaskAsset.ItiSeconds > 0f)
                    yield return new WaitForSeconds(m_TaskAsset.ItiSeconds);
            }

            m_Running = false;

            m_EegMarkerEmitter?.Emit("cyberbug_block_end", extra: (blockIndex + 1).ToString());
            BlockEnded?.Invoke(blockIndex);
        }

        IEnumerator RunTrial(CyberBugTrialDefinition trial)
        {
            m_CurrentResponse = CyberYesNoResponse.None;
            m_ResponseTime = double.NaN;
            m_WaitingForResponse = true;

            double trialStartTime = Time.timeAsDouble;

            m_EegMarkerEmitter?.Emit(
                "cyberbug_trial_start",
                trial.trialId,
                trial.condition.ToString());

            if (m_PredictionPanel != null)
                m_PredictionPanel.ShowQuestion("Will the bug touch you?");

            if (m_FishController != null)
                m_FishController.StartFish(trial);
            else
                Debug.LogError("[CybersicknessTaskManager] No CyberFishController assigned.");

            while (m_FishController != null &&
                !m_FishController.OutcomeReached &&
                m_CurrentResponse == CyberYesNoResponse.None)
            {
                yield return null;
            }
            m_WaitingForResponse = false;

            if (m_CurrentResponse == CyberYesNoResponse.None)
            {
                m_EegMarkerEmitter?.Emit("cyberbug_no_response", trial.trialId);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (m_PredictionPanel != null)
                m_PredictionPanel.Hide();

            while (m_FishController != null && !m_FishController.Finished)
                yield return null;

            double outcomeTime = m_FishController != null
                ? m_FishController.OutcomeTime
                : Time.timeAsDouble;


            bool responded = m_CurrentResponse != CyberYesNoResponse.None;
            bool correct = responded && m_CurrentResponse == trial.expectedResponse;

            double reactionMs = responded
                ? (m_ResponseTime - trialStartTime) * 1000.0
                : double.NaN;

            var result = new CyberBugTrialResult
            {
                participantId = "",

                trialId = trial.trialId,
                blockIndex = trial.blockIndex,
                trialIndex = trial.trialIndex,

                condition = trial.condition,

                startDistance = trial.startDistance,
                speed = trial.speed,
                lateralOffset = trial.lateralOffset,

                willTouch = trial.willTouch,
                willSplash = trial.willSplash,

                expectedResponse = trial.expectedResponse,
                participantResponse = m_CurrentResponse,

                responded = responded,
                correct = correct,

                trialStartTime = trialStartTime,
                responseTime = m_ResponseTime,
                outcomeTime = outcomeTime,

                reactionTimeMs = reactionMs
            };

            m_EegMarkerEmitter?.Emit(
                "cyberbug_trial_end",
                trial.trialId,
                trial.condition.ToString(),
                extra: correct ? "correct" : "incorrect");

            TrialCompleted?.Invoke(result);
        }

        public void PressYes()
        {
            if (!m_WaitingForResponse) return;
            if (m_CurrentResponse != CyberYesNoResponse.None) return;

            m_CurrentResponse = CyberYesNoResponse.Yes;
            m_ResponseTime = Time.timeAsDouble;

            if (m_PredictionPanel != null)
                m_PredictionPanel.ShowResponseTextOnly("YES");

            m_EegMarkerEmitter?.Emit("cyberbug_response_yes");
        }

        public void PressNo()
        {
            if (!m_WaitingForResponse) return;
            if (m_CurrentResponse != CyberYesNoResponse.None) return;

            m_CurrentResponse = CyberYesNoResponse.No;
            m_ResponseTime = Time.timeAsDouble;

            if (m_PredictionPanel != null)
                m_PredictionPanel.ShowResponseTextOnly("NO");

            m_EegMarkerEmitter?.Emit("cyberbug_response_no");
        }

        public void StopBlock()
        {
            if (m_BlockCoroutine != null)
            {
                StopCoroutine(m_BlockCoroutine);
                m_BlockCoroutine = null;
            }

            if (m_FishController != null)
                m_FishController.StopAndClear();

            if (m_PredictionPanel != null)
                m_PredictionPanel.Hide();

            m_WaitingForResponse = false;
            m_Running = false;

            m_EegMarkerEmitter?.Emit("cyberbug_block_stopped");
        }
    }
}