using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HitOrMiss
{
    /// <summary>
    /// Controller button input: left trigger/button = HIT, right trigger/button = MISS.
    /// Uses Unity's new Input System. Assign actions in the Inspector.
    /// </summary>
    public class ControllerButtonInput : MonoBehaviour, IResponseInputSource
    {
        [Header("Input Actions")]
        [SerializeField] InputActionReference m_HitAction;   // Left controller trigger/button
        [SerializeField] InputActionReference m_MissAction;  // Right controller trigger/button

        public event Action<ResponseEvent> ResponseReceived;

        bool m_Enabled;

        public void Enable()
        {
            m_Enabled = true;
            if (m_HitAction != null && m_HitAction.action != null)
            {
                m_HitAction.action.Enable();
                m_HitAction.action.performed += OnHitPerformed;
            }
            if (m_MissAction != null && m_MissAction.action != null)
            {
                m_MissAction.action.Enable();
                m_MissAction.action.performed += OnMissPerformed;
            }
        }

        public void Disable()
        {
            m_Enabled = false;
            if (m_HitAction != null && m_HitAction.action != null)
            {
                m_HitAction.action.performed -= OnHitPerformed;
                m_HitAction.action.Disable();
            }
            if (m_MissAction != null && m_MissAction.action != null)
            {
                m_MissAction.action.performed -= OnMissPerformed;
                m_MissAction.action.Disable();
            }
        }

        void OnHitPerformed(InputAction.CallbackContext ctx)
        {
            if (!m_Enabled) return;
            ResponseReceived?.Invoke(new ResponseEvent
            {
                rawSource = "controller_left",
                command = SemanticCommand.Hit,
                confidence = 1f,
                timestamp = Time.timeAsDouble
            });
        }

        void OnMissPerformed(InputAction.CallbackContext ctx)
        {
            if (!m_Enabled) return;
            ResponseReceived?.Invoke(new ResponseEvent
            {
                rawSource = "controller_right",
                command = SemanticCommand.Miss,
                confidence = 1f,
                timestamp = Time.timeAsDouble
            });
        }

        void OnDestroy()
        {
            Disable();
        }
    }
}
