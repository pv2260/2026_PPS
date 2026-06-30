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

        [Header("YES / NO visual feedback")]
        [SerializeField] private GameObject m_YesObject;
        [SerializeField] private GameObject m_NoObject;

        [Header("Instruction text")]
        [SerializeField] private TMP_Text m_InstructionText;

        [TextArea(2, 5)]
        [SerializeField] private string m_Text =
            "Use the controller triggers to answer.\n\nLEFT trigger = YES\nRIGHT trigger = NO";

        [Header("Fade")]
        [SerializeField] private float m_TextFadeSeconds = 0.35f;

        [Header("Pop animation")]
        [SerializeField] private float m_PopSeconds = 0.18f;
        [SerializeField] private float m_PopStartScale = 0.65f;
        [SerializeField] private float m_PopEndScale = 1.0f;

        private Coroutine m_FadeCoroutine;
        private Coroutine m_YesPopCoroutine;
        private Coroutine m_NoPopCoroutine;

        private bool m_LeftPressed;
        private bool m_RightPressed;

        public bool HasPressedBothResponses => m_LeftPressed && m_RightPressed;

        private void OnEnable()
        {
            ResetDemo();
        }

        public void ResetDemo()
        {
            m_LeftPressed = false;
            m_RightPressed = false;

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

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(false);

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(false);

            if (m_YesObject != null)
            {
                m_YesObject.SetActive(false);
                m_YesObject.transform.localScale = Vector3.one * m_PopStartScale;
            }

            if (m_NoObject != null)
            {
                m_NoObject.SetActive(false);
                m_NoObject.transform.localScale = Vector3.one * m_PopStartScale;
            }
        }

        public void LeftPressed()
        {
            m_LeftPressed = true;

            Debug.Log("[HIT OR MISS RESPONSE MAPPING] LEFT = YES pressed.");

            FadeInstructionText();

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] LeftTriggerHighlight is not assigned.");

            if (m_YesObject != null)
            {
                m_YesObject.SetActive(true);

                if (m_YesPopCoroutine != null)
                    StopCoroutine(m_YesPopCoroutine);

                m_YesPopCoroutine = StartCoroutine(PopObject(m_YesObject));
            }
            else
            {
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] YesObject is not assigned.");
            }
        }

        public void RightPressed()
        {
            m_RightPressed = true;

            Debug.Log("[HIT OR MISS RESPONSE MAPPING] RIGHT = NO pressed.");

            FadeInstructionText();

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] RightTriggerHighlight is not assigned.");

            if (m_NoObject != null)
            {
                m_NoObject.SetActive(true);

                if (m_NoPopCoroutine != null)
                    StopCoroutine(m_NoPopCoroutine);

                m_NoPopCoroutine = StartCoroutine(PopObject(m_NoObject));
            }
            else
            {
                Debug.LogError("[HIT OR MISS RESPONSE MAPPING] NoObject is not assigned.");
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

        private IEnumerator PopObject(GameObject obj)
        {
            if (obj == null)
                yield break;

            float elapsed = 0f;

            Vector3 startScale = Vector3.one * m_PopStartScale;
            Vector3 endScale = Vector3.one * m_PopEndScale;

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