using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Pps
{
    public class PpsFeedback : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] AudioSource m_AudioSource;

        [Tooltip("Played on every accepted response, practice and main trials.")]
        [SerializeField] AudioClip m_ResponseSound;

        [Header("Practice feedback flash")]
        [Tooltip("Panel used for green/red practice feedback.")]
        [SerializeField] GameObject m_PracticeFeedbackPanel;

        [Tooltip("Image component on the practice feedback panel.")]
        [SerializeField] Image m_PracticeFeedbackImage;

        [SerializeField] float m_FlashDurationSeconds = 0.35f;

        [SerializeField, Range(0f, 1f)]
        float m_FlashAlpha = 0.75f;

        Coroutine m_FlashRoutine;

        void Awake()
        {
            if (m_PracticeFeedbackPanel != null)
                m_PracticeFeedbackPanel.SetActive(false);

            if (m_PracticeFeedbackImage == null && m_PracticeFeedbackPanel != null)
                m_PracticeFeedbackImage = m_PracticeFeedbackPanel.GetComponent<Image>();
        }

        /// <summary>
        /// Call immediately when a valid response is registered.
        /// This only plays the small confirmation sound.
        /// </summary>
        public void OnResponseSubmitted()
        {
            if (m_AudioSource != null && m_ResponseSound != null)
                m_AudioSource.PlayOneShot(m_ResponseSound);
        }

        /// <summary>
        /// Green feedback for correct vibration detection.
        /// </summary>
        public void FlashGreen()
        {
            Flash(Color.green);
        }

        /// <summary>
        /// Red feedback for missed vibration or false alarm.
        /// </summary>
        public void FlashRed()
        {
            Flash(Color.red);
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
            m_PracticeFeedbackPanel.SetActive(true);

            if (m_PracticeFeedbackImage != null)
            {
                color.a = m_FlashAlpha;
                m_PracticeFeedbackImage.color = color;
            }

            yield return new WaitForSeconds(m_FlashDurationSeconds);

            if (m_PracticeFeedbackPanel != null)
                m_PracticeFeedbackPanel.SetActive(false);

            m_FlashRoutine = null;
        }
    }
}

