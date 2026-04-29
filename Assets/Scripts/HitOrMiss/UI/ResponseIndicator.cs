using System.Collections;
using UnityEngine;
using TMPro;

namespace HitOrMiss
{
    /// <summary>
    /// Brief, neutral acknowledgement that an input was received.
    ///
    /// Shows the same colour for both Hit and Miss — only the label differs —
    /// so the participant gets feedback that their button/pinch/key was registered
    /// without any positive or negative connotation. Optional head-locked mode
    /// guarantees visibility in MR by following the main camera each frame, so
    /// you don't have to wire a world-space canvas.
    /// </summary>
    public class ResponseIndicator : MonoBehaviour
    {
        [SerializeField] TMP_Text m_IndicatorText;

        [Header("Diagnostics")]
        [SerializeField] bool m_VerboseLogging = true;

        [Header("Timing")]
        [SerializeField] float m_HoldDuration = 0.18f;
        [SerializeField] float m_FadeDuration = 0.35f;

        [Header("Neutral colour (used for both Hit and Miss)")]
        [SerializeField] Color m_MatchedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField] Color m_IgnoredColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);

        [Header("Labels")]
        [SerializeField] string m_HitLabel = "HIT";
        [SerializeField] string m_MissLabel = "MISS";
        [SerializeField] string m_IgnoredSuffix = " ·";

        [Header("Head-locked positioning (MR-safe)")]
        [Tooltip("If true, the indicator follows the main camera each frame so it's always in the participant's view.")]
        [SerializeField] bool m_FollowCamera = true;
        [Tooltip("If empty, uses Camera.main at runtime.")]
        [SerializeField] Transform m_FollowTarget;
        [Tooltip("Offset relative to the follow target (right, up, forward).")]
        [SerializeField] Vector3 m_LocalOffset = new Vector3(0f, -0.25f, 1.5f);
        [Tooltip("Uniform scale applied to the indicator transform when head-locked.")]
        [SerializeField] float m_FollowScale = 0.6f;

        Coroutine m_FadeCoroutine;

        void Awake()
        {
            if (m_IndicatorText == null)
                m_IndicatorText = GetComponentInChildren<TMP_Text>(includeInactive: true);

            if (m_IndicatorText != null)
                m_IndicatorText.gameObject.SetActive(false);
            else
                Debug.LogWarning("[ResponseIndicator] No TMP_Text assigned and none found in children — feedback will not be visible.");
        }

        void LateUpdate()
        {
            if (!m_FollowCamera) return;
            var target = ResolveFollowTarget();
            if (target == null) return;

            transform.position = target.position + target.rotation * m_LocalOffset;
            transform.rotation = target.rotation;
            transform.localScale = Vector3.one * m_FollowScale;
        }

        Transform ResolveFollowTarget()
        {
            if (m_FollowTarget != null) return m_FollowTarget;
            return Camera.main != null ? Camera.main.transform : null;
        }

        public void Show(SemanticCommand command, bool matched)
        {
            if (m_VerboseLogging)
            {
                string textState = m_IndicatorText == null
                    ? "TMP=null"
                    : $"TMP@{m_IndicatorText.transform.position} canvas={(m_IndicatorText.canvas != null ? m_IndicatorText.canvas.name : "<none>")} active={m_IndicatorText.gameObject.activeInHierarchy}";
                Debug.Log($"[ResponseIndicator] Show({command}, matched={matched}) {textState}");
            }

            if (m_IndicatorText == null)
            {
                Debug.LogWarning("[ResponseIndicator] Show() called but m_IndicatorText is null. Assign a TMP_Text in the inspector.");
                return;
            }

            string label = command == SemanticCommand.Hit ? m_HitLabel : m_MissLabel;
            if (!matched && !string.IsNullOrEmpty(m_IgnoredSuffix))
                label += m_IgnoredSuffix;

            Color color = matched ? m_MatchedColor : m_IgnoredColor;

            m_IndicatorText.text = label;
            m_IndicatorText.color = color;
            m_IndicatorText.gameObject.SetActive(true);

            if (m_FadeCoroutine != null)
                StopCoroutine(m_FadeCoroutine);
            m_FadeCoroutine = StartCoroutine(FadeOut(color));
        }

        IEnumerator FadeOut(Color startColor)
        {
            yield return new WaitForSeconds(m_HoldDuration);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, m_FadeDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
                m_IndicatorText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            m_IndicatorText.gameObject.SetActive(false);
            m_FadeCoroutine = null;
        }
    }
}
