using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Cybersickness
{
    public class CyberBugPredictionPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] GameObject m_Root;

        [Header("Text")]
        [SerializeField] TMP_Text m_QuestionText;
        [SerializeField] TMP_Text m_LeftText;
        [SerializeField] TMP_Text m_RightText;

        [Header("Side panels")]
        [SerializeField] Image m_LeftPanel;
        [SerializeField] Image m_RightPanel;

        [Header("Colors")]
        [SerializeField] Color m_NormalColor = new Color(1f, 1f, 1f, 0.20f);
        [SerializeField] Color m_SelectedColor = new Color(1f, 1f, 1f, 0.75f);

        void Awake()
        {
            if (m_Root == null)
                m_Root = gameObject;

            Hide();
        }

        public void ShowQuestion(string question = "Will the bug touch you?")
        {
            if (m_Root != null)
                m_Root.SetActive(true);

            if (m_QuestionText != null)
                m_QuestionText.text = question;

            if (m_LeftText != null)
                m_LeftText.text = "YES";

            if (m_RightText != null)
                m_RightText.text = "NO";

            ClearSelection();
        }

        public void Hide()
        {
            if (m_Root != null)
                m_Root.SetActive(false);
        }

        public void HighlightYes()
        {
            if (m_LeftPanel != null)
                m_LeftPanel.color = m_SelectedColor;

            if (m_RightPanel != null)
                m_RightPanel.color = m_NormalColor;
        }

        public void HighlightNo()
        {
            if (m_LeftPanel != null)
                m_LeftPanel.color = m_NormalColor;

            if (m_RightPanel != null)
                m_RightPanel.color = m_SelectedColor;
        }

        public void ClearSelection()
        {
            if (m_LeftPanel != null)
                m_LeftPanel.color = m_NormalColor;

            if (m_RightPanel != null)
                m_RightPanel.color = m_NormalColor;
        }
    }
}