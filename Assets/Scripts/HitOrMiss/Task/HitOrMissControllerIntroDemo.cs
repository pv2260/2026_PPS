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

        [Header("Advance delay")]
        [SerializeField] private float m_AfterBothTriggersDelaySeconds = 0.6f;

        private float m_BothTriggersCompletedTime = -1f;

        [TextArea(2, 5)]
        [SerializeField] private string m_DemoText =
            "First, let's learn which buttons to press.\n\nPress the TRIGGER on EACH controller.";

        private bool m_LeftPressed;
        private bool m_RightPressed;

        public bool HasPressedBothTriggers => m_LeftPressed && m_RightPressed;

        public bool ReadyToAdvance =>
            HasPressedBothTriggers &&
            m_BothTriggersCompletedTime > 0f &&
            Time.time >= m_BothTriggersCompletedTime + m_AfterBothTriggersDelaySeconds;

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

            m_BothTriggersCompletedTime = -1f;

            if (m_InstructionText != null)
                m_InstructionText.text = m_DemoText;

            if (m_LeftTriggerHighlight != null)
                m_LeftTriggerHighlight.SetActive(false);

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(false);
        }

        private void CheckBothTriggersCompleted()
        {
            if (m_LeftPressed && m_RightPressed && m_BothTriggersCompletedTime < 0f)
            {
                m_BothTriggersCompletedTime = Time.time;
                Debug.Log($"[HIT OR MISS TRIGGER DEMO] Both triggers completed at {m_BothTriggersCompletedTime:F3}");
            }
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

            CheckBothTriggersCompleted();
        }

        public void RightTriggerPressed()
        {
            m_RightPressed = true;

            Debug.Log("[HIT OR MISS TRIGGER DEMO] Right trigger pressed.");

            if (m_RightTriggerHighlight != null)
                m_RightTriggerHighlight.SetActive(true);
            else
                Debug.LogError("[HIT OR MISS TRIGGER DEMO] RightTriggerHighlight is not assigned.");

            CheckBothTriggersCompleted();
        }
    }
}