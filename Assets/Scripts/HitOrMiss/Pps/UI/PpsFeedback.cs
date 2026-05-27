using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Plays response feedback. During practice, also shows the
    /// PracticeFeedbackPanel briefly ("Felt it!").
    /// Call OnResponseSubmitted(trial) whenever a response is registered.
    /// </summary>
    public class PpsFeedback : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] AudioSource m_AudioSource;
        [Tooltip("Played on every response (practice and main trials).")]
        [SerializeField] AudioClip m_ResponseSound;

        [Header("Practice feedback panel")]
        [Tooltip("Reference to PracticeFeedbackPanel in the scene. Toggled on/off.")]
        [SerializeField] GameObject m_PracticeFeedbackPanel;

        [Tooltip("How long the 'Felt it!' panel stays visible after a response.")]
        [SerializeField] float m_PanelDurationSeconds = 0.8f;

        Coroutine m_HideRoutine;

        void Awake()
        {
            // Make sure the panel starts hidden
            if (m_PracticeFeedbackPanel != null)
                m_PracticeFeedbackPanel.SetActive(false);
        }

        /// <summary>
        /// Call this whenever a response (button press) is registered during a trial.
        /// </summary>
        public void OnResponseSubmitted(PpsTrialDefinition trial)
        {
            // Always: play the response sound
            if (m_AudioSource != null && m_ResponseSound != null)
                m_AudioSource.PlayOneShot(m_ResponseSound);

            // Practice-only: also show the "Felt it!" panel
            if (trial.isPractice)
                ShowPracticePanel();
        }

        void ShowPracticePanel()
        {
            if (m_PracticeFeedbackPanel == null) return;

            m_PracticeFeedbackPanel.SetActive(true);

            if (m_HideRoutine != null) StopCoroutine(m_HideRoutine);
            m_HideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(m_PanelDurationSeconds);
            if (m_PracticeFeedbackPanel != null)
                m_PracticeFeedbackPanel.SetActive(false);
            m_HideRoutine = null;
        }
    }
}