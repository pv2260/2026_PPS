using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Opens the Quest system keyboard when a TMP_InputField is selected.
    ///
    /// Works on an Android build running on a Meta Quest headset
    /// (TouchScreenKeyboard.isSupported is true there). It is a no-op in the
    /// Editor on Windows / macOS — that is expected; test on the device.
    ///
    /// The OnSelect event requires the canvas to have a GraphicRaycaster
    /// (or TrackedDeviceGraphicRaycaster for XR pointer input) and the
    /// EventSystem to have an XRUI Input Module — otherwise OnSelect will
    /// never fire when you click the field with a controller ray.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class OpenSystemKeyboardOnSelect : MonoBehaviour, ISelectHandler
    {
        [Tooltip("Log diagnostic info to the console when selected.")]
        [SerializeField] bool m_VerboseLogging = true;

        TMP_InputField m_Field;
        TouchScreenKeyboard m_Keyboard;

        void Awake()
        {
            m_Field = GetComponent<TMP_InputField>();
            // Belt-and-braces: TMP_InputField also exposes an onSelect UnityEvent.
            // Subscribe so the keyboard opens even if ISelectHandler isn't routed.
            if (m_Field != null)
                m_Field.onSelect.AddListener(_ => Open());
        }

        public void OnSelect(BaseEventData eventData) => Open();

        void Open()
        {
            if (m_VerboseLogging)
                Debug.Log($"[OpenSystemKeyboardOnSelect] Select fired. " +
                          $"TouchScreenKeyboard.isSupported={TouchScreenKeyboard.isSupported}, " +
                          $"platform={Application.platform}");

            if (!TouchScreenKeyboard.isSupported)
                return; // Editor on Windows / macOS: no system keyboard. Expected.

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
                m_Keyboard = null;
        }
    }
}
