using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HitOrMiss
{
    public enum KeyboardInputTaskMode
    {
        HitOrMiss,
        PPS
    }

    public class KeyboardCommandInput : MonoBehaviour, IResponseInputSource
    {
        [Header("Task Mode")]
        [SerializeField] private KeyboardInputTaskMode m_TaskMode = KeyboardInputTaskMode.HitOrMiss;

        public event Action<ResponseEvent> ResponseReceived;

        private bool m_Enabled;
        private Keyboard m_Keyboard;

        public void Enable() => m_Enabled = true;
        public void Disable() => m_Enabled = false;

        private void Update()
        {
            if (!m_Enabled)
                return;

            m_Keyboard = Keyboard.current;

            if (m_Keyboard == null)
            {
                Debug.LogWarning("[KeyboardCommandInput] Keyboard.current is null");
                return;
            }

            if (m_TaskMode == KeyboardInputTaskMode.PPS)
                CheckPpsInput();
            else
                CheckHitOrMissInput();
        }

// Check whether spacebar was pressed during an event
        private void CheckPpsInput()
        {
            if (!m_Keyboard.spaceKey.wasPressedThisFrame)
                return;

            Debug.Log("[PPS INPUT] Space pressed");
            ResponseReceived?.Invoke(new ResponseEvent
            {
                rawSource = "keyboard_space_felt_it",
                command = SemanticCommand.Hit,
                confidence = 1f,
                timestamp = Time.timeAsDouble
            });
        }
        private void CheckHitOrMissInput()
        {
            if (m_Keyboard.hKey.wasPressedThisFrame)
            {
                ResponseReceived?.Invoke(new ResponseEvent
                {
                    rawSource = "keyboard_H",
                    command = SemanticCommand.Hit,
                    confidence = 1f,
                    timestamp = Time.timeAsDouble
                });
            }
            else if (m_Keyboard.mKey.wasPressedThisFrame)
            {
                ResponseReceived?.Invoke(new ResponseEvent
                {
                    rawSource = "keyboard_M",
                    command = SemanticCommand.Miss,
                    confidence = 1f,
                    timestamp = Time.timeAsDouble
                });
            }
        }
    }
}