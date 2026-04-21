using System.Collections;
using UnityEngine;
using TMPro;

namespace HitOrMiss
{
    /// <summary>
    /// Brief visual indicator shown when the participant presses a button.
    /// Shows "HIT" or "MISS" text for a short duration. Does NOT indicate correctness.
    /// Placed in the participant's peripheral view (bottom center).
    /// </summary>
    public class ResponseIndicator : MonoBehaviour
    {
        [SerializeField] TMP_Text m_IndicatorText;
        [SerializeField] float m_DisplayDuration = 0.5f;
        [SerializeField] Color m_HitColor = new Color(0.2f, 0.6f, 1f, 1f);  // Neutral blue
        [SerializeField] Color m_MissColor = new Color(1f, 0.6f, 0.2f, 1f); // Neutral orange

        Coroutine m_FadeCoroutine;

        void Awake()
        {
            if (m_IndicatorText != null)
                m_IndicatorText.gameObject.SetActive(false);
        }

        public void Show(SemanticCommand command, bool matched)
        {
            if (m_IndicatorText == null) return;

            m_IndicatorText.text = command == SemanticCommand.Hit ? "HIT" : "MISS";
            m_IndicatorText.color = command == SemanticCommand.Hit ? m_HitColor : m_MissColor;
            m_IndicatorText.gameObject.SetActive(true);

            if (m_FadeCoroutine != null)
                StopCoroutine(m_FadeCoroutine);
            m_FadeCoroutine = StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            yield return new WaitForSeconds(m_DisplayDuration);

            float fadeDuration = 0.3f;
            Color startColor = m_IndicatorText.color;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                m_IndicatorText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            m_IndicatorText.gameObject.SetActive(false);
            m_FadeCoroutine = null;
        }
    }
}
