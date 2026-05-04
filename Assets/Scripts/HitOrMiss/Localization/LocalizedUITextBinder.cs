using UnityEngine;
using TMPro;

namespace HitOrMiss
{
    public class LocalizedUITextBinder : MonoBehaviour
    {
        [SerializeField] LocalizedTermTable m_TermTable;
        [SerializeField] string m_Key;
        [SerializeField] TMP_Text m_Text;

        [Header("Optional override")]
        [SerializeField] bool m_UseManualTextOverride = false;
        [SerializeField] string m_ManualText = "";

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
            if (m_Text == null)
                return;

            if (m_UseManualTextOverride)
            {
                m_Text.text = m_ManualText;
                return;
            }

            if (m_TermTable == null || string.IsNullOrEmpty(m_Key))
                return;

            m_Text.text = m_TermTable.Get(m_Key, m_Language);
        }
    }
}