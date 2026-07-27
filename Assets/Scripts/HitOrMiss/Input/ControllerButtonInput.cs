using System;
using UnityEngine;
using UnityEngine.InputSystem;
using HitOrMiss.Pps;

namespace HitOrMiss
{
    /// <summary>
    /// Controller button input: RIGHT trigger/button = HIT ("yes, it will hit me"),
    /// LEFT trigger/button = MISS ("no, it will miss me").
    /// Uses Unity's new Input System. Assign actions in the Inspector.
    ///
    /// The two InputActionReference fields keep their old names so the existing
    /// Inspector assignments survive. They are wired by HAND, not by answer:
    /// m_HitAction holds the LEFT controller action and m_MissAction the RIGHT,
    /// which is why the names now read backwards against what they emit.
    /// </summary>
    public class ControllerButtonInput : MonoBehaviour, IResponseInputSource
    {
        [Header("Input Actions")]
        [SerializeField] InputActionReference m_HitAction;   // LEFT controller trigger/button -> Miss
        [SerializeField] InputActionReference m_MissAction;  // RIGHT controller trigger/button -> Hit

        [Header("Haptics")]
        [SerializeField] private PPSControllerHaptics m_Haptics;

        public event Action<ResponseEvent> ResponseReceived;

        bool m_Enabled;

        public void Enable()
        {
            m_Enabled = true;

            if (m_HitAction != null && m_HitAction.action != null)
            {
                m_HitAction.action.Enable();
                m_HitAction.action.performed += OnLeftPerformed;
            }

            if (m_MissAction != null && m_MissAction.action != null)
            {
                m_MissAction.action.Enable();
                m_MissAction.action.performed += OnRightPerformed;
            }
        }

        public void Disable()
        {
            m_Enabled = false;

            if (m_HitAction != null && m_HitAction.action != null)
            {
                m_HitAction.action.performed -= OnLeftPerformed;
                m_HitAction.action.Disable();
            }

            if (m_MissAction != null && m_MissAction.action != null)
            {
                m_MissAction.action.performed -= OnRightPerformed;
                m_MissAction.action.Disable();
            }
        }

        void OnLeftPerformed(InputAction.CallbackContext ctx)
        {
            if (!m_Enabled) return;

            m_Haptics?.PlayLeftResponseHaptic();

            ResponseReceived?.Invoke(new ResponseEvent
            {
                rawSource = "controller_left",
                command = SemanticCommand.Miss,
                confidence = 1f,
                timestamp = Time.timeAsDouble
            });
        }

        void OnRightPerformed(InputAction.CallbackContext ctx)
        {
            if (!m_Enabled) return;

            m_Haptics?.PlayRightResponseHaptic();

            ResponseReceived?.Invoke(new ResponseEvent
            {
                rawSource = "controller_right",
                command = SemanticCommand.Hit,
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