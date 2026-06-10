using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Headset-side pause panel for Task 1 (PPS). Mirrors the Task 2
    /// ParticipantPausePanel: shows when the session pauses (either from the
    /// clinician panel or from the network), offers Resume and Stop, and
    /// auto-hides when the session resumes or ends.
    ///
    /// Wire m_AppController to the PPSAppController in the scene and assign
    /// the button GameObjects on the panel root. Restart-block is intentionally
    /// omitted: PPS does not yet support mid-session block restart.
    /// </summary>
    public class PpsParticipantPausePanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PpsAppController m_AppController;
        [SerializeField] GameObject m_Root;
        [SerializeField] Button m_ResumeButton;
        [SerializeField] Button m_StopButton;
        [SerializeField] TMP_Text m_StatusText;

        void Awake()
        {
            if (m_Root == null) m_Root = gameObject;
            if (m_ResumeButton != null) m_ResumeButton.onClick.AddListener(OnResume);
            if (m_StopButton   != null) m_StopButton.onClick.AddListener(OnStop);
            Hide();
        }

        void OnEnable()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused  += OnPaused;
                m_AppController.SessionResumed += OnResumed;
                m_AppController.SessionEnded   += OnEnded;
            }
        }

        void OnDisable()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused  -= OnPaused;
                m_AppController.SessionResumed -= OnResumed;
                m_AppController.SessionEnded   -= OnEnded;
            }
        }

        void OnDestroy()
        {
            if (m_ResumeButton != null) m_ResumeButton.onClick.RemoveListener(OnResume);
            if (m_StopButton   != null) m_StopButton.onClick.RemoveListener(OnStop);
        }

        void OnPaused()
        {
            if (m_StatusText != null)
                m_StatusText.text = "Task paused.\nWaiting for clinician.";
            Show();
        }

        void OnResumed() => Hide();
        void OnEnded()   => Hide();

        void OnResume()
        {
            if (m_AppController != null) m_AppController.ResumeSession();
            Hide();
        }

        void OnStop()
        {
            if (m_AppController != null) m_AppController.RequestStop();
            Hide();
        }

        public void Show()
        {
            if (m_Root != null) m_Root.SetActive(true);
        }

        public void Hide()
        {
            if (m_Root != null) m_Root.SetActive(false);
        }
    }
}
