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

        [Tooltip("Seconds the reminder takes to fade in from 0 → m_NoResponseTargetAlpha.")]
        [SerializeField] float m_NoResponseFadeInSeconds = 0.6f;
        [Tooltip("Seconds the reminder takes to fade out before being hidden.")]
        [SerializeField] float m_NoResponseFadeOutSeconds = 0.6f;
        [Range(0f, 1f)]
        [Tooltip("Peak alpha of the reminder (kept subtle by default).")]
        [SerializeField] float m_NoResponseTargetAlpha = 0.55f;

        // The Image on the panel itself, grabbed automatically in Awake.
        Image m_PanelImage;
        // CanvasGroup used to drive the no-response fade. Created if missing.
        CanvasGroup m_NoResponseCanvasGroup;

        Coroutine m_FlashRoutine;
        Coroutine m_NoResponseFadeRoutine;

        void Awake()
        {
            if (m_PracticeFeedbackPanel != null)
            {
                m_PracticeFeedbackPanel.SetActive(false);

                // Grab the Image on the panel itself rather than a separate field.
                m_PanelImage = m_PracticeFeedbackPanel.GetComponent<Image>();

                // Ensure a CanvasGroup exists so we can fade alpha cleanly
                // without touching the Image's color alpha directly (which the
                // flash routine controls separately).
                m_NoResponseCanvasGroup = m_PracticeFeedbackPanel.GetComponent<CanvasGroup>();
                if (m_NoResponseCanvasGroup == null)
                    m_NoResponseCanvasGroup = m_PracticeFeedbackPanel.AddComponent<CanvasGroup>();
                m_NoResponseCanvasGroup.alpha = 0f;
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
        /// Shows the no-response reminder on the same panel, in neutral white,
        /// fading the CanvasGroup alpha from 0 up to m_NoResponseTargetAlpha so
        /// the message appears subtly rather than popping in. Stays visible
        /// until HideNoResponseMessage is called.
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

            if (m_NoResponseFadeRoutine != null)
                StopCoroutine(m_NoResponseFadeRoutine);
            m_NoResponseFadeRoutine = StartCoroutine(
                FadeNoResponse(m_NoResponseTargetAlpha, m_NoResponseFadeInSeconds, hideOnEnd: false));
        }

        /// <summary>
        /// Hides the no-response reminder by fading the CanvasGroup alpha back
        /// to 0 and then disabling the panel. Safe to call even when no fade
        /// is currently running.
        /// </summary>
        public void HideNoResponseMessage()
        {
            if (m_PracticeFeedbackPanel == null) return;

            if (m_NoResponseFadeRoutine != null)
                StopCoroutine(m_NoResponseFadeRoutine);
            m_NoResponseFadeRoutine = StartCoroutine(
                FadeNoResponse(0f, m_NoResponseFadeOutSeconds, hideOnEnd: true));
        }

        IEnumerator FadeNoResponse(float targetAlpha, float duration, bool hideOnEnd)
        {
            if (m_NoResponseCanvasGroup == null)
            {
                // Fallback: no CanvasGroup available, just hard toggle.
                if (hideOnEnd) m_PracticeFeedbackPanel.SetActive(false);
                if (m_NoResponseLabel != null && hideOnEnd) m_NoResponseLabel.text = string.Empty;
                yield break;
            }

            float start = m_NoResponseCanvasGroup.alpha;
            float t = 0f;
            float safeDuration = Mathf.Max(duration, 0.0001f);

            while (t < safeDuration)
            {
                t += Time.deltaTime;
                m_NoResponseCanvasGroup.alpha =
                    Mathf.Lerp(start, targetAlpha, Mathf.SmoothStep(0f, 1f, t / safeDuration));
                yield return null;
            }
            m_NoResponseCanvasGroup.alpha = targetAlpha;

            if (hideOnEnd)
            {
                m_PracticeFeedbackPanel.SetActive(false);
                if (m_NoResponseLabel != null) m_NoResponseLabel.text = string.Empty;
            }
            m_NoResponseFadeRoutine = null;
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

            // Restore CanvasGroup alpha so the flash isn't faded out from a
            // prior no-response cycle. The Image's alpha is what controls the
            // flash color intensity via SetPanelColor.
            if (m_NoResponseCanvasGroup != null)
                m_NoResponseCanvasGroup.alpha = 1f;

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