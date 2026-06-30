using TMPro;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CyberControllerIntroDemo : MonoBehaviour
    {
        [Header("Trigger highlight meshes")]
        [SerializeField] GameObject m_LeftTriggerHighlight;
        [SerializeField] GameObject m_RightTriggerHighlight;

        [Header("Text")]
        [SerializeField] TMP_Text m_InstructionText;

        [TextArea(2, 5)]
        [SerializeField] string m_DemoText =
            "Press the trigger on each controller.\n\nWhen you press a trigger, it will light up.";

        void OnEnable()
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
            Debug.LogError("[TRIGGER DEMO] Left trigger visual pressed.");

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[TRIGGER DEMO] LeftTriggerHighlight is not assigned.");
        }

        public void RightTriggerPressed()
        {
            Debug.LogError("[TRIGGER DEMO] Right trigger visual pressed.");

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[TRIGGER DEMO] RightTriggerHighlight is not assigned.");
        }
    }
}