using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    [CreateAssetMenu(
        fileName = "CybersicknessTask",
        menuName = "Parkinson/Cybersickness/Task Asset")]
    public class CybersicknessTaskAsset : ScriptableObject
    {
        [SerializeField] string m_TaskName = "Cybersickness Controller Task";

        [Header("Intensity")]
        [SerializeField] float m_StartIntensity = 0.10f;
        [SerializeField] float m_IntensityStep = 0.05f;
        [SerializeField] float m_MaxIntensity = 1.00f;

        [Header("Visual motion at max intensity")]
        [SerializeField] float m_MaxOpticFlowSpeed = 1.00f;
        [SerializeField] float m_MaxYawDegPerSecond = 45f;
        [SerializeField] float m_MaxRollAmplitudeDeg = 8f;
        [SerializeField] float m_RollFrequencyHz = 0.20f;

        [Header("Safety")]
        [Tooltip("Maximum task duration in seconds. The task auto-stops if this is reached.")]
        [SerializeField] float m_MaxDurationSeconds = 180f;

        [Header("Instructions")]
        [TextArea(4, 12)]
        [SerializeField] string m_IntroText =
            "In this task, you will see a moving visual environment.\n\n" +
            "Your goal is to adjust the motion using the controllers.\n\n" +
            "Increase the motion until it becomes uncomfortable, then stop the task.";

        [TextArea(4, 12)]
        [SerializeField] string m_ControllerGuideText =
            "Controller guide:\n\n" +
            "RIGHT trigger / A button: increase motion\n" +
            "LEFT trigger / X button: decrease motion\n" +
            "B button / grip: stop when uncomfortable\n\n" +
            "Press START when you are ready.";

        [TextArea(2, 8)]
        [SerializeField] string m_RatingText =
            "How sick or uncomfortable do you feel right now?\n\n" +
            "0 = not at all\n" +
            "10 = extremely uncomfortable";

        public string TaskName => m_TaskName;

        public float StartIntensity => m_StartIntensity;
        public float IntensityStep => m_IntensityStep;
        public float MaxIntensity => m_MaxIntensity;

        public float MaxOpticFlowSpeed => m_MaxOpticFlowSpeed;
        public float MaxYawDegPerSecond => m_MaxYawDegPerSecond;
        public float MaxRollAmplitudeDeg => m_MaxRollAmplitudeDeg;
        public float RollFrequencyHz => m_RollFrequencyHz;

        public float MaxDurationSeconds => m_MaxDurationSeconds;

        public string IntroText => m_IntroText;
        public string ControllerGuideText => m_ControllerGuideText;
        public string RatingText => m_RatingText;

        void OnValidate()
        {
            if (m_StartIntensity < 0f) m_StartIntensity = 0f;
            if (m_IntensityStep <= 0f) m_IntensityStep = 0.05f;
            if (m_MaxIntensity <= 0f) m_MaxIntensity = 1f;
            if (m_StartIntensity > m_MaxIntensity) m_StartIntensity = m_MaxIntensity;

            if (m_MaxDurationSeconds < 10f) m_MaxDurationSeconds = 10f;
            if (m_RollFrequencyHz < 0f) m_RollFrequencyHz = 0f;
        }
    }
}