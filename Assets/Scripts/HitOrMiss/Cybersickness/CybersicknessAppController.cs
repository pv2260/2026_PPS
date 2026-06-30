using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField] GameObject m_PracticeIntroPopup;
        [SerializeField] GameObject m_TriggerDemoPopup;
        [SerializeField] GameObject m_ResponseMappingPopup;
        [SerializeField] GameObject m_ApproachIntroPopup;
        [SerializeField] GameObject m_IntroTaskPopup;
        [SerializeField] GameObject m_ControllerGuidePopup;
        [SerializeField] GameObject m_PracticeYesPopup;

        [FormerlySerializedAs("m_PracticeNoPPopup")]
        [SerializeField] GameObject m_PracticeNoPopup;

        [SerializeField] GameObject m_ReadyPopup;
        [SerializeField] GameObject m_BreakPopup;
        [SerializeField] GameObject m_EndPopup;

        [Header("Instruction demos")]
        [FormerlySerializedAs("m_ControllerIntroDemo")]
        [SerializeField] CyberControllerIntroDemo m_TriggerDemo;

        [SerializeField] CyberResponseMappingDemo m_ResponseMappingDemo;
        [SerializeField] CyberApproachIntroDemo m_ApproachIntroDemo;

        [Header("Timing")]
        [SerializeField] float m_TriggerDemoAdvanceDelay = 0.6f;
        [SerializeField] float m_ResponseMappingAdvanceDelay = 0.6f;
        [SerializeField] float m_ApproachIntroFallbackSeconds = 4.0f;

        Coroutine m_SessionCoroutine;

        bool m_WaitingForPanelAdvance;

        bool m_PracticeWaiting;
        CyberYesNoResponse m_PracticeReceived;

        bool m_TriggerDemoWaiting;
        bool m_TriggerDemoLeftPressed;
        bool m_TriggerDemoRightPressed;

        bool m_ResponseMappingWaiting;
        bool m_ResponseMappingLeftPressed;
        bool m_ResponseMappingRightPressed;

        public bool IsRunning { get; private set; }

        void Awake()
        {
            HideAllPanels();

            ApplyTaskLanguage();

            if (m_WelcomePanel != null)
                m_WelcomePanel.SetActive(true);

            if (m_TaskManager != null && m_TaskAsset != null)
                m_TaskManager.SetTaskAsset(m_TaskAsset);
        }

        public void SwitchLanguage()
{
    if (m_TaskAsset == null)
    {
        Debug.LogWarning("[CYBER LANGUAGE] Cannot switch language. Task asset is not assigned.");
        return;
    }

    m_TaskAsset.ToggleLanguage();

    Debug.LogError($"[CYBER LANGUAGE] Switched to: {m_TaskAsset.CurrentLanguage}");

    ApplyTaskLanguage();
}

void ApplyTaskLanguage()
{
    if (m_TaskAsset == null)
        return;

    SetPopupText(m_PracticeIntroPopup, m_TaskAsset.PracticeIntroText);

    SetPopupText(m_TriggerDemoPopup, m_TaskAsset.TriggerDemoText);
    SendDemoText(m_TriggerDemo, m_TaskAsset.TriggerDemoText);

    SetPopupText(m_ResponseMappingPopup, m_TaskAsset.ResponseMappingText);
    SendDemoText(m_ResponseMappingDemo, m_TaskAsset.ResponseMappingText);

    SetPopupText(m_ApproachIntroPopup, m_TaskAsset.ApproachIntroText);
    SendDemoText(m_ApproachIntroDemo, m_TaskAsset.ApproachIntroText);

    SetPopupText(m_IntroTaskPopup, m_TaskAsset.IntroTaskText);
    SetPopupText(m_ControllerGuidePopup, m_TaskAsset.ControllerGuideText);

    SetPopupText(m_ReadyPopup, m_TaskAsset.ReadyText);
    SetPopupText(m_EndPopup, m_TaskAsset.OutroText);
}

    void SetPopupText(GameObject popupObject, string text)
    {
        if (popupObject == null)
            return;

        if (string.IsNullOrEmpty(text))
            return;

        CyberTaskPopupPanel popupPanel =
            popupObject.GetComponent<CyberTaskPopupPanel>();

        if (popupPanel != null)
            popupPanel.SetText(text);

        TMP_Text[] textObjects =
            popupObject.GetComponentsInChildren<TMP_Text>(true);

        TMP_Text targetText = null;

        foreach (TMP_Text textObject in textObjects)
        {
            if (textObject.name == "BodyText")
            {
                targetText = textObject;
                break;
            }
        }

        if (targetText == null && textObjects.Length > 0)
            targetText = textObjects[0];

        if (targetText != null)
            targetText.text = text;
    }

    void SendDemoText(MonoBehaviour demo, string text)
    {
        if (demo == null)
            return;

        demo.SendMessage(
            "SetDemoText",
            text,
            SendMessageOptions.DontRequireReceiver
        );
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

            if (m_WelcomePanel != null)
                m_WelcomePanel.SetActive(false);

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
            
            ApplyTaskLanguage();
            m_SessionCoroutine = StartCoroutine(RunSession());
        }

        IEnumerator RunSession()
        {
            Debug.LogError("[CYBER FLOW] Showing Practice Intro.");
            yield return ShowPanelAndWait(m_PracticeIntroPopup);

            Debug.LogError("[CYBER FLOW] Showing Trigger Demo.");
            yield return RunTriggerDemo();

            Debug.LogError("[CYBER FLOW] Showing Response Mapping.");
            yield return RunResponseMappingDemo();

            Debug.LogError("[CYBER FLOW] Showing Approach Intro.");
            yield return RunApproachIntroDemo();

            Debug.LogError("[CYBER FLOW] Showing Task Intro.");
            yield return ShowPanelAndWait(m_IntroTaskPopup);

            Debug.LogError("[CYBER FLOW] Showing Controller Guide.");
            yield return ShowPanelAndWait(m_ControllerGuidePopup);

            Debug.LogError("[CYBER FLOW] Practice YES.");
            yield return RunPracticeYes();

            Debug.LogError("[CYBER FLOW] Practice NO.");
            yield return RunPracticeNo();

            Debug.LogError("[CYBER FLOW] Showing Ready.");
            yield return ShowPanelAndWait(m_ReadyPopup);

            for (int b = 0; b < m_TaskAsset.BlockCount; b++)
            {
                Debug.LogError($"[CYBER FLOW] Starting block {b + 1}.");

                m_TaskManager.StartBlock(b);

                while (m_TaskManager.IsRunning)
                    yield return null;

                if (b < m_TaskAsset.BlockCount - 1)
                    yield return ShowPanelAndWait(m_BreakPopup);
            }

            Debug.LogError("[CYBER FLOW] Showing End.");
            yield return ShowPanelAndWait(m_EndPopup);

            m_SessionCoroutine = null;
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

        IEnumerator RunTriggerDemo()
        {
            HideAllPanels();

            if (m_TriggerDemoPopup == null)
            {
                Debug.LogError("[CYBER FLOW] TriggerDemoPopup is not assigned.");
                yield break;
            }

            Debug.LogError("[CYBER FLOW] Activating TriggerDemoPopup.");

            ForceActivateHierarchy(m_TriggerDemoPopup);

            m_TriggerDemoWaiting = true;
            m_TriggerDemoLeftPressed = false;
            m_TriggerDemoRightPressed = false;

            if (m_TriggerDemo != null)
                m_TriggerDemo.ResetDemo();
            else
                Debug.LogError("[CYBER FLOW] TriggerDemo component is not assigned.");

            while (m_TriggerDemoWaiting)
                yield return null;

            yield return new WaitForSeconds(m_TriggerDemoAdvanceDelay);

            m_TriggerDemoPopup.SetActive(false);
        }

        IEnumerator RunResponseMappingDemo()
        {
            HideAllPanels();

            if (m_ResponseMappingPopup == null)
            {
                Debug.LogError("[CYBER FLOW] ResponseMappingPopup is not assigned.");
                yield break;
            }

            Debug.LogError("[CYBER FLOW] Activating ResponseMappingPopup.");

            ForceActivateHierarchy(m_ResponseMappingPopup);

            m_ResponseMappingWaiting = true;
            m_ResponseMappingLeftPressed = false;
            m_ResponseMappingRightPressed = false;

            if (m_ResponseMappingDemo != null)
                m_ResponseMappingDemo.ResetDemo();
            else
                Debug.LogError("[CYBER FLOW] ResponseMappingDemo component is not assigned.");

            while (m_ResponseMappingWaiting)
                yield return null;

            yield return new WaitForSeconds(m_ResponseMappingAdvanceDelay);

            m_ResponseMappingPopup.SetActive(false);
        }

        IEnumerator RunApproachIntroDemo()
        {
            HideAllPanels();

            if (m_ApproachIntroPopup == null)
            {
                Debug.LogError("[CYBER FLOW] ApproachIntroPopup is not assigned.");
                yield break;
            }

            Debug.LogError("[CYBER FLOW] Activating ApproachIntroPopup.");

            ForceActivateHierarchy(m_ApproachIntroPopup);

            if (m_ApproachIntroDemo != null)
            {
                m_ApproachIntroDemo.PlayDemo();

                while (!m_ApproachIntroDemo.Finished)
                    yield return null;
            }
            else
            {
                Debug.LogWarning("[CYBER FLOW] ApproachIntroDemo component is not assigned. Using fallback wait.");
                yield return new WaitForSeconds(m_ApproachIntroFallbackSeconds);
            }

            m_ApproachIntroPopup.SetActive(false);
        }

        void ForceActivateHierarchy(GameObject obj)
        {
            if (obj == null) return;

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
            m_WaitingForPanelAdvance = false;
        }

        IEnumerator RunPracticeYes()
        {
            HideAllPanels();

            if (m_PracticeYesPopup == null)
            {
                Debug.LogError("[CYBER FLOW] PracticeYesPopup is not assigned.");
                yield break;
            }

            ForceActivateHierarchy(m_PracticeYesPopup);

            m_PracticeReceived = CyberYesNoResponse.None;
            m_PracticeWaiting = true;

            while (m_PracticeReceived != CyberYesNoResponse.Yes)
                yield return null;

            m_PracticeWaiting = false;
            m_PracticeYesPopup.SetActive(false);

            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator RunPracticeNo()
        {
            HideAllPanels();

            if (m_PracticeNoPopup == null)
            {
                Debug.LogError("[CYBER FLOW] PracticeNoPopup is not assigned.");
                yield break;
            }

            ForceActivateHierarchy(m_PracticeNoPopup);

            m_PracticeReceived = CyberYesNoResponse.None;
            m_PracticeWaiting = true;

            while (m_PracticeReceived != CyberYesNoResponse.No)
                yield return null;

            m_PracticeWaiting = false;
            m_PracticeNoPopup.SetActive(false);

            yield return new WaitForSeconds(0.3f);
        }

        public void PracticePressYes()
        {
            Debug.LogError("[APP INPUT] PracticePressYes called.");

            if (m_TriggerDemoWaiting)
            {
                Debug.LogError("[TRIGGER DEMO] LEFT trigger pressed.");

                m_TriggerDemoLeftPressed = true;
                m_TriggerDemo?.LeftTriggerPressed();

                if (m_TriggerDemoLeftPressed && m_TriggerDemoRightPressed)
                {
                    Debug.LogError("[TRIGGER DEMO] Both triggers pressed. Advancing.");
                    m_TriggerDemoWaiting = false;
                }

                return;
            }

            if (m_ResponseMappingWaiting)
            {
                Debug.LogError("[RESPONSE MAPPING] LEFT trigger = YES.");

                m_ResponseMappingLeftPressed = true;
                m_ResponseMappingDemo?.LeftPressed();

                if (m_ResponseMappingLeftPressed && m_ResponseMappingRightPressed)
                {
                    Debug.LogError("[RESPONSE MAPPING] Both mappings pressed. Advancing.");
                    m_ResponseMappingWaiting = false;
                }

                return;
            }

            if (!m_PracticeWaiting) return;

            Debug.LogError("[CYBER PRACTICE] YES pressed.");

            m_PracticeReceived = CyberYesNoResponse.Yes;
            m_EegMarkerEmitter?.Emit("cyberfish_practice_yes");
        }

        public void PracticePressNo()
        {
            Debug.LogError("[APP INPUT] PracticePressNo called.");

            if (m_TriggerDemoWaiting)
            {
                Debug.LogError("[TRIGGER DEMO] RIGHT trigger pressed.");

                m_TriggerDemoRightPressed = true;
                m_TriggerDemo?.RightTriggerPressed();

                if (m_TriggerDemoLeftPressed && m_TriggerDemoRightPressed)
                {
                    Debug.LogError("[TRIGGER DEMO] Both triggers pressed. Advancing.");
                    m_TriggerDemoWaiting = false;
                }

                return;
            }

            if (m_ResponseMappingWaiting)
            {
                Debug.LogError("[RESPONSE MAPPING] RIGHT trigger = NO.");

                m_ResponseMappingRightPressed = true;
                m_ResponseMappingDemo?.RightPressed();

                if (m_ResponseMappingLeftPressed && m_ResponseMappingRightPressed)
                {
                    Debug.LogError("[RESPONSE MAPPING] Both mappings pressed. Advancing.");
                    m_ResponseMappingWaiting = false;
                }

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
            if (m_PracticeIntroPopup != null) m_PracticeIntroPopup.SetActive(false);
            if (m_TriggerDemoPopup != null) m_TriggerDemoPopup.SetActive(false);
            if (m_ResponseMappingPopup != null) m_ResponseMappingPopup.SetActive(false);
            if (m_ApproachIntroPopup != null) m_ApproachIntroPopup.SetActive(false);
            if (m_IntroTaskPopup != null) m_IntroTaskPopup.SetActive(false);
            if (m_ControllerGuidePopup != null) m_ControllerGuidePopup.SetActive(false);
            if (m_PracticeYesPopup != null) m_PracticeYesPopup.SetActive(false);
            if (m_PracticeNoPopup != null) m_PracticeNoPopup.SetActive(false);
            if (m_ReadyPopup != null) m_ReadyPopup.SetActive(false);
            if (m_BreakPopup != null) m_BreakPopup.SetActive(false);
            if (m_EndPopup != null) m_EndPopup.SetActive(false);
        }
    }
}