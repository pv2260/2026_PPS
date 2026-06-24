using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CybersicknessStimulusController : MonoBehaviour
    {
        [Header("Visual root")]
        [Tooltip("Root object for the visual environment: tunnel, starfield, checkerboard room, etc.")]
        [SerializeField] Transform m_VisualRoot;

        [Header("Optional optic-flow material")]
        [SerializeField] Material m_OpticFlowMaterial;

        Vector3 m_InitialPosition;
        Quaternion m_InitialRotation;
        Vector2 m_InitialTextureOffset;

        float m_Intensity;
        float m_MaxOpticFlowSpeed;
        float m_MaxYawDegPerSecond;
        float m_MaxRollAmplitudeDeg;
        float m_RollFrequencyHz;

        bool m_Running;
        float m_StartTime;

        void Awake()
        {
            if (m_VisualRoot == null)
                m_VisualRoot = transform;

            m_InitialPosition = m_VisualRoot.localPosition;
            m_InitialRotation = m_VisualRoot.localRotation;

            if (m_OpticFlowMaterial != null)
                m_InitialTextureOffset = m_OpticFlowMaterial.mainTextureOffset;
        }

        public void Configure(CybersicknessTaskAsset asset)
        {
            if (asset == null) return;

            m_MaxOpticFlowSpeed = asset.MaxOpticFlowSpeed;
            m_MaxYawDegPerSecond = asset.MaxYawDegPerSecond;
            m_MaxRollAmplitudeDeg = asset.MaxRollAmplitudeDeg;
            m_RollFrequencyHz = asset.RollFrequencyHz;
        }

        public void StartStimulus(float intensity)
        {
            ResetStimulus();

            m_Intensity = Mathf.Clamp01(intensity);
            m_StartTime = Time.time;
            m_Running = true;
        }

        public void SetIntensity(float intensity)
        {
            m_Intensity = Mathf.Clamp01(intensity);
        }

        public void StopStimulus()
        {
            m_Running = false;
            ResetStimulus();
        }

        void Update()
        {
            if (!m_Running) return;

            float elapsed = Time.time - m_StartTime;

            ApplyWorldRotation(elapsed);
            ApplyOpticFlow(elapsed);
        }

        void ApplyWorldRotation(float elapsed)
        {
            float yaw = m_MaxYawDegPerSecond * m_Intensity * elapsed;

            float roll =
                Mathf.Sin(elapsed * Mathf.PI * 2f * m_RollFrequencyHz)
                * m_MaxRollAmplitudeDeg
                * m_Intensity;

            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            Quaternion rollRotation = Quaternion.Euler(0f, 0f, roll);

            m_VisualRoot.localRotation = m_InitialRotation * yawRotation * rollRotation;
        }

        void ApplyOpticFlow(float elapsed)
        {
            if (m_OpticFlowMaterial == null) return;

            Vector2 offset = m_InitialTextureOffset;
            offset.y += m_MaxOpticFlowSpeed * m_Intensity * elapsed;

            m_OpticFlowMaterial.mainTextureOffset = offset;
        }

        void ResetStimulus()
        {
            if (m_VisualRoot != null)
            {
                m_VisualRoot.localPosition = m_InitialPosition;
                m_VisualRoot.localRotation = m_InitialRotation;
            }

            if (m_OpticFlowMaterial != null)
                m_OpticFlowMaterial.mainTextureOffset = m_InitialTextureOffset;
        }
    }
}