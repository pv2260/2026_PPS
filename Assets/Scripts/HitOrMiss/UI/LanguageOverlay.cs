using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss
{
    /// <summary>
    /// Minimal participant-facing language picker — English / French.
    /// Shown by the WelcomePanel's "Language Settings" button. Selecting a
    /// language calls <see cref="HitOrMissAppController.SetLanguage"/> so
    /// every <see cref="LocalizedUITextBinder"/> in the scene re-resolves
    /// against the new <see cref="SupportedLanguage"/> immediately.
    ///
    /// Designed as a tiny modal: open, pick, close. No persistence beyond
    /// the running session — if you want the choice to survive a restart,
    /// stash <see cref="HitOrMissAppController.CurrentLanguage"/> in
    /// PlayerPrefs (left as a follow-up).
    /// </summary>
    public class LanguageOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] HitOrMissAppController m_AppController;
        [SerializeField] GameObject m_Root;
        [SerializeField] Button m_OpenButton;
        [SerializeField] Button m_CloseButton;
        [SerializeField] Button m_EnglishButton;
        [SerializeField] Button m_FrenchButton;
        [SerializeField] TMP_Text m_CurrentLanguageLabel;

        void Awake()
        {
            if (m_Root == null) m_Root = gameObject;
            if (m_OpenButton != null) m_OpenButton.onClick.AddListener(Open);
            if (m_CloseButton != null) m_CloseButton.onClick.AddListener(Close);
            if (m_EnglishButton != null) m_EnglishButton.onClick.AddListener(SelectEnglish);
            if (m_FrenchButton != null) m_FrenchButton.onClick.AddListener(SelectFrench);
            Close();
        }

        void OnDestroy()
        {
            if (m_OpenButton != null) m_OpenButton.onClick.RemoveListener(Open);
            if (m_CloseButton != null) m_CloseButton.onClick.RemoveListener(Close);
            if (m_EnglishButton != null) m_EnglishButton.onClick.RemoveListener(SelectEnglish);
            if (m_FrenchButton != null) m_FrenchButton.onClick.RemoveListener(SelectFrench);
        }

        public void Open()
        {
            if (m_Root != null) m_Root.SetActive(true);
            UpdateLabel();
        }

        public void Close()
        {
            if (m_Root != null) m_Root.SetActive(false);
        }

        void SelectEnglish() => Apply(SupportedLanguage.English);
        void SelectFrench()  => Apply(SupportedLanguage.French);

        void Apply(SupportedLanguage language)
        {
            if (m_AppController != null) m_AppController.SetLanguage(language);
            UpdateLabel();
            Close();
        }

        void UpdateLabel()
        {
            if (m_CurrentLanguageLabel == null || m_AppController == null) return;
            m_CurrentLanguageLabel.text = m_AppController.CurrentLanguage.ToString();
        }
    }
}
