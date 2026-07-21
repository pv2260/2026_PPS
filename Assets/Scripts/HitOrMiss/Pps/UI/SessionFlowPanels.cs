using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace HitOrMiss.Pps
{
    public class SessionFlowPanels : MonoBehaviour
    {
        [Header("Task 1 Panels")]
        [SerializeField] GameObject m_WelcomePanel;
        [SerializeField] GameObject m_InstructionsPanel;
        [SerializeField] GameObject m_PositioningPanel;
        [SerializeField] GameObject m_PracticeIntroVTOnlyPanel;
        [SerializeField] GameObject m_PracticeIntroVOnlyPanel;
        [SerializeField] GameObject m_PracticeIntroVTVisualPanel;
        [SerializeField] GameObject m_PracticeFeedbackPanel;
        [SerializeField] GameObject m_NoFeedbackPanel;
        [SerializeField] GameObject m_ReadyToStartPanel;
        [SerializeField] GameObject m_BlockCounterPanel;
        [SerializeField] GameObject m_BreakPanel;
        [SerializeField] GameObject m_PausePanel;
        [SerializeField] GameObject m_AttentionCheckPanel;
        [SerializeField] GameObject m_EndPanel;

        [Header("Optional Dynamic Text")]
        [SerializeField] TMP_Text m_BlockCounterText;
        [SerializeField] TMP_Text m_BreakText;
        [Tooltip("Optional dedicated countdown label inside the break panel. If wired, the remaining seconds are written here (big number, separate from m_BreakText's body copy).")]
        [SerializeField] TMP_Text m_BreakCountdownText;
        [SerializeField] TMP_Text m_PracticeFeedbackText;
        [SerializeField] TMP_Text m_EndText;

        [Header("AR Guidance")]
        [SerializeField] private GameObject m_StandingCross;

        [Header("Feedback Timing")]
        [SerializeField] float m_PracticeFeedbackSeconds = 1f;

        [Header("Break Extension")]
        [SerializeField] private Button m_ExtendBreakButton;
        [SerializeField] private float m_ExtendBreakSeconds = 30f;

        private float m_BreakRemainingSeconds;
        private bool m_BreakIsRunning;

        private int m_TokenBlocksCount;
        private int m_TokenCurrentBlock;
        private int m_TokenTotalBlocks;
        private float m_TokenBreakSeconds;

        public void SetTokens(int blocksCount, int currentBlock, int totalBlocks, float breakSeconds)
        {
            m_TokenBlocksCount  = blocksCount;
            m_TokenCurrentBlock = currentBlock;
            m_TokenTotalBlocks  = totalBlocks;
            m_TokenBreakSeconds = breakSeconds;
        }

        private void ResolveTokens()
        {
            if (m_LocalizedTexts == null) return;

            string breakTimeStr = m_TokenBreakSeconds >= 60f
                ? (m_CurrentLanguage == UiLanguage.English
                    ? $"{Mathf.RoundToInt(m_TokenBreakSeconds / 60f)}-minute"
                    : $"{Mathf.RoundToInt(m_TokenBreakSeconds / 60f)} minutes")
                : (m_CurrentLanguage == UiLanguage.English
                    ? $"{Mathf.RoundToInt(m_TokenBreakSeconds)}-second"
                    : $"{Mathf.RoundToInt(m_TokenBreakSeconds)} secondes");

            foreach (var entry in m_LocalizedTexts)
            {
                if (entry == null || entry.textTarget == null) continue;

                entry.textTarget.text = entry.textTarget.text
                    .Replace("{blocksCount}",  m_TokenBlocksCount.ToString())
                    .Replace("{currentBlock}", m_TokenCurrentBlock.ToString())
                    .Replace("{totalBlocks}",  m_TokenTotalBlocks.ToString())
                    .Replace("{breakTime}",    breakTimeStr);
            }
        }

        public enum PanelNavigationAction
        {
            Continue,
            Back,
            Stop
        }

        private bool m_BackRequested;
        public bool BackRequested => m_BackRequested;
        public PanelNavigationAction LastAction { get; private set; } = PanelNavigationAction.Continue;

        bool m_WaitingForContinue;
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

        void Awake()
        {
            HideAll();
            RefreshLanguage();
                if (m_ExtendBreakButton != null)
                m_ExtendBreakButton.onClick.AddListener(ExtendBreak);

        }

        void OnDestroy()
        {
            if (m_ExtendBreakButton != null)
                m_ExtendBreakButton.onClick.RemoveListener(ExtendBreak);
        }
        public void HideAll()
        {
            SetActive(m_WelcomePanel, false);
            SetActive(m_InstructionsPanel, false);
            SetActive(m_PositioningPanel, false);
            SetActive(m_PracticeIntroVTOnlyPanel, false);
            SetActive(m_PracticeIntroVOnlyPanel, false);
            SetActive(m_PracticeIntroVTVisualPanel, false);
            SetActive(m_PracticeFeedbackPanel, false);
            SetActive(m_NoFeedbackPanel, false);
            SetActive(m_ReadyToStartPanel, false);
            SetActive(m_BlockCounterPanel, false);
            SetActive(m_BreakPanel, false);
            SetActive(m_PausePanel, false);
            SetActive(m_AttentionCheckPanel, false);
            SetActive(m_EndPanel, false);
        }

        public IEnumerator ShowWelcomeAndWait()
        {
            Debug.Log("[UI FLOW] ShowWelcomeAndWait called");

            ClearBackRequest();
            yield return ShowAndWait(m_WelcomePanel);
        }

        public IEnumerator ShowInstructionsAndWait()
        {
            ClearBackRequest();
            yield return ShowAndWait(m_InstructionsPanel);
        }

        public IEnumerator ShowPositioningAndWait()
        {
            ClearBackRequest();
            yield return ShowAndWait(m_PositioningPanel);
        }

        public void ShowStandingCross()
        {
            SetActive(m_StandingCross, true);
        }

        public void HideStandingCross()
        {
            SetActive(m_StandingCross, false);
        }

        public IEnumerator ShowPracticeIntroVTOnlyAndWait()
        {
            ClearBackRequest();
            yield return ShowAndWait(m_PracticeIntroVTOnlyPanel);
        }

        public IEnumerator ShowPracticeIntroVTVisualAndWait()
        {
            ClearBackRequest();
            yield return ShowAndWait(m_PracticeIntroVTVisualPanel);
        }

        public IEnumerator ShowPracticeIntroVOnlyAndWait()
        {
            ClearBackRequest();
            yield return ShowAndWait(m_PracticeIntroVOnlyPanel);
        }


        public IEnumerator ShowNoFeedbackAndWait()
        {
            ClearBackRequest();
            yield return ShowAndWait(m_NoFeedbackPanel);
        }

        public IEnumerator ShowReadyToStartAndWait()
        {
            ClearBackRequest();
            yield return ShowAndWait(m_ReadyToStartPanel);
        }

        public IEnumerator ShowBlockCounterAndWait(int blockIndex, int totalBlocks)
        {
            ClearBackRequest();

            HideAll();

            if (m_BlockCounterPanel == null)
            {
                Debug.LogError("[UI FLOW] BlockCounterPanel reference is NULL.");
                yield break;
            }

            Debug.Log($"[UI FLOW] Showing block counter: {blockIndex + 1} / {totalBlocks}");

            m_BlockCounterPanel.SetActive(true);
            RefreshLanguage();

            if (m_BlockCounterText != null)
            {
                if (m_CurrentLanguage == UiLanguage.English)
                {
                    m_BlockCounterText.text =
                        $"Block {blockIndex + 1} / {totalBlocks}\n\nPress Begin when you are ready.";
                }
                else
                {
                    m_BlockCounterText.text =
                        $"Bloc {blockIndex + 1} / {totalBlocks}\n\nAppuyez sur Démarrer lorsque vous êtes prêt.";
                }
            }
            else
            {
                Debug.LogWarning("[UI FLOW] m_BlockCounterText is not wired — block counter text won't update.");
            }

            m_WaitingForContinue = true;

            while (m_WaitingForContinue && !StopRequested && !m_BackRequested)
                yield return null;

            m_BlockCounterPanel.SetActive(false);
            m_WaitingForContinue = false;
        }
        public void ExtendBreak()
        {
            if (!m_BreakIsRunning)
                return;

            m_BreakRemainingSeconds += m_ExtendBreakSeconds;

            Debug.Log(
                $"[UI FLOW] Break extended by {m_ExtendBreakSeconds:F0}s. " +
                $"Remaining={m_BreakRemainingSeconds:F1}s"
            );
        }

        public IEnumerator ShowPauseAndWait()
            => ShowAndWait(m_PausePanel);

        public IEnumerator ShowEndAndWait(string text = null)
        {
            if (m_EndText != null && text != null)
                m_EndText.text = text;

            yield return ShowAndWait(m_EndPanel, allowStopToClose: false);
        }

        public IEnumerator ShowPracticeFeedback(string message)
        {
            HideAll();

            if (m_PracticeFeedbackText != null)
                m_PracticeFeedbackText.text = message;

            SetActive(m_PracticeFeedbackPanel, true);

            float elapsed = 0f;

            while (elapsed < m_PracticeFeedbackSeconds && !StopRequested)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetActive(m_PracticeFeedbackPanel, false);
        }

        public IEnumerator ShowAttentionCheckAndWait()
        {
            Debug.Log("[UI FLOW] Showing attention check.");

            ClearBackRequest();

            yield return ShowAndWait(m_AttentionCheckPanel);

            Debug.Log("[UI FLOW] Attention check completed.");
        }

        public void OnBack()
        {
            Debug.Log("[UI FLOW] OnBack pressed");

            m_BackRequested = true;
            LastAction = PanelNavigationAction.Back;
            m_WaitingForContinue = false;
        }

        public void ClearBackRequest()
        {
            m_BackRequested = false;
            LastAction = PanelNavigationAction.Continue;
        }
                        
        public IEnumerator ShowBreakAndWait(float breakSeconds)
        {
            HideAll();

            if (m_BreakPanel == null)
            {
                Debug.LogError("[UI FLOW] BreakPanel reference is NULL.");
                yield break;
            }

            SetActive(m_BreakPanel, true);

            if (m_ExtendBreakButton != null)
                m_ExtendBreakButton.gameObject.SetActive(true);

            if (m_BreakText == null && m_BreakCountdownText == null)
            {
                Debug.LogWarning(
                    "[UI FLOW] ShowBreakAndWait: both m_BreakText and m_BreakCountdownText are NULL — " +
                    "break countdown will not be visible. Wire at least one on SessionFlowPanels."
                );
            }

            m_WaitingForContinue = true;
            m_BreakIsRunning = true;
            m_BreakRemainingSeconds = Mathf.Max(0f, breakSeconds);

            int lastWholeSecond = -1;

            while (m_BreakRemainingSeconds > 0f && m_WaitingForContinue && !StopRequested)
            {
                int secondsLeft = Mathf.CeilToInt(m_BreakRemainingSeconds);

                if (secondsLeft != lastWholeSecond)
                {
                    lastWholeSecond = secondsLeft;
                    UpdateBreakText(secondsLeft);
                }

                m_BreakRemainingSeconds -= Time.deltaTime;
                yield return null;
            }

            m_BreakIsRunning = false;
            m_WaitingForContinue = false;

            if (m_ExtendBreakButton != null)
                m_ExtendBreakButton.gameObject.SetActive(false);

            SetActive(m_BreakPanel, false);
        }


        private void UpdateBreakText(int secondsLeft)
        {
            if (m_BreakText != null)
            {
                m_BreakText.text = m_CurrentLanguage == UiLanguage.English
                    ? $"Break\n\n{secondsLeft} seconds remaining.\n\nPress Continue when ready."
                    : $"Pause\n\nIl reste {secondsLeft} secondes.\n\nAppuyez sur Continuer lorsque vous êtes prêt.";
            }

            if (m_BreakCountdownText != null)
                m_BreakCountdownText.text = secondsLeft.ToString();
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
            while (m_WaitingForContinue && !StopRequested && !m_BackRequested)
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

        public void OnContinue()
        {
            Debug.Log("[UI FLOW] OnContinue pressed");

            m_BackRequested = false;
            LastAction = PanelNavigationAction.Continue;
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
            LastAction = PanelNavigationAction.Stop;
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
            ResolveTokens();
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null)
                go.SetActive(on);
        }
    }
}