using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Cybersickness
{
    public class CyberBugPredictionPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] GameObject m_Root;

        [Header("Camera / placement")]
        [SerializeField] Transform m_PlayerAnchor;
        [SerializeField] float m_DistanceFromPlayer = 1.6f;
        [SerializeField] float m_VerticalOffset = -0.35f;
        [SerializeField] float m_PanelScale = 0.0012f;
        [SerializeField] bool m_FollowHeadWhileVisible = true;

        [Header("Text")]
        [SerializeField] TMP_Text m_QuestionText;
        [SerializeField] TMP_Text m_YesText;
        [SerializeField] TMP_Text m_NoText;

        [Header("Panels")]
        [SerializeField] Image m_YesPanelImage;
        [SerializeField] Image m_NoPanelImage;

        [Header("Colors")]
        [SerializeField] Color m_NormalColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] Color m_YesSelectedColor = new Color(0.35f, 1f, 0.45f, 0.95f);
        [SerializeField] Color m_NoSelectedColor = new Color(1f, 0.35f, 0.35f, 0.95f);
        [SerializeField] Color m_UnselectedColor = new Color(1f, 1f, 1f, 0.05f);

        [Header("Pop animation")]
        [SerializeField] float m_PopSeconds = 0.18f;
        [SerializeField] float m_NormalScale = 1.0f;
        [SerializeField] float m_PopScale = 1.18f;

        [Header("Question fade")]
        [SerializeField] bool m_FadeQuestionAfterResponse = true;
        [SerializeField] float m_QuestionFadeSeconds = 0.35f;

        Coroutine m_YesPopCoroutine;
        Coroutine m_NoPopCoroutine;
        Coroutine m_QuestionFadeCoroutine;

        bool m_IsVisible;

        void Awake()
        {
            Hide();
        }

        void LateUpdate()
        {
            if (!m_IsVisible) return;
            if (!m_FollowHeadWhileVisible) return;

            PlaceInFrontOfPlayer();
        }

        public void ShowResponseTextOnly(string responseText)
    {
        Debug.LogError($"[PREDICTION PANEL] ShowResponseTextOnly: {responseText}");

        if (m_Root != null)
            m_Root.SetActive(true);
        else
            gameObject.SetActive(true);

        m_IsVisible = true;

        PlaceInFrontOfPlayer();

        if (m_QuestionText != null)
        {
            m_QuestionText.text = responseText;
            SetQuestionAlpha(1f);
        }

        if (m_YesText != null)
            m_YesText.text = "";

        if (m_NoText != null)
            m_NoText.text = "";

        if (m_YesPanelImage != null)
            m_YesPanelImage.gameObject.SetActive(false);

        if (m_NoPanelImage != null)
            m_NoPanelImage.gameObject.SetActive(false);
    }

        public void ShowQuestion(string question)
        {
            Debug.LogError("[PREDICTION PANEL] ShowQuestion called.");

            if (m_Root != null)
                m_Root.SetActive(true);
            else
                gameObject.SetActive(true);

            m_IsVisible = true;

            PlaceInFrontOfPlayer();

            if (m_QuestionText != null)
            {
                m_QuestionText.text = question;
                SetQuestionAlpha(1f);
            }

            if (m_YesText != null)
                m_YesText.text = "YES";

            if (m_NoText != null)
                m_NoText.text = "NO";

            if (m_YesPanelImage != null)
                m_YesPanelImage.gameObject.SetActive(true);

            if (m_NoPanelImage != null)
                m_NoPanelImage.gameObject.SetActive(true);

            ResetVisuals();

            ResetVisuals();
        }

        public void HighlightYes()
        {
            Debug.LogError("[PREDICTION PANEL] YES highlighted.");

            FadeQuestionIfNeeded();

            if (m_YesPanelImage != null)
                m_YesPanelImage.color = m_YesSelectedColor;

            if (m_NoPanelImage != null)
                m_NoPanelImage.color = m_UnselectedColor;

            if (m_YesPanelImage != null)
            {
                if (m_YesPopCoroutine != null)
                    StopCoroutine(m_YesPopCoroutine);

                m_YesPopCoroutine = StartCoroutine(PopObject(m_YesPanelImage.transform));
            }
        }

        public void HighlightNo()
        {
            Debug.LogError("[PREDICTION PANEL] NO highlighted.");

            FadeQuestionIfNeeded();

            if (m_NoPanelImage != null)
                m_NoPanelImage.color = m_NoSelectedColor;

            if (m_YesPanelImage != null)
                m_YesPanelImage.color = m_UnselectedColor;

            if (m_NoPanelImage != null)
            {
                if (m_NoPopCoroutine != null)
                    StopCoroutine(m_NoPopCoroutine);

                m_NoPopCoroutine = StartCoroutine(PopObject(m_NoPanelImage.transform));
            }
        }

        public void Hide()
        {
            m_IsVisible = false;

            StopAllPanelCoroutines();
            ResetVisuals();

            if (m_Root != null)
                m_Root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        void PlaceInFrontOfPlayer()
        {
            if (m_PlayerAnchor == null)
            {
                Debug.LogWarning("[PREDICTION PANEL] PlayerAnchor is not assigned.");
                return;
            }

            Vector3 forward = m_PlayerAnchor.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();

            Vector3 panelPosition =
                m_PlayerAnchor.position +
                forward * m_DistanceFromPlayer +
                Vector3.up * m_VerticalOffset;

            transform.position = panelPosition;

            Vector3 lookDirection = transform.position - m_PlayerAnchor.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);

            transform.localScale = Vector3.one * m_PanelScale;
        }

        void ResetVisuals()
        {
            StopAllPanelCoroutines();

            if (m_YesPanelImage != null)
            {
                m_YesPanelImage.color = m_NormalColor;
                m_YesPanelImage.transform.localScale = Vector3.one * m_NormalScale;
            }

            if (m_NoPanelImage != null)
            {
                m_NoPanelImage.color = m_NormalColor;
                m_NoPanelImage.transform.localScale = Vector3.one * m_NormalScale;
            }

            if (m_QuestionText != null)
                SetQuestionAlpha(1f);
        }

        void FadeQuestionIfNeeded()
        {
            if (!m_FadeQuestionAfterResponse)
                return;

            if (m_QuestionText == null)
                return;

            if (m_QuestionFadeCoroutine != null)
                StopCoroutine(m_QuestionFadeCoroutine);

            m_QuestionFadeCoroutine = StartCoroutine(FadeQuestionOut());
        }

        IEnumerator FadeQuestionOut()
        {
            float elapsed = 0f;
            float startAlpha = m_QuestionText.color.a;

            while (elapsed < m_QuestionFadeSeconds)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / m_QuestionFadeSeconds);
                float alpha = Mathf.Lerp(startAlpha, 0f, t);

                SetQuestionAlpha(alpha);

                yield return null;
            }

            SetQuestionAlpha(0f);
        }

        IEnumerator PopObject(Transform target)
        {
            if (target == null)
                yield break;

            float elapsed = 0f;

            Vector3 startScale = Vector3.one * m_NormalScale;
            Vector3 peakScale = Vector3.one * m_PopScale;
            Vector3 endScale = Vector3.one * m_NormalScale;

            while (elapsed < m_PopSeconds)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / m_PopSeconds);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (smoothT < 0.5f)
                {
                    float upT = smoothT / 0.5f;
                    target.localScale = Vector3.Lerp(startScale, peakScale, upT);
                }
                else
                {
                    float downT = (smoothT - 0.5f) / 0.5f;
                    target.localScale = Vector3.Lerp(peakScale, endScale, downT);
                }

                yield return null;
            }

            target.localScale = endScale;
        }

        void SetQuestionAlpha(float alpha)
        {
            if (m_QuestionText == null)
                return;

            Color color = m_QuestionText.color;
            color.a = alpha;
            m_QuestionText.color = color;
        }

        void StopAllPanelCoroutines()
        {
            if (m_YesPopCoroutine != null)
            {
                StopCoroutine(m_YesPopCoroutine);
                m_YesPopCoroutine = null;
            }

            if (m_NoPopCoroutine != null)
            {
                StopCoroutine(m_NoPopCoroutine);
                m_NoPopCoroutine = null;
            }

            if (m_QuestionFadeCoroutine != null)
            {
                StopCoroutine(m_QuestionFadeCoroutine);
                m_QuestionFadeCoroutine = null;
            }
        }
    }
}