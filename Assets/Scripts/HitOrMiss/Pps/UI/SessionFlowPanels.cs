using System.Collections;
using UnityEngine;
using TMPro;

namespace HitOrMiss.Pps
{
    public class SessionFlowPanels : MonoBehaviour
    {
        [Header("Subject ID")]
        [SerializeField] GameObject m_SubjectIdPanel;
        [SerializeField] TMP_InputField m_SubjectIdInput;

        [Header("Text panels with Continue buttons")]
        [SerializeField] GameObject m_InstructionsPanel;
        [SerializeField] GameObject m_PracticeIntroPanel;
        [SerializeField] GameObject m_BlockIntroPanel;

        [Header("Practice Runtime")]
        [SerializeField] GameObject m_PracticeRuntimePanel;
        [SerializeField] TMP_Text m_TrialStatusText;

        [Header("Feedback")]
        [SerializeField] TMP_Text m_FeedbackText;
        [SerializeField] float m_FeedbackDurationSeconds = 0.5f;

        Coroutine m_FeedbackRoutine;

        [Header("Auto-advance panels")]
        [SerializeField] GameObject m_RestPanel;
        [SerializeField] GameObject m_OutroPanel;

        [Header("Optional localized text slots")]
        [SerializeField] TMP_Text m_InstructionsText;
        [SerializeField] TMP_Text m_PracticeIntroText;
        [SerializeField] TMP_Text m_BlockIntroText;
        [SerializeField] TMP_Text m_OutroText;

        bool m_WaitingForContinue;
        string m_SubjectId = "";

        public string SubjectId => m_SubjectId;

        void Awake()
        {
            m_WaitingForContinue = false;
            HideAll();
            
        }

        public void HideAll()
        {
            SetActive(m_SubjectIdPanel, false);
            SetActive(m_InstructionsPanel, false);
            SetActive(m_PracticeIntroPanel, false);
            SetActive(m_PracticeRuntimePanel, false);
            SetActive(m_BlockIntroPanel, false);
            SetActive(m_RestPanel, false);
            SetActive(m_OutroPanel, false);
        }

        public IEnumerator ShowSubjectIdAndWait()
        {
            Debug.Log("[SessionFlowPanels] Showing Subject ID only.");

            HideAll();

            SetActive(m_SubjectIdPanel, true);
            SetActive(m_PracticeIntroPanel, false);
            SetActive(m_PracticeRuntimePanel, false);

            m_WaitingForContinue = true;

            while (m_WaitingForContinue)
                yield return null;

            SetActive(m_SubjectIdPanel, false);
            HideAll();
        }
        public void OnSubjectIdContinue()
        {
            Debug.Log("[SessionFlowPanels] Subject ID continue pressed.");

            if (m_SubjectIdInput != null)
                m_SubjectId = m_SubjectIdInput.text;

            m_WaitingForContinue = false;
            HideAll();
        }

        public IEnumerator ShowAndWait(GameObject panel, TMP_Text textSlot = null, string text = null)
        {
            HideAll();

            if (panel == null)
            {
                Debug.LogWarning("[SessionFlowPanels] Tried to show a null panel.");
                yield break;
            }

            if (textSlot != null && text != null)
                textSlot.text = text;

            panel.SetActive(true);
            m_WaitingForContinue = true;

            while (m_WaitingForContinue)
                yield return null;

            panel.SetActive(false);
            HideAll();
        }

        public IEnumerator ShowInstructionsAndWait(string text = null)
        {
            yield return ShowAndWait(m_InstructionsPanel, m_InstructionsText, text);
        }

        public IEnumerator ShowPracticeIntroAndWait(string text = null)
        {
            Debug.Log("[SessionFlowPanels] Showing PracticeIntroPanel.");

            HideAll();

            if (m_PracticeIntroPanel == null)
            {
                Debug.LogError("[SessionFlowPanels] PracticeIntroPanel is not assigned.");
                yield break;
            }

            if (m_PracticeIntroText != null && text != null)
                m_PracticeIntroText.text = text;

            m_PracticeIntroPanel.SetActive(true);

            m_WaitingForContinue = true;

            while (m_WaitingForContinue)
                yield return null;

            m_PracticeIntroPanel.SetActive(false);
        }

        public IEnumerator ShowPracticeRuntimeAndWait(string message)
        {
            HideAll();

            SetActive(m_PracticeRuntimePanel, true);

            if (m_TrialStatusText != null)
                m_TrialStatusText.text = message;

            m_WaitingForContinue = true;

            while (m_WaitingForContinue)
                yield return null;
        }



        public void ShowTrialStatus(string message)
        {
            Debug.Log("[SessionFlowPanels] ShowTrialStatus: " + message);

            HideAll();

            SetActive(m_PracticeRuntimePanel, true);

            if (m_TrialStatusText == null)
            {
                Debug.LogError("[SessionFlowPanels] TrialStatusText is not assigned.");
                return;
            }

            m_TrialStatusText.text = "";
            m_TrialStatusText.text = message;
        }

        public void HideTrialStatus()
        {
            SetActive(m_PracticeRuntimePanel, false);
        }

        public void ShowFeedback(string message)
        {
            if (m_FeedbackText == null)
            {
                Debug.Log($"[SessionFlowPanels] Feedback: {message}");
                return;
            }

            if (m_FeedbackRoutine != null)
                StopCoroutine(m_FeedbackRoutine);

            m_FeedbackRoutine = StartCoroutine(ShowFeedbackRoutine(message));
        }

        IEnumerator ShowFeedbackRoutine(string message)
        {
            m_FeedbackText.text = message;
            m_FeedbackText.gameObject.SetActive(true);

            yield return new WaitForSeconds(m_FeedbackDurationSeconds);

            m_FeedbackText.gameObject.SetActive(false);
            m_FeedbackRoutine = null;
        }
        public IEnumerator ShowBlockIntroAndWait(string text = null)
        {
            yield return ShowAndWait(m_BlockIntroPanel, m_BlockIntroText, text);
        }

        public void OnContinue()
        {
            Debug.Log("[SessionFlowPanels] Continue pressed.");
            m_WaitingForContinue = false;
        }

        public IEnumerator ShowRestAndAutoAdvance(float seconds)
        {
            HideAll();

            SetActive(m_RestPanel, true);
            yield return new WaitForSeconds(seconds);
            SetActive(m_RestPanel, false);

            HideAll();
        }

        public IEnumerator ShowOutro(string text = null, float holdSeconds = 5f)
        {
            HideAll();

            if (m_OutroText != null && text != null)
                m_OutroText.text = text;

            SetActive(m_OutroPanel, true);
            yield return new WaitForSeconds(holdSeconds);
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null)
                go.SetActive(on);
        }
    }
}