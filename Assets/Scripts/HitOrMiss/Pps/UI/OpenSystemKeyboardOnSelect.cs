using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

namespace HitOrMiss.Pps
{
    [RequireComponent(typeof(TMP_InputField))]
    public class OpenSystemKeyboardOnSelect : MonoBehaviour, ISelectHandler
    {
        [SerializeField] bool m_VerboseLogging = true;

        TMP_InputField m_Field;
        TouchScreenKeyboard m_Keyboard;

        void Awake()
        {
            m_Field = GetComponent<TMP_InputField>();

            if (m_Field != null)
                m_Field.onSelect.AddListener(_ => Open());
        }

        public void OnSelect(BaseEventData eventData)
        {
            Open();
        }

        void Open()
        {
            if (m_VerboseLogging)
            {
                Debug.Log($"[OpenSystemKeyboardOnSelect] Select fired. " +
                          $"TouchScreenKeyboard.isSupported={TouchScreenKeyboard.isSupported}, " +
                          $"platform={Application.platform}");
            }

            if (!TouchScreenKeyboard.isSupported)
                return;

            if (m_Keyboard != null && m_Keyboard.active)
                return;

            m_Keyboard = TouchScreenKeyboard.Open(
                m_Field != null ? m_Field.text : string.Empty,
                TouchScreenKeyboardType.Default);
        }

        void Update()
        {
            if (m_Keyboard == null || m_Field == null) return;

            m_Field.text = m_Keyboard.text;

            if (m_Keyboard.status == TouchScreenKeyboard.Status.Done ||
                m_Keyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                m_Keyboard = null;
            }
        }
    }
}