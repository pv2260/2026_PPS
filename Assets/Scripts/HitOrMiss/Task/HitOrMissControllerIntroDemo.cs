using TMPro;
using UnityEngine;

namespace HitOrMiss
{
    public class HitOrMissControllerIntroDemo : MonoBehaviour
    {
        [Header("Trigger highlight meshes")]
        [SerializeField] private GameObject m_LeftTriggerHighlight;
        [SerializeField] private GameObject m_RightTriggerHighlight;

        [Header("Text")]
        [SerializeField] private TMP_Text m_InstructionText;

        [TextArea(2, 5)]
        [SerializeField] private string m_DemoText =
            "First, let's learn which buttons to press.\n\nPress the TRIGGER on EACH controller.";

        private bool m_LeftPressed;
        private bool m_RightPressed;

        public bool HasPressedBothTriggers => m_LeftPressed && m_RightPressed;

        private void OnEnable()
        {
            ResetDemo();
        }

        public void SetDemoText(string text)
        {
            if (m_InstructionText != null)
                m_InstructionText.text = text;
        }

        public void ResetDemo()
        {
            m_LeftPressed = false;
            m_RightPressed = false;

            if (m_InstructionText != null)
                m_InstructionText.text = m_DemoText;

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(false);

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(false);
        }

        public void ShowBeforeStart()
        {
            ResetDemo();
        }

        public void BeginDemo()
        {
            ResetDemo();
        }

        public void LeftTriggerPressed()
        {
            m_LeftPressed = true;

            Debug.Log("[HIT OR MISS TRIGGER DEMO] Left trigger pressed.");

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[HIT OR MISS TRIGGER DEMO] LeftTriggerHighlight is not assigned.");
        }

        public void RightTriggerPressed()
        {
            m_RightPressed = true;

            Debug.Log("[HIT OR MISS TRIGGER DEMO] Right trigger pressed.");

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[HIT OR MISS TRIGGER DEMO] RightTriggerHighlight is not assigned.");
        }
    }
}