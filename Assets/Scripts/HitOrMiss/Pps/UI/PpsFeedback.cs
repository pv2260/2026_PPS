using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HitOrMiss.Pps
{
    public class PpsFeedback : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] AudioSource m_AudioSource;

        [Tooltip("Played on every accepted response, practice and main trials.")]
        [SerializeField] AudioClip m_ResponseSound;

        [Header("Practice feedback flash")]
        [Tooltip("Panel used for green/red practice feedback and no-response reminder.")]
        [SerializeField] GameObject m_PracticeFeedbackPanel;

        [SerializeField] float m_FlashDurationSeconds = 0.35f;

        [SerializeField, Range(0f, 1f)]
        float m_FlashAlpha = 0.75f;

        [Header("No-response reminder")]
        [Tooltip("TMP_Text child inside PracticeFeedbackPanel.")]
        [SerializeField] TMP_Text m_NoResponseLabel;

        [SerializeField] string m_NoResponseText = "You did not respond.\nPress the button when you feel the vibration.";

        // The Image on the panel itself, grabbed automatically in Awake.
        Image m_PanelImage;

        Coroutine m_FlashRoutine;

        void Awake()
        {
            if (m_PracticeFeedbackPanel != null)
            {
                m_PracticeFeedbackPanel.SetActive(false);

                // Grab the Image on the panel itself rather than a separate field.
                m_PanelImage = m_PracticeFeedbackPanel.GetComponent<Image>();
            }

            if (m_NoResponseLabel != null)
                m_NoResponseLabel.text = string.Empty;
        }

        /// <summary>
        /// Call on every press — plays the confirmation sound.
        /// </summary>
        public void OnResponseSubmitted()
        {
            if (m_AudioSource != null && m_ResponseSound != null)
                m_AudioSource.PlayOneShot(m_ResponseSound);
        }

        /// <summary>
        /// Green flash for correct vibration detection (practice only).
        /// </summary>
        public void FlashGreen() => Flash(Color.green);

        /// <summary>
        /// Red flash for miss or false alarm (practice only).
        /// </summary>
        public void FlashRed() => Flash(Color.red);

        /// <summary>
        /// Shows the no-response reminder on the same panel, in neutral white.
        /// Stays visible until HideNoResponseMessage is called.
        /// </summary>
        public void ShowNoResponseMessage()
        {
            if (m_PracticeFeedbackPanel == null)
                return;

            // Stop any running flash so it doesn't hide the panel mid-reminder.
            if (m_FlashRoutine != null)
            {
                StopCoroutine(m_FlashRoutine);
                m_FlashRoutine = null;
            }

            if (m_NoResponseLabel != null)
                m_NoResponseLabel.text = m_NoResponseText;

            SetPanelColor(Color.white);
            m_PracticeFeedbackPanel.SetActive(true);
        }

        /// <summary>
        /// Hides the no-response reminder.
        /// </summary>
        public void HideNoResponseMessage()
        {
            if (m_PracticeFeedbackPanel != null)
                m_PracticeFeedbackPanel.SetActive(false);

            if (m_NoResponseLabel != null)
                m_NoResponseLabel.text = string.Empty;
        }

        void Flash(Color color)
        {
            if (m_PracticeFeedbackPanel == null)
                return;

            if (m_FlashRoutine != null)
                StopCoroutine(m_FlashRoutine);

            m_FlashRoutine = StartCoroutine(FlashRoutine(color));
        }

        IEnumerator FlashRoutine(Color color)
        {
            // Clear any leftover no-response text before showing hit/miss color.
            if (m_NoResponseLabel != null)
                m_NoResponseLabel.text = string.Empty;

            SetPanelColor(color);
            m_PracticeFeedbackPanel.SetActive(true);

            yield return new WaitForSeconds(m_FlashDurationSeconds);

            if (m_PracticeFeedbackPanel != null)
                m_PracticeFeedbackPanel.SetActive(false);

            m_FlashRoutine = null;
        }

        void SetPanelColor(Color color)
        {
            if (m_PanelImage == null)
                return;

            color.a = m_FlashAlpha;
            m_PanelImage.color = color;
        }
    }
}