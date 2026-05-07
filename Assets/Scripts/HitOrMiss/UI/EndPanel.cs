using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HitOrMiss
{
    /// <summary>
    /// Final participant-facing panel shown when a task ends. Per the spec
    /// it offers two routes:
    ///
    ///   - "Return to Clinician Menu" → load the clinician menu scene (or
    ///     fire <see cref="OnReturnToClinician"/> for a custom hook).
    ///   - "End Session"              → quit / send the participant to the
    ///     final outro and shut down. Default action calls
    ///     <c>Application.Quit</c>; override via <see cref="OnEndSession"/>.
    ///
    /// The text body is driven from a <see cref="TaskPopupPanel"/> on the
    /// same GameObject so localization + auto-advance behaviors are
    /// available if you don't want the buttons.
    /// </summary>
    public class EndPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject m_Root;
        [SerializeField] TMP_Text m_Body;
        [SerializeField] Button m_ReturnToClinicianButton;
        [SerializeField] Button m_EndSessionButton;

        [Header("Localization")]
        [SerializeField] string m_BodyKey = "task_end_body";
        [TextArea(2, 4)]
        [SerializeField] string m_BodyFallback = "You are all done. Thank you for your participation.";

        [Header("Custom hooks (override default behaviors)")]
        public UnityEvent OnReturnToClinician;
        public UnityEvent OnEndSession;

        void Awake()
        {
            if (m_Root == null) m_Root = gameObject;
            if (m_ReturnToClinicianButton != null) m_ReturnToClinicianButton.onClick.AddListener(HandleReturn);
            if (m_EndSessionButton != null) m_EndSessionButton.onClick.AddListener(HandleEnd);
            Hide();
        }

        void OnDestroy()
        {
            if (m_ReturnToClinicianButton != null) m_ReturnToClinicianButton.onClick.RemoveListener(HandleReturn);
            if (m_EndSessionButton != null) m_EndSessionButton.onClick.RemoveListener(HandleEnd);
        }

        public void Show(string text = null)
        {
            if (m_Body != null) m_Body.text = string.IsNullOrEmpty(text) ? m_BodyFallback : text;
            if (m_Root != null) m_Root.SetActive(true);
        }

        public void Hide()
        {
            if (m_Root != null) m_Root.SetActive(false);
        }

        void HandleReturn()
        {
            Debug.Log("[EndPanel] Return to clinician menu pressed.");
            // If a UnityEvent listener is wired in the Inspector, fire it
            // (e.g. SceneManager.LoadScene("ClinicianMenu")). Otherwise the
            // panel just hides itself.
            OnReturnToClinician?.Invoke();
            Hide();
        }

        void HandleEnd()
        {
            Debug.Log("[EndPanel] End session pressed.");
            if (OnEndSession != null && OnEndSession.GetPersistentEventCount() > 0)
            {
                OnEndSession.Invoke();
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            Hide();
        }
    }
}
