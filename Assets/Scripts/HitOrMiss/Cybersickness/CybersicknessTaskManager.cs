using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Cybersickness
{
    public class CybersicknessTaskManager : MonoBehaviour
    {
        [Header("Task")]
        [SerializeField] CyberBugTaskAsset m_TaskAsset;
        [SerializeField] CyberFishController m_FishController;

        // Keep this if other code still references it, but we will not use it.
        [SerializeField] CyberBugPredictionPanel m_PredictionPanel;

        [Header("Response Feedback Text")]
        [SerializeField] TMP_Text m_ResponseFeedbackText;
        [SerializeField] float m_ResponseFeedbackVisibleSeconds = 0.75f;

        [Header("Feedback Timing")]
        [SerializeField] float m_ResponsePopupSeconds = 0.45f;
        [SerializeField] float m_CorrectnessDelaySeconds = 0.15f;
        [SerializeField] float m_PostFeedbackPauseSeconds = 0.6f;

        [Header("Correctness Flash")]
        [SerializeField] Image m_CorrectnessFlashImage;
        [SerializeField] Color m_CorrectFlashColor = new Color(0f, 1f, 0f, 0.35f);
        [SerializeField] Color m_IncorrectFlashColor = new Color(1f, 0f, 0f, 0.35f);
        [SerializeField] float m_FlashSeconds = 0.25f;

        [Header("Optional EEG")]
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;

        [Header("Testing")]
        [SerializeField] bool m_AllowKeyboardTesting = true;

        bool m_Running;
        bool m_WaitingForResponse;

        CyberYesNoResponse m_CurrentResponse;
        double m_ResponseTime;

        Coroutine m_BlockCoroutine;
        Coroutine m_ResponseFeedbackCoroutine;
        Coroutine m_CorrectnessFlashCoroutine;
        CyberYesNoResponse m_CurrentExpectedResponse;

        public bool IsRunning => m_Running;

        public event Action<CyberBugTrialResult> TrialCompleted;
        public event Action<int> BlockStarted;
        public event Action<int> BlockEnded;

        void Awake()
        {
            if (m_PredictionPanel != null)
                m_PredictionPanel.Hide();

            HideResponseFeedbackText();
            HideCorrectnessFlash();
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
            m_CurrentExpectedResponse = trial.expectedResponse;

            double trialStartTime = Time.timeAsDouble;

            m_EegMarkerEmitter?.Emit(
                "cyberbug_trial_start",
                trial.trialId,
                trial.condition.ToString());

            //if (m_PredictionPanel != null)
            //    m_PredictionPanel.ShowQuestion("Will the bug touch you?");

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

            HideResponseFeedbackText();
            HideCorrectnessFlash();

            //if (m_PredictionPanel != null)
             //   m_PredictionPanel.Hide();

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

            ShowResponseFeedbackText("YES");
            ShowCorrectnessFlashDelayed(m_CurrentResponse == m_CurrentExpectedResponse);

            m_EegMarkerEmitter?.Emit("cyberbug_response_yes");
        }

        public void PressNo()
        {
            if (!m_WaitingForResponse) return;
            if (m_CurrentResponse != CyberYesNoResponse.None) return;

            m_CurrentResponse = CyberYesNoResponse.No;
            m_ResponseTime = Time.timeAsDouble;

            ShowResponseFeedbackText("NO");
            ShowCorrectnessFlashDelayed(m_CurrentResponse == m_CurrentExpectedResponse);

            m_EegMarkerEmitter?.Emit("cyberbug_response_no");
        }

        void ShowResponseFeedbackText(string text)
        {
            if (m_ResponseFeedbackText == null)
            {
                Debug.LogWarning("[CYBER TASK] Response feedback text is not assigned.");
                return;
            }

            if (m_ResponseFeedbackCoroutine != null)
                StopCoroutine(m_ResponseFeedbackCoroutine);

            m_ResponseFeedbackCoroutine = StartCoroutine(ResponsePopupRoutine(text));
        }

        IEnumerator ResponsePopupRoutine(string text)
        {
            m_ResponseFeedbackText.text = text;
            m_ResponseFeedbackText.gameObject.SetActive(true);

            Transform target = m_ResponseFeedbackText.transform;

            Vector3 startScale = Vector3.one * 0.4f;
            Vector3 popScale = Vector3.one * 1.35f;
            Vector3 settleScale = Vector3.one * 1.0f;

            target.localScale = startScale;

            float popInSeconds = 0.16f;
            float settleSeconds = 0.12f;

            float elapsed = 0f;

            while (elapsed < popInSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popInSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                target.localScale = Vector3.Lerp(startScale, popScale, eased);
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < settleSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settleSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                target.localScale = Vector3.Lerp(popScale, settleScale, eased);
                yield return null;
            }

            target.localScale = settleScale;

            yield return new WaitForSeconds(m_ResponsePopupSeconds);

            HideResponseFeedbackText();
            m_ResponseFeedbackCoroutine = null;
        }

        void HideResponseFeedbackText()
        {
            if (m_ResponseFeedbackText == null)
                return;

            m_ResponseFeedbackText.text = "";
            m_ResponseFeedbackText.transform.localScale = Vector3.one;
            m_ResponseFeedbackText.gameObject.SetActive(false);
        }

        void ShowCorrectnessFlashDelayed(bool correct)
        {
            if (m_CorrectnessFlashCoroutine != null)
                StopCoroutine(m_CorrectnessFlashCoroutine);

            m_CorrectnessFlashCoroutine = StartCoroutine(CorrectnessFlashDelayedRoutine(correct));
        }

        IEnumerator CorrectnessFlashDelayedRoutine(bool correct)
        {
            yield return new WaitForSeconds(m_CorrectnessDelaySeconds);

            if (m_CorrectnessFlashImage == null)
            {
                Debug.LogWarning("[CYBER TASK] Correctness flash image is not assigned.");
                yield break;
            }

            Color color = correct ? m_CorrectFlashColor : m_IncorrectFlashColor;

            m_CorrectnessFlashImage.gameObject.SetActive(true);
            m_CorrectnessFlashImage.color = color;

            yield return new WaitForSeconds(m_FlashSeconds);

            HideCorrectnessFlash();
            m_CorrectnessFlashCoroutine = null;
        }

        void HideCorrectnessFlash()
        {
            if (m_CorrectnessFlashImage == null)
                return;

            Color color = m_CorrectnessFlashImage.color;
            color.a = 0f;
            m_CorrectnessFlashImage.color = color;
            m_CorrectnessFlashImage.gameObject.SetActive(false);
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