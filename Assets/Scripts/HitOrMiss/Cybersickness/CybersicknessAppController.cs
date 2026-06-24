using System.Collections;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CybersicknessAppController : MonoBehaviour
    {
        [Header("Welcome")]
        [SerializeField] GameObject m_WelcomePanel;

        [Header("Task")]
        [SerializeField] CyberBugTaskAsset m_TaskAsset;
        [SerializeField] CybersicknessTaskManager m_TaskManager;

        [Header("Logging")]
        [SerializeField] CybersicknessCsvLogger m_CsvLogger;
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;

        [Header("Participant")]
        [SerializeField] string m_ParticipantId = "P000";

        [Header("Cyber popup GameObjects")]
        [SerializeField] GameObject m_ControllerIntroPanel;
        [SerializeField] GameObject m_IntroPanel;
        [SerializeField] GameObject m_ControllerGuidePanel;
        [SerializeField] GameObject m_PracticeYesPanel;
        [SerializeField] GameObject m_PracticeNoPanel;
        [SerializeField] GameObject m_ReadyPanel;
        [SerializeField] GameObject m_BreakPanel;
        [SerializeField] GameObject m_EndPanel;

        [Header("Controller intro")]
        [SerializeField] CyberControllerIntroDemo m_ControllerIntroDemo;
        [SerializeField] float m_ControllerIntroAdvanceDelay = 0.6f;


        Coroutine m_SessionCoroutine;

        bool m_WaitingForPanelAdvance;
        bool m_PracticeWaiting;
        CyberYesNoResponse m_PracticeReceived;

        bool m_ControllerIntroDemoStarted;
        bool m_ControllerIntroCompleted;
        bool m_ControllerIntroWaiting;
        bool m_ControllerIntroLeftPressed;
        bool m_ControllerIntroRightPressed;

        public bool IsRunning { get; private set; }

        void Awake()
        {
            HideAllPanels();

            if (m_WelcomePanel != null)
                m_WelcomePanel.SetActive(true);

            if (m_TaskManager != null && m_TaskAsset != null)
                m_TaskManager.SetTaskAsset(m_TaskAsset);
        }

        public void StartFromWelcomeButton()
        {
            Debug.LogError("[CYBER START BUTTON] StartFromWelcomeButton was clicked.");

            if (m_WelcomePanel != null)
                m_WelcomePanel.SetActive(false);

            StartSession();
        }

        public void StartSession()
        {
            Debug.LogError("[CYBER START] StartSession() was called.");

            if (IsRunning)
            {
                Debug.LogWarning("[CybersicknessAppController] Session already running.");
                return;
            }

            if (m_TaskAsset == null)
            {
                Debug.LogError("[CybersicknessAppController] No CyberBugTaskAsset assigned.");
                return;
            }

            if (m_TaskManager == null)
            {
                Debug.LogError("[CybersicknessAppController] No CybersicknessTaskManager assigned.");
                return;
            }

            IsRunning = true;

            m_TaskManager.SetTaskAsset(m_TaskAsset);
            m_TaskManager.TrialCompleted += OnTrialCompleted;

            if (m_CsvLogger != null)
                m_CsvLogger.BeginSession(m_ParticipantId);

            if (m_EegMarkerEmitter != null)
            {
                string sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                m_EegMarkerEmitter.BeginSession(sessionId);
                m_EegMarkerEmitter.Emit("cyberfish_session_start", extra: m_ParticipantId);
            }

            m_SessionCoroutine = StartCoroutine(RunSession());
        }

        IEnumerator RunSession()
        {
            Debug.LogError("[CYBER FLOW] Showing Controller Intro.");
            yield return RunControllerIntro();

            Debug.LogError("[CYBER FLOW] Showing Intro.");
            yield return ShowPanelAndWait(m_IntroPanel);

            Debug.LogError("[CYBER FLOW] Showing Controller Guide.");
            yield return ShowPanelAndWait(m_ControllerGuidePanel);

            Debug.LogError("[CYBER FLOW] Practice YES.");
            yield return RunPracticeYes();

            Debug.LogError("[CYBER FLOW] Practice NO.");
            yield return RunPracticeNo();

            Debug.LogError("[CYBER FLOW] Showing Ready.");
            yield return ShowPanelAndWait(m_ReadyPanel);

            for (int b = 0; b < m_TaskAsset.BlockCount; b++)
            {
                Debug.LogError($"[CYBER FLOW] Starting block {b + 1}.");

                m_TaskManager.StartBlock(b);

                while (m_TaskManager.IsRunning)
                    yield return null;

                if (b < m_TaskAsset.BlockCount - 1)
                    yield return ShowPanelAndWait(m_BreakPanel);
            }

            yield return ShowPanelAndWait(m_EndPanel);

            EndSession();
        }

        IEnumerator ShowPanelAndWait(GameObject panel)
        {
            HideAllPanels();

            if (panel == null)
            {
                Debug.LogError("[CYBER FLOW] Panel reference is null.");
                yield break;
            }

            Debug.LogError($"[CYBER FLOW] Activating panel: {panel.name}");

            ForceActivateHierarchy(panel);

            m_WaitingForPanelAdvance = true;

            while (m_WaitingForPanelAdvance)
                yield return null;

            panel.SetActive(false);
        }

        IEnumerator RunControllerIntro()
        {
            HideAllPanels();

            if (m_ControllerIntroPanel == null)
            {
                Debug.LogError("[CYBER FLOW] ControllerIntroPanel is not assigned.");
                yield break;
            }

            Debug.LogError("[CYBER FLOW] Activating ControllerIntroPanel.");

            ForceActivateHierarchy(m_ControllerIntroPanel);

            m_ControllerIntroDemoStarted = false;
            m_ControllerIntroCompleted = false;
            m_ControllerIntroLeftPressed = false;
            m_ControllerIntroRightPressed = false;

            if (m_ControllerIntroDemo != null)
                m_ControllerIntroDemo.ShowBeforeStart();

            while (!m_ControllerIntroCompleted)
                yield return null;

            yield return new WaitForSeconds(m_ControllerIntroAdvanceDelay);

            m_ControllerIntroPanel.SetActive(false);
        }

        public void StartControllerIntroDemo()
        {
            Debug.LogError("[CONTROLLER INTRO] StartControllerIntroDemo clicked.");

            m_ControllerIntroDemoStarted = true;
            m_ControllerIntroLeftPressed = false;
            m_ControllerIntroRightPressed = false;
            m_ControllerIntroCompleted = false;

            if (m_ControllerIntroDemo != null)
                m_ControllerIntroDemo.BeginDemo();
        }

        void ForceActivateHierarchy(GameObject obj)
        {
            Transform t = obj.transform;

            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);

                t = t.parent;
            }

            obj.SetActive(true);
        }

        public void AdvanceCurrentPanel()
        {
            Debug.LogError("[CYBER FLOW] AdvanceCurrentPanel clicked.");

            // Special case: controller intro panel is waiting for left + right trigger.
            // This allows the Continue button to advance it too.
            if (m_ControllerIntroWaiting)
            {
                Debug.LogError("[CYBER FLOW] Advancing Controller Intro panel.");
                m_ControllerIntroWaiting = false;
                return;
            }

            // Normal popup panels.
            m_WaitingForPanelAdvance = false;
        }

        IEnumerator RunPracticeYes()
        {
            HideAllPanels();

            if (m_PracticeYesPanel != null)
                ForceActivateHierarchy(m_PracticeYesPanel);

            m_PracticeReceived = CyberYesNoResponse.None;
            m_PracticeWaiting = true;

            while (m_PracticeReceived != CyberYesNoResponse.Yes)
                yield return null;

            m_PracticeWaiting = false;

            if (m_PracticeYesPanel != null)
                m_PracticeYesPanel.SetActive(false);

            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator RunPracticeNo()
        {
            HideAllPanels();

            if (m_PracticeNoPanel != null)
                ForceActivateHierarchy(m_PracticeNoPanel);

            m_PracticeReceived = CyberYesNoResponse.None;
            m_PracticeWaiting = true;

            while (m_PracticeReceived != CyberYesNoResponse.No)
                yield return null;

            m_PracticeWaiting = false;

            if (m_PracticeNoPanel != null)
                m_PracticeNoPanel.SetActive(false);

            yield return new WaitForSeconds(0.3f);
        }

        public void PracticePressYes()
        {

                    if (m_ControllerIntroDemoStarted && !m_ControllerIntroCompleted)
        {
            Debug.LogError("[CONTROLLER INTRO] LEFT trigger detected.");

            m_ControllerIntroLeftPressed = true;
            m_ControllerIntroDemo?.LeftTriggerPressed();

            if (m_ControllerIntroLeftPressed && m_ControllerIntroRightPressed)
                m_ControllerIntroCompleted = true;

            return;
        }
            if (m_ControllerIntroWaiting)
            {
                Debug.LogError("[CONTROLLER INTRO] LEFT trigger pressed.");

                m_ControllerIntroLeftPressed = true;
                m_ControllerIntroDemo?.LeftTriggerPressed();

                if (m_ControllerIntroLeftPressed && m_ControllerIntroRightPressed)
                    m_ControllerIntroWaiting = false;

                return;
            }

            if (!m_PracticeWaiting) return;

            Debug.LogError("[CYBER PRACTICE] YES pressed.");

            m_PracticeReceived = CyberYesNoResponse.Yes;
            m_EegMarkerEmitter?.Emit("cyberfish_practice_yes");
        }

        public void PracticePressNo()
        {
                        if (m_ControllerIntroDemoStarted && !m_ControllerIntroCompleted)
            {
                Debug.LogError("[CONTROLLER INTRO] RIGHT trigger detected.");

                m_ControllerIntroRightPressed = true;
                m_ControllerIntroDemo?.RightTriggerPressed();

                if (m_ControllerIntroLeftPressed && m_ControllerIntroRightPressed)
                    m_ControllerIntroCompleted = true;

                return;
            }
            if (m_ControllerIntroWaiting)
            {
                Debug.LogError("[CONTROLLER INTRO] RIGHT trigger pressed.");

                m_ControllerIntroRightPressed = true;
                m_ControllerIntroDemo?.RightTriggerPressed();

                if (m_ControllerIntroLeftPressed && m_ControllerIntroRightPressed)
                    m_ControllerIntroWaiting = false;

                return;
            }

            if (!m_PracticeWaiting) return;

            Debug.LogError("[CYBER PRACTICE] NO pressed.");

            m_PracticeReceived = CyberYesNoResponse.No;
            m_EegMarkerEmitter?.Emit("cyberfish_practice_no");
        }

        void OnTrialCompleted(CyberBugTrialResult result)
        {
            result.participantId = m_ParticipantId;

            if (m_CsvLogger != null)
                m_CsvLogger.LogTrial(result);
        }

        public void StopSession()
        {
            if (m_TaskManager != null && m_TaskManager.IsRunning)
                m_TaskManager.StopBlock();

            EndSession();
        }

        void EndSession()
        {
            if (!IsRunning) return;

            IsRunning = false;

            if (m_SessionCoroutine != null)
            {
                StopCoroutine(m_SessionCoroutine);
                m_SessionCoroutine = null;
            }

            if (m_TaskManager != null)
                m_TaskManager.TrialCompleted -= OnTrialCompleted;

            if (m_CsvLogger != null)
                m_CsvLogger.EndSession();

            HideAllPanels();

            m_EegMarkerEmitter?.Emit("cyberfish_session_end");
        }

        void HideAllPanels()
        {   
            if (m_ControllerIntroPanel != null) m_ControllerIntroPanel.SetActive(false);
            if (m_IntroPanel != null) m_IntroPanel.SetActive(false);
            if (m_ControllerGuidePanel != null) m_ControllerGuidePanel.SetActive(false);
            if (m_PracticeYesPanel != null) m_PracticeYesPanel.SetActive(false);
            if (m_PracticeNoPanel != null) m_PracticeNoPanel.SetActive(false);
            if (m_ReadyPanel != null) m_ReadyPanel.SetActive(false);
            if (m_BreakPanel != null) m_BreakPanel.SetActive(false);
            if (m_EndPanel != null) m_EndPanel.SetActive(false);
        }
    }
}