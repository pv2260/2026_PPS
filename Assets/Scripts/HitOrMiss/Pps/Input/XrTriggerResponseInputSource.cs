using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HitOrMiss
{
    /// <summary>
    /// Captures participant responses from an XR controller trigger button.
    /// Bind m_TriggerAction in the Inspector to e.g. XRI Right Interaction/Activate.
    /// </summary>
    public class XrTriggerResponseInputSource : MonoBehaviour, IResponseInputSource
    {
        [Header("Trigger Input")]
        [Tooltip("Bind to XRI Right Interaction/Activate (the trigger press).")]
        [SerializeField] InputActionProperty m_TriggerAction;

        [Tooltip("Label written into ResponseEvent.rawSource for logs.")]
        [SerializeField] string m_SourceLabel = "controller_right_trigger";

        public event Action<ResponseEvent> ResponseReceived;

        bool m_Enabled;

        public void Enable()
        {
            if (m_Enabled) return;

            var action = m_TriggerAction.action;
            if (action == null)
            {
                Debug.LogError("[XrTriggerResponseInputSource] No InputAction assigned.");
                return;
            }

            action.performed += OnTriggerPerformed;
            action.Enable();
            m_Enabled = true;

            Debug.Log("[XrTriggerResponseInputSource] Enabled.");
        }

        public void Disable()
        {
            if (!m_Enabled) return;

            var action = m_TriggerAction.action;
            if (action != null)
            {
                action.performed -= OnTriggerPerformed;
                action.Disable();
            }
            m_Enabled = false;

            Debug.Log("[XrTriggerResponseInputSource] Disabled.");
        }

        void OnTriggerPerformed(InputAction.CallbackContext ctx)
        {
            var ev = new ResponseEvent
            {
                rawSource = m_SourceLabel,
                command = SemanticCommand.Hit,   // "I felt it" = affirmative response
                confidence = 1f,
                timestamp = Time.timeAsDouble,
            };

            ResponseReceived?.Invoke(ev);
        }

        void OnDisable()
        {
            Disable();
        }
    }
}