using System.Collections;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Top-level orchestrator for the Hit-or-Miss assessment.
    /// Flow: Idle -> Intro -> [Block -> Rest] x N -> Outro -> Idle.
    /// 3 blocks of 80 trials each. No feedback during task.
    /// </summary>
    public class HitOrMissAppController : MonoBehaviour
    {
        [Header("Task")]
        [SerializeField] TrajectoryTaskAsset m_TaskAsset;
        [SerializeField] TrajectoryTaskManager m_TaskManager;

        [Header("Input")]
        [SerializeField] ControllerButtonInput m_ControllerInput;
        [SerializeField] KeyboardCommandInput m_KeyboardInput;
        [SerializeField] HandPinchInput m_HandPinchInput;
        [SerializeField] InputMode m_InputMode = InputMode.Controller;

        [Header("Logging")]
        [SerializeField] TaskLogger m_TaskLogger;
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;

        [Header("Localization")]
        [SerializeField] LocalizedTermTable m_TermTable;
        [SerializeField] LocalizedUITextBinder[] m_UITextBinders;

        [Header("UI Panels")]
        [SerializeField] GameObject m_IntroPanel;
        [SerializeField] GameObject m_RestPanel;
        [SerializeField] GameObject m_OutroPanel;
        [SerializeField] TMPro.TMP_Text m_BlockMessageText;

        [Header("Participant Feedback")]
        [SerializeField] ResponseIndicator m_ResponseIndicator;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        SupportedLanguage m_Language = SupportedLanguage.English;
        TaskPhase m_CurrentPhase = TaskPhase.Idle;
        Coroutine m_SessionCoroutine;

        public TaskPhase CurrentPhase => m_CurrentPhase;
        public TrajectoryTaskManager TaskManager => m_TaskManager;
        public string ParticipantId { get; set; } = "P000";
        public int CurrentBlockIndex { get; private set; }

        void Awake()
        {
            if (m_TaskManager != null)
            {
                m_TaskManager.TaskAsset = m_TaskAsset;
                m_TaskManager.SetMarkerEmitter(m_EegMarkerEmitter);
            }

            SetPanelActive(m_IntroPanel, false);
            SetPanelActive(m_RestPanel, false);
            SetPanelActive(m_OutroPanel, false);
        }

        public void SetLanguage(SupportedLanguage language)
        {
            m_Language = language;
            if (m_UITextBinders == null) return;
            foreach (var binder in m_UITextBinders)
            {
                if (binder != null)
                    binder.Language = language;
            }
        }

        public void StartSession()
        {
            if (m_CurrentPhase != TaskPhase.Idle)
            {
                Debug.LogWarning("[HitOrMissAppController] Session already running.");
                return;
            }

            // Always wire every input source that is assigned in the inspector,
            // so keyboard + controller + hand pinch are all live simultaneously.
            // m_InputMode is retained for future use (e.g. logging the participant's
            // primary modality) but no longer gates which sources are active.
            var composite = new CompositeInputSource(m_ControllerInput, m_KeyboardInput, m_HandPinchInput);
            if (composite.Sources.Count == 0)
            {
                Debug.LogError("[HitOrMissAppController] No input sources assigned in inspector.");
                return;
            }
            m_TaskManager.SetInputSource(composite);

            // Configure logger
            if (m_TaskLogger != null)
            {
                m_TaskLogger.ParticipantId = ParticipantId;
                m_TaskLogger.BeginSession(m_TaskAsset != null ? m_TaskAsset.TaskName : "HitOrMiss");
                m_TaskManager.TrialJudged += m_TaskLogger.LogTrial;
            }

            // Configure EEG
            if (m_EegMarkerEmitter != null)
            {
                string sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                m_EegMarkerEmitter.BeginSession(sessionId);
            }

            // Wire response indicator
            if (m_ResponseIndicator != null)
                m_TaskManager.ResponseIndicator += m_ResponseIndicator.Show;

            // Hide clinician setup controls, keep Stop + monitoring visible
            if (m_ClinicianPanel != null)
                m_ClinicianPanel.EnterTaskMode();

            m_SessionCoroutine = StartCoroutine(RunSession());
        }

        public void StopSession()
        {
            if (m_SessionCoroutine != null)
            {
                StopCoroutine(m_SessionCoroutine);
                m_SessionCoroutine = null;
            }

            if (m_TaskManager.IsRunning)
                m_TaskManager.StopBlock();

            EndSession();
        }

        IEnumerator RunSession()
        {
            int blockCount = m_TaskAsset != null ? m_TaskAsset.BlockCount : 3;

            // === INTRO ===
            m_CurrentPhase = TaskPhase.Intro;
            m_EegMarkerEmitter?.Emit("phase_intro");
            SetPanelActive(m_IntroPanel, true);

            float introDuration = m_TaskAsset != null ? m_TaskAsset.IntroDuration : 20f;
            yield return new WaitForSeconds(introDuration);
            SetPanelActive(m_IntroPanel, false);

            // === BLOCK LOOP ===
            for (int b = 0; b < blockCount; b++)
            {
                CurrentBlockIndex = b;

                // Start block
                m_CurrentPhase = TaskPhase.Block;
                m_EegMarkerEmitter?.Emit("phase_block", "", b.ToString());
                m_TaskManager.StartBlock(b);

                // Wait for block to complete
                while (m_TaskManager.IsRunning)
                    yield return null;

                // Rest between blocks (not after the last one)
                if (b < blockCount - 1)
                {
                    m_CurrentPhase = TaskPhase.Rest;
                    m_EegMarkerEmitter?.Emit("phase_rest");
                    ShowBlockMessage(GetLocalizedString("block_complete", "Block completed - take a small break"));
                    SetPanelActive(m_RestPanel, true);

                    float restDuration = m_TaskAsset != null ? m_TaskAsset.RestDuration : 30f;
                    yield return new WaitForSeconds(restDuration);

                    SetPanelActive(m_RestPanel, false);
                }
            }

            // === OUTRO ===
            m_CurrentPhase = TaskPhase.Outro;
            m_EegMarkerEmitter?.Emit("phase_outro");
            ShowBlockMessage(GetLocalizedString("experiment_end", "End of the experiment - Thank you!"));
            SetPanelActive(m_OutroPanel, true);

            float outroDuration = m_TaskAsset != null ? m_TaskAsset.OutroDuration : 10f;
            yield return new WaitForSeconds(outroDuration);

            SetPanelActive(m_OutroPanel, false);
            EndSession();
        }

        void EndSession()
        {
            if (m_TaskLogger != null)
            {
                m_TaskManager.TrialJudged -= m_TaskLogger.LogTrial;
                m_TaskLogger.EndSession();
            }

            // Unwire response indicator
            if (m_ResponseIndicator != null)
                m_TaskManager.ResponseIndicator -= m_ResponseIndicator.Show;

            // Restore clinician controls
            if (m_ClinicianPanel != null)
                m_ClinicianPanel.ExitTaskMode();

            m_EegMarkerEmitter?.EndSession();
            m_CurrentPhase = TaskPhase.Idle;
            m_SessionCoroutine = null;

            Debug.Log("[HitOrMissAppController] Session complete. Data saved.");
        }

        void ShowBlockMessage(string message)
        {
            if (m_BlockMessageText != null)
                m_BlockMessageText.text = message;
        }

        string GetLocalizedString(string key, string fallback)
        {
            if (m_TermTable != null)
                return m_TermTable.Get(key, m_Language);
            return fallback;
        }

        static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }
    }
}
