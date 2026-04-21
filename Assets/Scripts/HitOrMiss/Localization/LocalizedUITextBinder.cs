using UnityEngine;
using TMPro;

namespace HitOrMiss
{
    /// <summary>
    /// Binds a TextMeshProUGUI element to a localized term table key.
    /// Call Refresh() or set Language to update.
    /// </summary>
    public class LocalizedUITextBinder : MonoBehaviour
    {
        [SerializeField] LocalizedTermTable m_TermTable;
        [SerializeField] string m_Key;
        [SerializeField] TMP_Text m_Text;

        SupportedLanguage m_Language = SupportedLanguage.English;

        public SupportedLanguage Language
        {
            get => m_Language;
            set
            {
                m_Language = value;
                Refresh();
            }
        }

        public string Key
        {
            get => m_Key;
            set
            {
                m_Key = value;
                Refresh();
            }
        }

        void Start()
        {
            if (m_Text == null)
                m_Text = GetComponent<TMP_Text>();
            Refresh();
        }

        public void Refresh()
        {
            if (m_Text == null || m_TermTable == null || string.IsNullOrEmpty(m_Key))
                return;
            m_Text.text = m_TermTable.Get(m_Key, m_Language);
        }
    }
}
