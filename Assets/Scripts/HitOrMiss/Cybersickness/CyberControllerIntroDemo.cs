using System.Collections;
using TMPro;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CyberControllerIntroDemo : MonoBehaviour
    {
        [Header("World placement")]
        [SerializeField] Transform m_PlayerAnchor;
        [SerializeField] float m_DistanceFromParticipant = 1.2f;
        [SerializeField] float m_HorizontalOffset = 0.35f;
        [SerializeField] float m_VerticalOffset = -0.20f;
        [SerializeField] float m_ControllerScale = 0.35f;
        [SerializeField] Vector3 m_LeftRotationOffset = new Vector3(0f, 180f, 0f);
        [SerializeField] Vector3 m_RightRotationOffset = new Vector3(0f, 180f, 0f);

        [Header("Controller demo objects")]
        [SerializeField] GameObject m_LeftControllerDemo;
        [SerializeField] GameObject m_RightControllerDemo;

        [Header("Trigger renderers")]
        [SerializeField] Renderer m_LeftTriggerRenderer;
        [SerializeField] Renderer m_RightTriggerRenderer;

        [Header("Intro UI")]
        [SerializeField] GameObject m_StartDemoButtonObject;
        [SerializeField] TMP_Text m_InstructionText;

        [Header("Text")]
        [TextArea(2, 5)]
        [SerializeField] string m_BeforeStartText =
            "Before starting the task, we will learn how to use the controllers.\n\nPress Continue to show the controllers.";

        [TextArea(2, 5)]
        [SerializeField] string m_DemoText =
            "Press the trigger on each controller.\n\nWhen you press a trigger, it will light up.";

        [Header("Colors")]
        [SerializeField] Color m_NormalColor = Color.white;
        [SerializeField] Color m_PressedColor = Color.green;

        [Header("Timing")]
        [SerializeField] float m_FlashSeconds = 0.35f;

        Coroutine m_LeftCoroutine;
        Coroutine m_RightCoroutine;

        MaterialPropertyBlock m_LeftBlock;
        MaterialPropertyBlock m_RightBlock;

        void Awake()
        {
            m_LeftBlock = new MaterialPropertyBlock();
            m_RightBlock = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            ShowBeforeStart();
        }

        void OnDisable()
        {
            HideControllers();
        }

        public void ShowBeforeStart()
        {
            Debug.LogError("[CONTROLLER INTRO] ShowBeforeStart called.");

            HideControllers();

            if (m_StartDemoButtonObject != null)
                m_StartDemoButtonObject.SetActive(true);
            else
                Debug.LogError("[CONTROLLER INTRO] StartDemoButtonObject is NOT assigned.");

            if (m_InstructionText != null)
                m_InstructionText.text = m_BeforeStartText;
            else
                Debug.LogError("[CONTROLLER INTRO] InstructionText is NOT assigned.");

            ResetTriggerColors();
        }

        public void BeginDemo()
        {
            Debug.LogError("[CONTROLLER INTRO] BeginDemo called.");

            if (m_StartDemoButtonObject != null)
                m_StartDemoButtonObject.SetActive(false);
            else
                Debug.LogError("[CONTROLLER INTRO] StartDemoButtonObject is NOT assigned.");

            if (m_InstructionText != null)
                m_InstructionText.text = m_DemoText;
            else
                Debug.LogError("[CONTROLLER INTRO] InstructionText is NOT assigned.");

            PlaceControllersInFrontOfParticipant();

            ResetTriggerColors();
        }

        void PlaceControllersInFrontOfParticipant()
        {
            if (m_PlayerAnchor == null)
            {
                Debug.LogError("[CONTROLLER INTRO] PlayerAnchor is NOT assigned.");
                return;
            }

            Vector3 eye = m_PlayerAnchor.position;

            Vector3 forward = m_PlayerAnchor.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();

            Vector3 right = m_PlayerAnchor.right;
            right.y = 0f;

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            right.Normalize();

            Vector3 center =
                eye +
                forward * m_DistanceFromParticipant +
                Vector3.up * m_VerticalOffset;

            Quaternion baseRotation = Quaternion.LookRotation(forward, Vector3.up);

            if (m_LeftControllerDemo == null)
            {
                Debug.LogError("[CONTROLLER INTRO] LeftControllerDemo is NOT assigned.");
            }
            else
            {
                m_LeftControllerDemo.SetActive(true);

                m_LeftControllerDemo.transform.position =
                    center - right * m_HorizontalOffset;

                m_LeftControllerDemo.transform.rotation =
                    baseRotation * Quaternion.Euler(m_LeftRotationOffset);

                m_LeftControllerDemo.transform.localScale =
                    Vector3.one * m_ControllerScale;

                Debug.LogError(
                    $"[CONTROLLER INTRO] Left controller placed at {m_LeftControllerDemo.transform.position}"
                );
            }

            if (m_RightControllerDemo == null)
            {
                Debug.LogError("[CONTROLLER INTRO] RightControllerDemo is NOT assigned.");
            }
            else
            {
                m_RightControllerDemo.SetActive(true);

                m_RightControllerDemo.transform.position =
                    center + right * m_HorizontalOffset;

                m_RightControllerDemo.transform.rotation =
                    baseRotation * Quaternion.Euler(m_RightRotationOffset);

                m_RightControllerDemo.transform.localScale =
                    Vector3.one * m_ControllerScale;

                Debug.LogError(
                    $"[CONTROLLER INTRO] Right controller placed at {m_RightControllerDemo.transform.position}"
                );
            }

            Debug.LogError(
                $"[CONTROLLER INTRO] Controllers placed. center={center}, forward={forward}, right={right}"
            );
        }

        void HideControllers()
        {
            if (m_LeftControllerDemo != null)
                m_LeftControllerDemo.SetActive(false);

            if (m_RightControllerDemo != null)
                m_RightControllerDemo.SetActive(false);
        }

        public void LeftTriggerPressed()
        {
            Debug.LogError("[CONTROLLER INTRO] Left trigger visual pressed.");

            if (m_LeftCoroutine != null)
                StopCoroutine(m_LeftCoroutine);

            m_LeftCoroutine = StartCoroutine(
                FlashTrigger(m_LeftTriggerRenderer, m_LeftBlock)
            );
        }

        public void RightTriggerPressed()
        {
            Debug.LogError("[CONTROLLER INTRO] Right trigger visual pressed.");

            if (m_RightCoroutine != null)
                StopCoroutine(m_RightCoroutine);

            m_RightCoroutine = StartCoroutine(
                FlashTrigger(m_RightTriggerRenderer, m_RightBlock)
            );
        }

        IEnumerator FlashTrigger(Renderer targetRenderer, MaterialPropertyBlock block)
        {
            if (targetRenderer == null)
            {
                Debug.LogError("[CONTROLLER INTRO] Trigger renderer is NOT assigned.");
                yield break;
            }

            SetRendererColor(targetRenderer, block, m_PressedColor);

            yield return new WaitForSeconds(m_FlashSeconds);

            // Keep it green after the participant has pressed it.
            SetRendererColor(targetRenderer, block, m_PressedColor);
        }

        void ResetTriggerColors()
        {
            if (m_LeftTriggerRenderer != null)
                SetRendererColor(m_LeftTriggerRenderer, m_LeftBlock, m_NormalColor);

            if (m_RightTriggerRenderer != null)
                SetRendererColor(m_RightTriggerRenderer, m_RightBlock, m_NormalColor);
        }

        void SetRendererColor(Renderer targetRenderer, MaterialPropertyBlock block, Color color)
        {
            if (targetRenderer == null)
                return;

            targetRenderer.GetPropertyBlock(block);

            // URP/Lit usually uses _BaseColor.
            block.SetColor("_BaseColor", color);

            // Built-in/Standard materials usually use _Color.
            block.SetColor("_Color", color);

            targetRenderer.SetPropertyBlock(block);
        }
    }
}