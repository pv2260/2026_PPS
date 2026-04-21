using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HitOrMiss
{
    /// <summary>
    /// Clinician control panel with live monitoring.
    /// On session start: hides setup controls (Start, Language, ID) but keeps
    /// Stop button and monitoring stats visible.
    /// </summary>
    public class ClinicianControlPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] HitOrMissAppController m_AppController;
        [SerializeField] TrajectoryTaskManager m_TaskManager;

        [Header("Setup Controls (hidden during task)")]
        [SerializeField] Button m_StartButton;
        [SerializeField] Button m_LanguageToggleButton;
        [SerializeField] TMP_InputField m_ParticipantIdField;

        [Header("Always-Visible Controls")]
        [SerializeField] Button m_StopButton;

        [Header("Status Display")]
        [SerializeField] TMP_Text m_PhaseText;
        [SerializeField] TMP_Text m_BlockProgressText;
        [SerializeField] TMP_Text m_TrialCounterText;
        [SerializeField] TMP_Text m_AccuracyText;
        [SerializeField] TMP_Text m_CategoryBreakdownText;
        [SerializeField] TMP_Text m_LanguageLabel;
        [SerializeField] TMP_Text m_LastTrialText;

        SupportedLanguage m_CurrentLanguage = SupportedLanguage.English;
        int m_CorrectCount;
        int m_TotalResponded;
        int m_HitCorrect, m_NearHitCorrect, m_NearMissCorrect, m_MissCorrect;
        int m_HitTotal, m_NearHitTotal, m_NearMissTotal, m_MissTotal;

        void Awake()
        {
            if (m_StartButton != null) m_StartButton.onClick.AddListener(OnStartPressed);
            if (m_StopButton != null) m_StopButton.onClick.AddListener(OnStopPressed);
            if (m_LanguageToggleButton != null) m_LanguageToggleButton.onClick.AddListener(OnLanguageToggle);
        }

        void OnEnable()
        {
            if (m_TaskManager != null)
                m_TaskManager.TrialJudged += OnTrialJudged;
        }

        void OnDisable()
        {
            if (m_TaskManager != null)
                m_TaskManager.TrialJudged -= OnTrialJudged;
        }

        void Update()
        {
            UpdateStatusDisplay();
        }

        void OnStartPressed()
        {
            if (m_AppController == null) return;

            if (m_ParticipantIdField != null && !string.IsNullOrEmpty(m_ParticipantIdField.text))
                m_AppController.ParticipantId = m_ParticipantIdField.text;

            ResetCounters();
            m_AppController.StartSession();
        }

        void OnStopPressed()
        {
            m_AppController?.StopSession();
        }

        void OnLanguageToggle()
        {
            m_CurrentLanguage = m_CurrentLanguage == SupportedLanguage.English
                ? SupportedLanguage.French
                : SupportedLanguage.English;

            m_AppController?.SetLanguage(m_CurrentLanguage);

            if (m_LanguageLabel != null)
                m_LanguageLabel.text = m_CurrentLanguage.ToString();
        }

        /// <summary>
        /// Called by HitOrMissAppController when session starts.
        /// Hides setup controls, keeps Stop + monitoring visible.
        /// </summary>
        public void EnterTaskMode()
        {
            SetActive(m_StartButton, false);
            SetActive(m_LanguageToggleButton, false);
            if (m_ParticipantIdField != null) m_ParticipantIdField.gameObject.SetActive(false);
            SetActive(m_StopButton, true);
        }

        /// <summary>
        /// Called by HitOrMissAppController when session ends.
        /// Restores all setup controls.
        /// </summary>
        public void ExitTaskMode()
        {
            SetActive(m_StartButton, true);
            SetActive(m_LanguageToggleButton, true);
            if (m_ParticipantIdField != null) m_ParticipantIdField.gameObject.SetActive(true);
            SetActive(m_StopButton, true);
        }

        void OnTrialJudged(TrialJudgement j)
        {
            if (j.result != TrialResult.NoResponse)
                m_TotalResponded++;
            if (j.isCorrect)
                m_CorrectCount++;

            switch (j.category)
            {
                case TrialCategory.Hit:
                    m_HitTotal++;
                    if (j.isCorrect) m_HitCorrect++;
                    break;
                case TrialCategory.NearHit:
                    m_NearHitTotal++;
                    if (j.isCorrect) m_NearHitCorrect++;
                    break;
                case TrialCategory.NearMiss:
                    m_NearMissTotal++;
                    if (j.isCorrect) m_NearMissCorrect++;
                    break;
                case TrialCategory.Miss:
                    m_MissTotal++;
                    if (j.isCorrect) m_MissCorrect++;
                    break;
            }

            if (m_LastTrialText != null)
            {
                string rt = j.reactionTimeMs > 0 ? $"{j.reactionTimeMs:F0}ms" : "N/A";
                m_LastTrialText.text = $"Last: {j.trialId} {j.category} | {j.received} | {(j.isCorrect ? "OK" : "X")} | RT: {rt}";
            }
        }

        void UpdateStatusDisplay()
        {
            if (m_AppController == null) return;

            if (m_PhaseText != null)
                m_PhaseText.text = $"Phase: {m_AppController.CurrentPhase}";

            if (m_BlockProgressText != null)
            {
                var mgr = m_AppController.TaskManager;
                if (mgr != null && mgr.IsRunning)
                    m_BlockProgressText.text = $"Block {m_AppController.CurrentBlockIndex + 1} | Trial {mgr.TrialsCompletedInBlock}/{mgr.TotalTrialsInBlock}";
                else
                    m_BlockProgressText.text = $"Block {m_AppController.CurrentBlockIndex + 1}";
            }

            if (m_TrialCounterText != null)
                m_TrialCounterText.text = $"Total completed: {m_TaskManager?.Results.Count ?? 0}";

            if (m_AccuracyText != null && m_TotalResponded > 0)
            {
                float pct = (float)m_CorrectCount / m_TotalResponded * 100f;
                m_AccuracyText.text = $"Accuracy: {pct:F1}% ({m_CorrectCount}/{m_TotalResponded})";
            }

            if (m_CategoryBreakdownText != null)
            {
                m_CategoryBreakdownText.text =
                    $"HIT: {m_HitCorrect}/{m_HitTotal}  NearHIT: {m_NearHitCorrect}/{m_NearHitTotal}\n" +
                    $"NearMISS: {m_NearMissCorrect}/{m_NearMissTotal}  MISS: {m_MissCorrect}/{m_MissTotal}";
            }
        }

        void ResetCounters()
        {
            m_CorrectCount = m_TotalResponded = 0;
            m_HitCorrect = m_NearHitCorrect = m_NearMissCorrect = m_MissCorrect = 0;
            m_HitTotal = m_NearHitTotal = m_NearMissTotal = m_MissTotal = 0;

            if (m_AccuracyText != null) m_AccuracyText.text = "Accuracy: --";
            if (m_CategoryBreakdownText != null) m_CategoryBreakdownText.text = "";
            if (m_LastTrialText != null) m_LastTrialText.text = "";
        }

        static void SetActive(Button btn, bool active)
        {
            if (btn != null) btn.gameObject.SetActive(active);
        }

        void OnDestroy()
        {
            if (m_StartButton != null) m_StartButton.onClick.RemoveListener(OnStartPressed);
            if (m_StopButton != null) m_StopButton.onClick.RemoveListener(OnStopPressed);
            if (m_LanguageToggleButton != null) m_LanguageToggleButton.onClick.RemoveListener(OnLanguageToggle);
        }
    }
}
