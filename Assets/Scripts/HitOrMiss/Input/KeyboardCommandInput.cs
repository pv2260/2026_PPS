using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HitOrMiss
{
    /// <summary>
    /// Debug input source using the new Input System: H for HIT, M for MISS.
    /// </summary>
    public class KeyboardCommandInput : MonoBehaviour, IResponseInputSource
    {
        public event Action<ResponseEvent> ResponseReceived;

        bool m_Enabled;
        Keyboard m_Keyboard;

        public void Enable() => m_Enabled = true;
        public void Disable() => m_Enabled = false;

        void Update()
        {
            if (!m_Enabled) return;

            m_Keyboard = Keyboard.current;
            if (m_Keyboard == null) return;

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
