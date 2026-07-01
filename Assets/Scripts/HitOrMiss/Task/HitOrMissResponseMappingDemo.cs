using System.Collections;
using TMPro;
using UnityEngine;

namespace HitOrMiss
{
    public class HitOrMissResponseMappingDemo : MonoBehaviour
    {
        [Header("Trigger highlight meshes")]
        [SerializeField] private GameObject m_LeftTriggerHighlight;
        [SerializeField] private GameObject m_RightTriggerHighlight;

        [Header("Trigger highlight renderers")]
        [SerializeField] private Renderer m_LeftTriggerRenderer;
        [SerializeField] private Renderer m_RightTriggerRenderer;

        [Header("Ball + Y/N panels")]
        [SerializeField] private GameObject m_BallObject;
        [SerializeField] private GameObject m_YesPanel;
        [SerializeField] private GameObject m_NoPanel;

        [Header("Panel renderers")]
        [SerializeField] private Renderer m_YesPanelRenderer;
        [SerializeField] private Renderer m_NoPanelRenderer;

        [Header("Materials")]
        [SerializeField] private Material m_DefaultMaterial;
        [SerializeField] private Material m_SelectedOrangeMaterial;

        [Header("Instruction text")]
        [SerializeField] private TMP_Text m_InstructionText;

        [TextArea(2, 5)]
        [SerializeField] private string m_Text =
            "You will see a ball with two panels around it:\n\nY = YES\nN = NO\n\nPress LEFT trigger for YES.\nPress RIGHT trigger for NO.\n\nThe selected controller and panel will turn orange.";

        [Header("Fade")]
        [SerializeField] private float m_TextFadeSeconds = 0.35f;

        [Header("Panel pop animation")]
        [SerializeField] private float m_PopSeconds = 0.18f;
        [SerializeField] private float m_PopStartScale = 0.85f;
        [SerializeField] private float m_PopEndScale = 1.2f;

        [Header("Advance delay")]
        [SerializeField] private float m_AfterResponsePaddingSeconds = 0.8f;

        private float m_BothResponsesCompletedTime = -1f;

        private Coroutine m_FadeCoroutine;
        private Coroutine m_YesPopCoroutine;
        private Coroutine m_NoPopCoroutine;

        private Vector3 m_YesPanelOriginalScale;
        private Vector3 m_NoPanelOriginalScale;

        private bool m_LeftPressed;
        private bool m_RightPressed;

        public bool HasPressedBothResponses => m_LeftPressed && m_RightPressed;

        public bool ReadyToAdvance =>
            HasPressedBothResponses &&
            m_BothResponsesCompletedTime > 0f &&
            Time.time >= m_BothResponsesCompletedTime + m_AfterResponsePaddingSeconds;

        private void Awake()
        {
            if (m_YesPanel != null)
                m_YesPanelOriginalScale = m_YesPanel.transform.localScale;

            if (m_NoPanel != null)
                m_NoPanelOriginalScale = m_NoPanel.transform.localScale;
        }

        private void OnEnable()
        {
            ResetDemo();
        }

        public void ResetDemo()
        {
            m_LeftPressed = false;
            m_RightPressed = false;
            m_BothResponsesCompletedTime = -1f;

            if (m_FadeCoroutine != null)
                StopCoroutine(m_FadeCoroutine);

            if (m_YesPopCoroutine != null)
                StopCoroutine(m_YesPopCoroutine);

            if (m_NoPopCoroutine != null)
                StopCoroutine(m_NoPopCoroutine);

            if (m_InstructionText != null)
            {
                m_InstructionText.text = m_Text;
                SetTextAlpha(1f);
            }

            if (m_BallObject != null)
                m_BallObject.SetActive(true);

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(true);

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(true);

            if (m_YesPanel != null)
            {
                m_YesPanel.SetActive(true);
                m_YesPanel.transform.localScale = m_YesPanelOriginalScale;
            }

            if (m_NoPanel != null)
            {
                m_NoPanel.SetActive(true);
                m_NoPanel.transform.localScale = m_NoPanelOriginalScale;
            }

            SetRendererMaterial(m_LeftTriggerRenderer, m_DefaultMaterial);
            SetRendererMaterial(m_RightTriggerRenderer, m_DefaultMaterial);
            SetRendererMaterial(m_YesPanelRenderer, m_DefaultMaterial);
            SetRendererMaterial(m_NoPanelRenderer, m_DefaultMaterial);
        }

        public void LeftPressed()
        {
            if (m_LeftPressed)
                return;

            m_LeftPressed = true;

            Debug.Log("[HIT OR MISS RESPONSE MAPPING] LEFT = YES pressed.");

            FadeInstructionText();

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] LeftTriggerHighlight is not assigned.");

            SetRendererMaterial(m_LeftTriggerRenderer, m_SelectedOrangeMaterial);
            SetRendererMaterial(m_YesPanelRenderer, m_SelectedOrangeMaterial);

            if (m_YesPanel != null)
            {
                if (m_YesPopCoroutine != null)
                    StopCoroutine(m_YesPopCoroutine);

                m_YesPopCoroutine = StartCoroutine(PopObject(m_YesPanel, m_YesPanelOriginalScale));
            }
            else
            {
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] YesPanel is not assigned.");
            }

            CheckBothResponsesCompleted();
        }

        public void RightPressed()
        {
            Debug.LogError("[RESPONSE MAPPING DEBUG] RightPressed was called.");

            Debug.LogError($"[RESPONSE MAPPING DEBUG] RightTriggerHighlight = {(m_RightTriggerHighlight != null ? m_RightTriggerHighlight.name : "NULL")}");
            Debug.LogError($"[RESPONSE MAPPING DEBUG] RightTriggerRenderer = {(m_RightTriggerRenderer != null ? m_RightTriggerRenderer.name : "NULL")}");
            Debug.LogError($"[RESPONSE MAPPING DEBUG] NoPanel = {(m_NoPanel != null ? m_NoPanel.name : "NULL")}");
            Debug.LogError($"[RESPONSE MAPPING DEBUG] NoPanelRenderer = {(m_NoPanelRenderer != null ? m_NoPanelRenderer.name : "NULL")}");

            if (m_RightTriggerHighlight != null)
                Debug.LogError($"[RESPONSE MAPPING DEBUG] RightTriggerHighlight activeInHierarchy = {m_RightTriggerHighlight.activeInHierarchy}");

            if (m_NoPanel != null)
                Debug.LogError($"[RESPONSE MAPPING DEBUG] NoPanel activeInHierarchy = {m_NoPanel.activeInHierarchy}");

            if (m_RightPressed)
                return;

            m_RightPressed = true;

            Debug.Log("[HIT OR MISS RESPONSE MAPPING] RIGHT = NO pressed.");

            FadeInstructionText();

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] RightTriggerHighlight is not assigned.");

            SetRendererMaterial(m_RightTriggerRenderer, m_SelectedOrangeMaterial);
            SetRendererMaterial(m_NoPanelRenderer, m_SelectedOrangeMaterial);

            if (m_NoPanel != null)
            {
                m_NoPanel.SetActive(true);

                if (m_NoPopCoroutine != null)
                    StopCoroutine(m_NoPopCoroutine);

                m_NoPopCoroutine = StartCoroutine(PopObject(m_NoPanel, m_NoPanelOriginalScale));
            }
            else
            {
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] NoPanel is not assigned.");
            }

            CheckBothResponsesCompleted();
        }

        private void CheckBothResponsesCompleted()
        {
            if (m_LeftPressed && m_RightPressed && m_BothResponsesCompletedTime < 0f)
            {
                m_BothResponsesCompletedTime = Time.time;
                Debug.Log($"[HIT OR MISS RESPONSE MAPPING] Both responses completed at {m_BothResponsesCompletedTime:F3}");
            }
        }

        private void FadeInstructionText()
        {
            if (m_InstructionText == null)
                return;

            if (m_FadeCoroutine != null)
                StopCoroutine(m_FadeCoroutine);

            m_FadeCoroutine = StartCoroutine(FadeTextOut());
        }

        private IEnumerator FadeTextOut()
        {
            float elapsed = 0f;
            float startAlpha = m_InstructionText.color.a;

            while (elapsed < m_TextFadeSeconds)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / m_TextFadeSeconds);
                float alpha = Mathf.Lerp(startAlpha, 0f, t);

                SetTextAlpha(alpha);

                yield return null;
            }

            SetTextAlpha(0f);
        }

        private void SetTextAlpha(float alpha)
        {
            if (m_InstructionText == null)
                return;

            Color color = m_InstructionText.color;
            color.a = alpha;
            m_InstructionText.color = color;
        }

        private void SetRendererMaterial(Renderer targetRenderer, Material material)
        {
            if (targetRenderer == null)
            {
                Debug.LogError("[RESPONSE DEMO] Missing renderer reference.");
                return;
            }

            if (material == null)
            {
                Debug.LogError("[RESPONSE DEMO] Missing material reference.");
                return;
            }

            targetRenderer.sharedMaterial = material;

            Debug.Log($"[RESPONSE DEMO] Set {targetRenderer.name} material to {material.name}");
        }

        private IEnumerator PopObject(GameObject obj, Vector3 originalScale)
        {
            if (obj == null)
                yield break;

            float elapsed = 0f;

            Vector3 startScale = originalScale * m_PopStartScale;
            Vector3 endScale = originalScale * m_PopEndScale;

            obj.transform.localScale = startScale;

            while (elapsed < m_PopSeconds)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / m_PopSeconds);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                obj.transform.localScale = Vector3.Lerp(startScale, endScale, smoothT);

                yield return null;
            }

            obj.transform.localScale = endScale;
        }
    }
}