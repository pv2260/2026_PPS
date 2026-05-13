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

        // Used for the crosshair element: we want them to be oriented towards a fixation cross.
        [Header("AR Guidance")]
        [SerializeField] private GameObject m_StandingCross;

        [Header("Feedback Timing")]
        [SerializeField] float m_PracticeFeedbackSeconds = 1f;

        // True while a panel is waiting for the participant to press Continue.
        bool m_WaitingForContinue;

        // Read by PPSAppController to decide whether the experiment should stop.
        public bool StopRequested { get; private set; }

        public enum UiLanguage
        {
            English,
            French
        }

        [System.Serializable]
        public class LocalizedTextEntry
        {
            public TMP_Text textTarget;

            [TextArea(2, 6)]
            public string english;

            [TextArea(2, 6)]
            public string french;
        }

        [Header("Language")]
        [SerializeField] private UiLanguage m_CurrentLanguage = UiLanguage.English;
        [SerializeField] private LocalizedTextEntry[] m_LocalizedTexts;

        private string GetLocalizedText(string key)
        {
            if (string.IsNullOrEmpty(key) || m_TextEntries == null)
                return string.Empty;

            foreach (var entry in m_TextEntries)
            {
                if (entry == null)
                    continue;

                if (entry.key != key)
                    continue;

                return m_CurrentLanguage == UiLanguage.English
                    ? entry.english
                    : entry.french;
            }

            Debug.LogWarning($"[SessionFlowPanels] Missing localization key: {key}");
            return key;
        }


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
        {
            Debug.Log("[UI FLOW] ShowWelcomeAndWait called");

            HideAll();

            if (m_WelcomePanel == null)
            {
                Debug.LogError("[UI FLOW] WelcomePanel reference is NULL.");
                yield break;
            }

            ShowOnly(m_WelcomePanel);

            m_WaitingForContinue = true;

            while (m_WaitingForContinue && !StopRequested)
                yield return null;

            m_WelcomePanel.SetActive(false);
        }

        public IEnumerator ShowTriggerCheckAndWait(string text = null)
        {
            Debug.Log("[UI FLOW] ShowTriggerCheckAndWait called");

            if (m_TriggerCheckText != null && text != null)
                m_TriggerCheckText.text = text;

            yield return ShowAndWait(m_TriggerCheckPanel);
        }

        public IEnumerator ShowInstructionsAndWait()
            => ShowAndWait(m_InstructionsPanel);

        public IEnumerator ShowPositioningAndWait()
            => ShowAndWait(m_PositioningPanel);

        public void ShowStandingCross()
        {
            SetActive(m_StandingCross, true);
        }

        public void HideStandingCross()
        {
            SetActive(m_StandingCross, false);
        }

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
            {
                m_BlockCounterText.text =
                    $"Block {blockIndex + 1} / {totalBlocks}\n\nPress Begin when you are ready.";
            }

            yield return ShowAndWait(m_BlockCounterPanel);
        }

        public IEnumerator ShowPauseAndWait()
            => ShowAndWait(m_PausePanel);

        public IEnumerator ShowEndAndWait(string text = null)
        {
            if (m_EndText != null && text != null)
                m_EndText.text = text;

            // End screen should still be visible even if StopRequested is true.
            yield return ShowAndWait(m_EndPanel, allowStopToClose: false);
        }

        public IEnumerator ShowPracticeFeedback(string message)
        {
            HideAll();

            if (m_PracticeFeedbackText != null)
                m_PracticeFeedbackText.text = message;

            SetActive(m_PracticeFeedbackPanel, true);
            RefreshLanguage();
            float elapsed = 0f;

            while (elapsed < m_PracticeFeedbackSeconds && !StopRequested)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetActive(m_PracticeFeedbackPanel, false);
        }

        public IEnumerator ShowBreakAndWait(float seconds)
        {
            HideAll();

            if (m_BreakPanel == null)
            {
                Debug.LogError("[UI FLOW] BreakPanel reference is NULL.");
                yield break;
            }

            SetActive(m_BreakPanel, true);
            RefreshLanguage();
            m_WaitingForContinue = true;
            float remaining = seconds;

            while (remaining > 0f && m_WaitingForContinue && !StopRequested)
            {
                if (m_BreakText != null)
                {
                    m_BreakText.text =
                        $"Break\n\n{Mathf.CeilToInt(remaining)} seconds remaining.\n\nPress Continue when ready.";
                }

                remaining -= Time.deltaTime;
                yield return null;
            }

            SetActive(m_BreakPanel, false);
            m_WaitingForContinue = false;
        }

        public IEnumerator ShowAndWait(GameObject panel)
        {
            yield return ShowAndWait(panel, allowStopToClose: true);
        }



        private IEnumerator ShowAndWait(GameObject panel, bool allowStopToClose)
        {
            HideAll();

            if (panel == null)
            {
                Debug.LogError("[UI FLOW] Panel reference is NULL.");
                yield break;
            }

            Debug.Log("[UI FLOW] Showing panel: " + panel.name);

            panel.SetActive(true);
            RefreshLanguage();
            
            m_WaitingForContinue = true;

            if (allowStopToClose)
            {
                while (m_WaitingForContinue && !StopRequested)
                    yield return null;
            }
            else
            {
                while (m_WaitingForContinue)
                    yield return null;
            }

            Debug.Log("[UI FLOW] Closing panel: " + panel.name);

            panel.SetActive(false);
            m_WaitingForContinue = false;
        }

        private void ShowOnly(GameObject panel)
        {
            HideAll();
            SetActive(panel, true);
        }

        public void OnContinue()
        {
            Debug.Log("[UI FLOW] OnContinue pressed");
            m_WaitingForContinue = false;
        }

        public void OnStop()
        {
            OnStopPressed();
        }

        public void OnStopPressed()
        {
            Debug.Log("[SessionFlowPanels] Stop pressed.");

            StopRequested = true;
            m_WaitingForContinue = false;
        }

        public void ToggleLanguage()
        {
            m_CurrentLanguage =
                m_CurrentLanguage == UiLanguage.English
                    ? UiLanguage.French
                    : UiLanguage.English;

            Debug.Log($"[SessionFlowPanels] Language changed to {m_CurrentLanguage}");

            RefreshLanguage();
        }

        private void RefreshLanguage()
        {
            if (m_LocalizedTexts == null)
                return;

            foreach (var entry in m_LocalizedTexts)
            {
                if (entry == null || entry.textTarget == null)
                    continue;

                entry.textTarget.text =
                    m_CurrentLanguage == UiLanguage.English
                        ? entry.english
                        : entry.french;
            }
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null)
                go.SetActive(on);
        }
    }
}