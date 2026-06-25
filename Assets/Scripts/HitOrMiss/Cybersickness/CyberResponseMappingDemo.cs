using TMPro;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CyberResponseMappingDemo : MonoBehaviour
    {
        [Header("Trigger highlight meshes")]
        [SerializeField] GameObject m_LeftTriggerHighlight;
        [SerializeField] GameObject m_RightTriggerHighlight;

        [Header("YES / NO visual feedback")]
        [SerializeField] GameObject m_YesObject;
        [SerializeField] GameObject m_NoObject;

        [Header("Instruction text")]
        [SerializeField] TMP_Text m_InstructionText;

        [TextArea(2, 5)]
        [SerializeField] string m_Text =
            "Use the controller triggers to answer.\n\nLEFT trigger = YES\nRIGHT trigger = NO";

        void OnEnable()
        {
            ResetDemo();
        }

        public void ResetDemo()
        {
            if (m_InstructionText != null)
                m_InstructionText.text = m_Text;

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(false);

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(false);

            if (m_YesObject != null)
                m_YesObject.SetActive(false);

            if (m_NoObject != null)
                m_NoObject.SetActive(false);
        }

        public void LeftPressed()
        {
            Debug.LogError("[RESPONSE MAPPING] LEFT = YES pressed.");

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(true);

            if (m_YesObject != null)
                m_YesObject.SetActive(true);
        }

        public void RightPressed()
        {
            Debug.LogError("[RESPONSE MAPPING] RIGHT = NO pressed.");

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(true);

            if (m_NoObject != null)
                m_NoObject.SetActive(true);
        }
    }
}