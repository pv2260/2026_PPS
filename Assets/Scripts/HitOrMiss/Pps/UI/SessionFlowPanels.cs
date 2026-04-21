using System.Collections;
using UnityEngine;
using TMPro;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Owns the non-trial UI panels: Subject ID, Instructions, Practice intro,
    /// Block intro, Rest, Outro. Exposes coroutine-friendly show/wait helpers
    /// so <see cref="PpsTaskManager"/> can advance phases cleanly.
    /// </summary>
    public class SessionFlowPanels : MonoBehaviour
    {
        [Header("Subject ID")]
        [SerializeField] GameObject m_SubjectIdPanel;
        [SerializeField] TMP_InputField m_SubjectIdInput;

        [Header("Text panels with Continue buttons")]
        [SerializeField] GameObject m_InstructionsPanel;
        [SerializeField] GameObject m_PracticeIntroPanel;
        [SerializeField] GameObject m_BlockIntroPanel;

        [Header("Auto-advance panels")]
        [SerializeField] GameObject m_RestPanel;
        [SerializeField] GameObject m_OutroPanel;

        [Header("Optional localized text slots (one per panel)")]
        [SerializeField] TMP_Text m_InstructionsText;
        [SerializeField] TMP_Text m_PracticeIntroText;
        [SerializeField] TMP_Text m_BlockIntroText;
        [SerializeField] TMP_Text m_OutroText;

        bool m_WaitingForContinue;
        string m_SubjectId = "";

        public string SubjectId => m_SubjectId;

        void Awake()
        {
            HideAll();
        }

        public void HideAll()
        {
            SetActive(m_SubjectIdPanel, false);
            SetActive(m_InstructionsPanel, false);
            SetActive(m_PracticeIntroPanel, false);
            SetActive(m_BlockIntroPanel, false);
            SetActive(m_RestPanel, false);
            SetActive(m_OutroPanel, false);
        }

        // ---- Subject ID ----

        public IEnumerator ShowSubjectIdAndWait()
        {
            SetActive(m_SubjectIdPanel, true);
            m_WaitingForContinue = true;
            while (m_WaitingForContinue) yield return null;
            SetActive(m_SubjectIdPanel, false);
        }

        /// <summary>Wire this to the Continue button inside the Subject ID panel.</summary>
        public void OnSubjectIdContinue()
        {
            if (m_SubjectIdInput != null) m_SubjectId = m_SubjectIdInput.text;
            m_WaitingForContinue = false;
        }

        // ---- Continue-style panels ----

        public IEnumerator ShowAndWait(GameObject panel, TMP_Text textSlot = null, string text = null)
        {
            if (panel == null) yield break;
            if (textSlot != null && text != null) textSlot.text = text;
            panel.SetActive(true);
            m_WaitingForContinue = true;
            while (m_WaitingForContinue) yield return null;
            panel.SetActive(false);
        }

        public IEnumerator ShowInstructionsAndWait(string text = null)
            => ShowAndWait(m_InstructionsPanel, m_InstructionsText, text);

        public IEnumerator ShowPracticeIntroAndWait(string text = null)
            => ShowAndWait(m_PracticeIntroPanel, m_PracticeIntroText, text);

        public IEnumerator ShowBlockIntroAndWait(string text = null)
            => ShowAndWait(m_BlockIntroPanel, m_BlockIntroText, text);

        /// <summary>Wire this to every "Continue" button on Instructions / Practice / Block panels.</summary>
        public void OnContinue()
        {
            m_WaitingForContinue = false;
        }

        // ---- Auto-advance panels ----

        public IEnumerator ShowRestAndAutoAdvance(float seconds)
        {
            SetActive(m_RestPanel, true);
            yield return new WaitForSeconds(seconds);
            SetActive(m_RestPanel, false);
        }

        public IEnumerator ShowOutro(string text = null, float holdSeconds = 5f)
        {
            if (m_OutroText != null && text != null) m_OutroText.text = text;
            SetActive(m_OutroPanel, true);
            yield return new WaitForSeconds(holdSeconds);
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null) go.SetActive(on);
        }
    }
}
