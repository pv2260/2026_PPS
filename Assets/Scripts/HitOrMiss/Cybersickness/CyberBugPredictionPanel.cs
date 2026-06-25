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
        [SerializeField] TMP_Text m_YesText;
        [SerializeField] TMP_Text m_NoText;

        [Header("Panels")]
        [SerializeField] Image m_YesPanelImage;
        [SerializeField] Image m_NoPanelImage;

        [Header("Colors")]
        [SerializeField] Color m_NormalColor = Color.white;
        [SerializeField] Color m_SelectedColor = Color.green;

        void Awake()
        {
            Hide();
        }

        public void ShowQuestion(string question)
        {
            if (m_Root != null)
                m_Root.SetActive(true);
            else
                gameObject.SetActive(true);

            if (m_QuestionText != null)
                m_QuestionText.text = question;

            if (m_YesText != null)
                m_YesText.text = "YES";

            if (m_NoText != null)
                m_NoText.text = "NO";

            ResetColors();
        }

        public void HighlightYes()
        {
            Debug.LogError("[PREDICTION PANEL] YES highlighted.");

            ResetColors();

            if (m_YesPanelImage != null)
                m_YesPanelImage.color = m_SelectedColor;
        }

        public void HighlightNo()
        {
            Debug.LogError("[PREDICTION PANEL] NO highlighted.");

            ResetColors();

            if (m_NoPanelImage != null)
                m_NoPanelImage.color = m_SelectedColor;
        }

        public void Hide()
        {
            ResetColors();

            if (m_Root != null)
                m_Root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        void ResetColors()
        {
            if (m_YesPanelImage != null)
                m_YesPanelImage.color = m_NormalColor;

            if (m_NoPanelImage != null)
                m_NoPanelImage.color = m_NormalColor;
        }
    }
}