using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss
{
    /// <summary>
    /// Participant-facing pause panel. Shown when the in-scene STOP button
    /// is pressed during a running block. Offers three actions per the spec:
    ///
    ///   - Resume Next Trial   → continues the block from where it paused
    ///   - Restart Current Block → discards the in-progress block and re-runs it
    ///   - Stop Task           → ends the session entirely (returns to clinician)
    ///
    /// Wires automatically into <see cref="HitOrMissAppController"/>'s
    /// PauseSession / ResumeSession / StopSession + a new RestartCurrentBlock
    /// hook. Shown by subscribing to <c>SessionPaused</c>.
    /// </summary>
    public class ParticipantPausePanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] HitOrMissAppController m_AppController;
        [SerializeField] GameObject m_Root;
        [SerializeField] Button m_ResumeButton;
        [SerializeField] Button m_RestartBlockButton;
        [SerializeField] Button m_StopButton;
        [SerializeField] TMP_Text m_StatusText;

        void Awake()
        {
            if (m_Root == null) m_Root = gameObject;
            if (m_ResumeButton != null) m_ResumeButton.onClick.AddListener(OnResume);
            if (m_RestartBlockButton != null) m_RestartBlockButton.onClick.AddListener(OnRestartBlock);
            if (m_StopButton != null) m_StopButton.onClick.AddListener(OnStop);
            Hide();
        }

        void OnEnable()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused += OnPaused;
                m_AppController.SessionResumed += OnResumed;
                m_AppController.SessionEnded += OnEnded;
            }
        }

        void OnDisable()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused -= OnPaused;
                m_AppController.SessionResumed -= OnResumed;
                m_AppController.SessionEnded -= OnEnded;
            }
        }

        void OnDestroy()
        {
            if (m_ResumeButton != null) m_ResumeButton.onClick.RemoveListener(OnResume);
            if (m_RestartBlockButton != null) m_RestartBlockButton.onClick.RemoveListener(OnRestartBlock);
            if (m_StopButton != null) m_StopButton.onClick.RemoveListener(OnStop);
        }

        void OnPaused()
        {
            if (m_StatusText != null)
                m_StatusText.text = $"Task paused — block {m_AppController.CurrentBlockIndex + 1}.";
            Show();
        }

        void OnResumed() => Hide();
        void OnEnded() => Hide();

        void OnResume()
        {
            if (m_AppController != null) m_AppController.ResumeSession();
            Hide();
        }

        void OnRestartBlock()
        {
            if (m_AppController != null && m_AppController.TaskManager != null)
            {
                Debug.Log("[ParticipantPausePanel] Restart Current Block requested.");
                // Soft restart: stop the current block and re-launch it.
                // The controller's RunSession loop will see m_TaskManager
                // come out of running with TrialsCompletedInBlock < trials,
                // but we'd rather have an explicit hook. Use the existing
                // ResumeSession flow combined with rewinding the manager.
                m_AppController.ResumeSession();
                m_AppController.TaskManager.RestartCurrentBlock();
            }
            Hide();
        }

        void OnStop()
        {
            if (m_AppController != null) m_AppController.StopSession();
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
