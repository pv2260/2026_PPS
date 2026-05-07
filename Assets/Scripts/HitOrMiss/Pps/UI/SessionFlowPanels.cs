using System.Collections;
using UnityEngine;
using TMPro;

namespace HitOrMiss.Pps
{
    public class SessionFlowPanels : MonoBehaviour
    {
        [Header("Task 1 Flow Panels")]
        [SerializeField] private GameObject m_WelcomePanel;
        [SerializeField] private GameObject m_InstructionsPanel;
        [SerializeField] private GameObject m_PositioningPanel;
        [SerializeField] private GameObject m_PracticeIntroVTOnlyPanel;
        [SerializeField] private GameObject m_PracticeIntroVTVisualPanel;
        [SerializeField] private GameObject m_NoFeedbackPanel;
        [SerializeField] private GameObject m_ReadyToStartPanel;
        [SerializeField] private GameObject m_BlockIntroPanel;
        [SerializeField] private GameObject m_RestPanel;
        [SerializeField] private GameObject m_PausePanel;
        [SerializeField] private GameObject m_OutroPanel;

        [Header("Feedback")]
        [SerializeField] private TMP_Text m_FeedbackText;
        [SerializeField] private float m_FeedbackDurationSeconds = 0.75f;

        [Header("Optional Dynamic Text")]
        [SerializeField] private TMP_Text m_WelcomeText;
        [SerializeField] private TMP_Text m_InstructionsText;
        [SerializeField] private TMP_Text m_PositioningText;
        [SerializeField] private TMP_Text m_PracticeIntroVTOnlyText;
        [SerializeField] private TMP_Text m_PracticeIntroVTVisualText;
        [SerializeField] private TMP_Text m_NoFeedbackText;
        [SerializeField] private TMP_Text m_ReadyToStartText;
        [SerializeField] private TMP_Text m_BlockIntroText;
        [SerializeField] private TMP_Text m_RestText;
        [SerializeField] private TMP_Text m_PauseText;
        [SerializeField] private TMP_Text m_OutroText;

        private bool m_WaitingForContinue;
        private Coroutine m_FeedbackRoutine;

        private void Awake()
        {
            HideAll();
        }

        public void HideAll()
        {
            SetActive(m_WelcomePanel, false);
            SetActive(m_InstructionsPanel, false);
            SetActive(m_PositioningPanel, false);
            SetActive(m_PracticeIntroVTOnlyPanel, false);
            SetActive(m_PracticeIntroVTVisualPanel, false);
            SetActive(m_NoFeedbackPanel, false);
            SetActive(m_ReadyToStartPanel, false);
            SetActive(m_BlockIntroPanel, false);
            SetActive(m_RestPanel, false);
            SetActive(m_PausePanel, false);
            SetActive(m_OutroPanel, false);
        }

        public IEnumerator ShowWelcomeAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_WelcomePanel,
                m_WelcomeText,
                text ?? "Welcome to Task 1"
            );
        }

        public IEnumerator ShowInstructionsAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_InstructionsPanel,
                m_InstructionsText,
                text ??
                "Lights may appear.\n\n" +
                "Vibration may occur.\n\n" +
                "Respond ONLY when you feel vibration.\n\n" +
                "Press as quickly as possible."
            );
        }

        public IEnumerator ShowPositioningAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_PositioningPanel,
                m_PositioningText,
                text ??
                "Please stand in front of the cross.\n\n" +
                "Face forward and hold the controller comfortably."
            );
        }

        public IEnumerator ShowPracticeIntroVTOnlyAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_PracticeIntroVTOnlyPanel,
                m_PracticeIntroVTOnlyText,
                text ??
                "You will first experience vibration only.\n\n" +
                "Press the controller whenever you feel a vibration."
            );
        }

        public IEnumerator ShowPracticeIntroVTVisualAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_PracticeIntroVTVisualPanel,
                m_PracticeIntroVTVisualText,
                text ??
                "You will also see lights moving toward you.\n\n" +
                "Continue responding ONLY when you feel a vibration."
            );
        }

        public IEnumerator ShowNoFeedbackAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_NoFeedbackPanel,
                m_NoFeedbackText,
                text ??
                "Practice is now complete.\n\n" +
                "The real task will now begin.\n\n" +
                "You will no longer receive feedback."
            );
        }

        public IEnumerator ShowReadyToStartAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_ReadyToStartPanel,
                m_ReadyToStartText,
                text ??
                "Are you ready to begin?\n\n" +
                "Press Start when you are ready."
            );
        }

        public IEnumerator ShowBlockIntroAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_BlockIntroPanel,
                m_BlockIntroText,
                text ?? "Block starting.\n\nPress Begin when ready."
            );
        }

        public IEnumerator ShowPauseAndWait(string text = null)
        {
            yield return ShowAndWait(
                m_PausePanel,
                m_PauseText,
                text ??
                "Task paused.\n\n" +
                "The current trial has been interrupted."
            );
        }

        public IEnumerator ShowRestAndAutoAdvance(float seconds)
        {
            HideAll();

            if (m_RestText != null)
                m_RestText.text = $"Break\n\nPlease rest.\n\nContinuing in {seconds:0} seconds.";

            SetActive(m_RestPanel, true);
            yield return new WaitForSeconds(seconds);
            HideAll();
        }

        public IEnumerator ShowOutro(string text = null, float holdSeconds = 5f)
        {
            HideAll();

            if (m_OutroText != null)
                m_OutroText.text = text ?? "Task 1 complete.\n\nThank you.";

            SetActive(m_OutroPanel, true);
            yield return new WaitForSeconds(holdSeconds);
        }

        public IEnumerator ShowAndWait(GameObject panel, TMP_Text textSlot, string text)
        {
            HideAll();

            if (panel == null)
            {
                Debug.LogWarning("[SessionFlowPanels] Missing panel reference.");
                yield break;
            }

            if (textSlot != null)
                textSlot.text = text;

            panel.SetActive(true);
            m_WaitingForContinue = true;

            while (m_WaitingForContinue)
                yield return null;

            HideAll();
        }

        public void OnContinue()
        {
            m_WaitingForContinue = false;
        }

        public void ShowFeedback(string message)
        {
            if (m_FeedbackText == null)
                return;

            if (m_FeedbackRoutine != null)
                StopCoroutine(m_FeedbackRoutine);

            m_FeedbackRoutine = StartCoroutine(FeedbackRoutine(message));
        }

        private IEnumerator FeedbackRoutine(string message)
        {
            m_FeedbackText.text = message;
            m_FeedbackText.gameObject.SetActive(true);

            yield return new WaitForSeconds(m_FeedbackDurationSeconds);

            m_FeedbackText.gameObject.SetActive(false);
            m_FeedbackRoutine = null;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null)
                go.SetActive(active);
        }
    }
}