using System.Collections;
using UnityEngine;
using TMPro;

namespace HitOrMiss.Pps
{
    public class SessionFlowPanels : MonoBehaviour
    {
        [Header("Task 1 Panels")]
        [SerializeField] GameObject m_WelcomePanel;
        [SerializeField] GameObject m_TriggerCheckPanel;
        [SerializeField] GameObject m_InstructionsPanel;
        [SerializeField] GameObject m_PositioningPanel;
        [SerializeField] GameObject m_PracticeIntroVTOnlyPanel;
        [SerializeField] GameObject m_PracticeIntroVTVisualPanel;
        [SerializeField] GameObject m_PracticeFeedbackPanel;
        [SerializeField] GameObject m_NoFeedbackPanel;
        [SerializeField] GameObject m_ReadyToStartPanel;
        [SerializeField] GameObject m_BlockCounterPanel;
        [SerializeField] GameObject m_BreakPanel;
        [SerializeField] GameObject m_PausePanel;
        [SerializeField] GameObject m_EndPanel;

        [Header("Optional Dynamic Text")]
        [SerializeField] TMP_Text m_TriggerCheckText;
        [SerializeField] TMP_Text m_BlockCounterText;
        [SerializeField] TMP_Text m_BreakText;
        [SerializeField] TMP_Text m_PracticeFeedbackText;
        [SerializeField] TMP_Text m_EndText;

        [Header("Feedback Timing")]
        [SerializeField] float m_PracticeFeedbackSeconds = 1f;

        bool m_WaitingForContinue;

        void Awake()
        {
            HideAll();
        }

        public void HideAll()
        {
            SetActive(m_WelcomePanel, false);
            SetActive(m_TriggerCheckPanel, false);
            SetActive(m_InstructionsPanel, false);
            SetActive(m_PositioningPanel, false);
            SetActive(m_PracticeIntroVTOnlyPanel, false);
            SetActive(m_PracticeIntroVTVisualPanel, false);
            SetActive(m_PracticeFeedbackPanel, false);
            SetActive(m_NoFeedbackPanel, false);
            SetActive(m_ReadyToStartPanel, false);
            SetActive(m_BlockCounterPanel, false);
            SetActive(m_BreakPanel, false);
            SetActive(m_PausePanel, false);
            SetActive(m_EndPanel, false);
        }

        public IEnumerator ShowWelcomeAndWait()
            => ShowAndWait(m_WelcomePanel);

        public IEnumerator ShowTriggerCheckAndWait(string text = null)
        {
            if (m_TriggerCheckText != null && text != null)
                m_TriggerCheckText.text = text;

            yield return ShowAndWait(m_TriggerCheckPanel);
        }

        public IEnumerator ShowInstructionsAndWait()
            => ShowAndWait(m_InstructionsPanel);

        public IEnumerator ShowPositioningAndWait()
            => ShowAndWait(m_PositioningPanel);

        public IEnumerator ShowPracticeIntroVTOnlyAndWait()
            => ShowAndWait(m_PracticeIntroVTOnlyPanel);

        public IEnumerator ShowPracticeIntroVTVisualAndWait()
            => ShowAndWait(m_PracticeIntroVTVisualPanel);

        public IEnumerator ShowNoFeedbackAndWait()
            => ShowAndWait(m_NoFeedbackPanel);

        public IEnumerator ShowReadyToStartAndWait()
            => ShowAndWait(m_ReadyToStartPanel);

        public IEnumerator ShowBlockCounterAndWait(int blockIndex, int totalBlocks)
        {
            if (m_BlockCounterText != null)
                m_BlockCounterText.text =
                    $"Block {blockIndex + 1} / {totalBlocks}\n\nPress Begin when you are ready.";

            yield return ShowAndWait(m_BlockCounterPanel);
        }

        public IEnumerator ShowBreakAndWait(float seconds)
        {
            HideAll();
            SetActive(m_BreakPanel, true);

            float remaining = seconds;

            while (remaining > 0f && m_WaitingForContinue == false)
            {
                if (m_BreakText != null)
                    m_BreakText.text =
                        $"Break\n\n{Mathf.CeilToInt(remaining)} seconds remaining.\n\nPress Continue when ready.";

                remaining -= Time.deltaTime;
                yield return null;
            }

            SetActive(m_BreakPanel, false);
        }

        public IEnumerator ShowPauseAndWait()
            => ShowAndWait(m_PausePanel);

        public IEnumerator ShowEndAndWait(string text = null)
        {
            if (m_EndText != null && text != null)
                m_EndText.text = text;

            yield return ShowAndWait(m_EndPanel);
        }

        public IEnumerator ShowPracticeFeedback(string message)
        {
            HideAll();

            if (m_PracticeFeedbackText != null)
                m_PracticeFeedbackText.text = message;

            SetActive(m_PracticeFeedbackPanel, true);
            yield return new WaitForSeconds(m_PracticeFeedbackSeconds);
            SetActive(m_PracticeFeedbackPanel, false);
        }

        IEnumerator ShowAndWait(GameObject panel)
        {
            HideAll();

            if (panel == null)
            {
                Debug.LogWarning("[SessionFlowPanels] Missing panel reference.");
                yield break;
            }

            panel.SetActive(true);
            m_WaitingForContinue = true;

            while (m_WaitingForContinue)
                yield return null;

            panel.SetActive(false);
        }

        public void OnContinue()
        {
            m_WaitingForContinue = false;
        }

        public void OnStop()
        {
            m_WaitingForContinue = false;
            HideAll();
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null)
                go.SetActive(on);
        }
    }
}