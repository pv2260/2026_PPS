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

        [Header("No-response reminder panel")]
        [Tooltip("Panel used only for no-response reminder.")]
        [SerializeField] GameObject m_PracticeFeedbackPanel;

        [Header("No-response reminder")]
        [Tooltip("TMP_Text child inside PracticeFeedbackPanel.")]
        [SerializeField] TMP_Text m_NoResponseLabel;

        [SerializeField] string m_NoResponseText =
            "Please press the button when you feel the vibration.";

        [Tooltip("Seconds the reminder takes to fade in.")]
        [SerializeField] float m_NoResponseFadeInSeconds = 0.3f;

        [Tooltip("Seconds the reminder takes to fade out before being hidden.")]
        [SerializeField] float m_NoResponseFadeOutSeconds = 0.3f;

        [Range(0f, 1f)]
        [Tooltip("Peak alpha of the no-response reminder.")]
        [SerializeField] float m_NoResponseTargetAlpha = 1f;

        [Header("No-response background")]
        [Tooltip("Background alpha behind the no-response text. Higher = more visible.")]
        [SerializeField, Range(0f, 1f)] float m_NoResponseBackgroundAlpha = 0.85f;

        [Tooltip("Background color behind the no-response text.")]
        [SerializeField] Color m_NoResponseBackgroundColor = Color.black;

        [Tooltip("Text color for the no-response message.")]
        [SerializeField] Color m_NoResponseTextColor = Color.white;

        Image m_PanelImage;
        CanvasGroup m_NoResponseCanvasGroup;

        Coroutine m_NoResponseFadeRoutine;

        void Awake()
        {
            if (m_PracticeFeedbackPanel != null)
            {
                m_PanelImage = m_PracticeFeedbackPanel.GetComponent<Image>();

                if (m_PanelImage == null)
                    m_PanelImage = m_PracticeFeedbackPanel.AddComponent<Image>();

                m_NoResponseCanvasGroup = m_PracticeFeedbackPanel.GetComponent<CanvasGroup>();

                if (m_NoResponseCanvasGroup == null)
                    m_NoResponseCanvasGroup = m_PracticeFeedbackPanel.AddComponent<CanvasGroup>();

                // Safety: prevent any accidental white/default panel flash.
                SetNoResponseBackground();

                m_NoResponseCanvasGroup.alpha = 0f;
                m_NoResponseCanvasGroup.interactable = false;
                m_NoResponseCanvasGroup.blocksRaycasts = false;

                m_PracticeFeedbackPanel.SetActive(false);
            }

            if (m_NoResponseLabel != null)
            {
                m_NoResponseLabel.text = string.Empty;
                m_NoResponseLabel.color = m_NoResponseTextColor;
            }
}

        public void OnResponseSubmitted()
        {
            if (m_AudioSource != null && m_ResponseSound != null)
                m_AudioSource.PlayOneShot(m_ResponseSound);
        }

        public void ShowNoResponseMessage()
        {
            if (m_PracticeFeedbackPanel == null)
                return;

            if (m_NoResponseFadeRoutine != null)
            {
                StopCoroutine(m_NoResponseFadeRoutine);
                m_NoResponseFadeRoutine = null;
            }

            if (m_NoResponseLabel != null)
            {
                m_NoResponseLabel.text = m_NoResponseText;
                m_NoResponseLabel.color = m_NoResponseTextColor;
            }

            // Set safe dark background before enabling the panel.
            SetNoResponseBackground();

            if (m_NoResponseCanvasGroup != null)
            {
                // Critical: guarantee no one-frame full-alpha flash.
                m_NoResponseCanvasGroup.alpha = 0f;
                m_NoResponseCanvasGroup.interactable = false;
                m_NoResponseCanvasGroup.blocksRaycasts = false;
            }

            m_PracticeFeedbackPanel.SetActive(true);

            m_NoResponseFadeRoutine = StartCoroutine(
                FadeNoResponse(
                    m_NoResponseTargetAlpha,
                    m_NoResponseFadeInSeconds,
                    hideOnEnd: false
                )
            );
        }

        /// <summary>
        /// Hides the no-response reminder by fading out.
        /// </summary>
        public void HideNoResponseMessage()
        {
            if (m_PracticeFeedbackPanel == null)
                return;

            if (m_NoResponseFadeRoutine != null)
                StopCoroutine(m_NoResponseFadeRoutine);

            m_NoResponseFadeRoutine = StartCoroutine(
                FadeNoResponse(
                    0f,
                    m_NoResponseFadeOutSeconds,
                    hideOnEnd: true
                )
            );
        }

        IEnumerator FadeNoResponse(float targetAlpha, float duration, bool hideOnEnd)
        {
            if (m_NoResponseCanvasGroup == null)
            {
                if (hideOnEnd)
                    m_PracticeFeedbackPanel.SetActive(false);

                if (m_NoResponseLabel != null && hideOnEnd)
                    m_NoResponseLabel.text = string.Empty;

                yield break;
            }

            float start = m_NoResponseCanvasGroup.alpha;
            float t = 0f;
            float safeDuration = Mathf.Max(duration, 0.25f);

            while (t < safeDuration)
            {
                t += Time.deltaTime;

                m_NoResponseCanvasGroup.alpha =
                    Mathf.Lerp(
                        start,
                        targetAlpha,
                        Mathf.SmoothStep(0f, 1f, t / safeDuration)
                    );

                yield return null;
            }

            m_NoResponseCanvasGroup.alpha = targetAlpha;

            if (hideOnEnd)
            {
                m_PracticeFeedbackPanel.SetActive(false);

                if (m_NoResponseLabel != null)
                    m_NoResponseLabel.text = string.Empty;
            }

            m_NoResponseFadeRoutine = null;
        }

        void SetNoResponseBackground()
        {
            if (m_PanelImage == null)
                return;

            Color bg = m_NoResponseBackgroundColor;
            bg.a = m_NoResponseBackgroundAlpha;
            m_PanelImage.color = bg;
        }
    }
}