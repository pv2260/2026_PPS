using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Pps
{
    public class PpsParticipantPausePanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PpsAppController m_AppController;
        [SerializeField] private GameObject m_Root;
        [SerializeField] private GameObject m_PauseAccessButtonRoot;

        [Header("Buttons")]
        [SerializeField] private Button m_ResumeNextTrialButton;
        [SerializeField] private Button m_RestartCurrentBlockButton;
        [SerializeField] private Button m_StopButton;

        [Header("Text")]
        [SerializeField] private TMP_Text m_StatusText;

        private void Awake()
        {
            if (m_Root == null)
                m_Root = gameObject;

            if (m_ResumeNextTrialButton != null)
                m_ResumeNextTrialButton.onClick.AddListener(OnResumeNextTrial);

            if (m_RestartCurrentBlockButton != null)
                m_RestartCurrentBlockButton.onClick.AddListener(OnRestartCurrentBlock);

            if (m_StopButton != null)
                m_StopButton.onClick.AddListener(OnStopTask);

            Hide();
        }

        private void OnEnable()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused += OnPaused;
                m_AppController.SessionResumed += OnResumed;
                m_AppController.SessionEnded += OnEnded;
            }
        }

        private void OnDisable()
        {
            if (m_AppController != null)
            {
                m_AppController.SessionPaused -= OnPaused;
                m_AppController.SessionResumed -= OnResumed;
                m_AppController.SessionEnded -= OnEnded;
            }
        }

        private void OnDestroy()
        {
            if (m_ResumeNextTrialButton != null)
                m_ResumeNextTrialButton.onClick.RemoveListener(OnResumeNextTrial);

            if (m_RestartCurrentBlockButton != null)
                m_RestartCurrentBlockButton.onClick.RemoveListener(OnRestartCurrentBlock);

            if (m_StopButton != null)
                m_StopButton.onClick.RemoveListener(OnStopTask);
        }

        public void RequestPause()
        {
            if (m_AppController != null)
                m_AppController.RequestParticipantPause();
            else
                Show();
        }

        private void OnPaused()
        {
            Show();
        }

        private void OnResumed()
        {
            Hide();
        }

        private void OnEnded()
        {
            Hide();
        }

        public void Show()
        {
            if (m_StatusText != null)
                m_StatusText.text = "Task paused. Choose how to continue.";

            if (m_PauseAccessButtonRoot != null)
                m_PauseAccessButtonRoot.SetActive(false);

            if (m_Root != null)
                m_Root.SetActive(true);
        }

        public void Hide()
        {
            if (m_Root != null)
                m_Root.SetActive(false);
        }

        private void OnResumeNextTrial()
        {
            if (m_AppController != null)
                m_AppController.ResumeFromParticipantPauseNextTrial();
            else
                Hide();
        }

        private void OnRestartCurrentBlock()
        {
            if (m_AppController != null)
                m_AppController.RestartCurrentBlockFromParticipantPause();

            Hide();
        }

        private void OnStopTask()
        {
            if (m_AppController != null)
                m_AppController.StopTaskFromParticipantPause();

            Hide();
        }
    }
}