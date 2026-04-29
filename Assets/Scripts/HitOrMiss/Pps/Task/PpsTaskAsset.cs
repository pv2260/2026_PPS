using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// ScriptableObject container for all PPS protocol parameters.
    /// Edit in the Inspector (one asset per study) — no recompilation needed.
    /// </summary>
    [CreateAssetMenu(fileName = "PpsTask", menuName = "Parkinson/HitOrMiss/PPS Task Asset")]
    public class PpsTaskAsset : ScriptableObject
    {
        [SerializeField] string m_TaskName = "PPS Looming Task";

        [Header("Protocol")]
        [Tooltip("Number of experimental blocks")]
        [SerializeField] int m_BlockCount = 3;

        [Tooltip("Target trials per block. Mix of VT / V / T follows the percentages below.")]
        [SerializeField] int m_TrialsPerBlock = 80;

        [Range(0f, 1f)]
        [Tooltip("Fraction of trials that are visuotactile (looming + vibration).")]
        [SerializeField] float m_PercentVT = 0.70f;

        [Range(0f, 1f)]
        [Tooltip("Fraction of trials that are visual-only (looming, no vibration — catch trials).")]
        [SerializeField] float m_PercentV = 0.15f;

        [Range(0f, 1f)]
        [Tooltip("Fraction of trials that are tactile-only (vibration only, no looming — unisensory baseline).")]
        [SerializeField] float m_PercentT = 0.15f;

        [Header("Loom timing")]
        [SerializeField] float m_FastDurationSeconds = 1.5f;
        [SerializeField] float m_SlowDurationSeconds = 3.5f;

        [Tooltip("Extra seconds after loom/vibration during which a response is still accepted")]
        [SerializeField] float m_ResponseGracePeriodSeconds = 1.0f;

        [Header("Inter-trial interval (jittered, seconds)")]
        [SerializeField] float m_ItiMinSeconds = 1.5f;
        [SerializeField] float m_ItiMaxSeconds = 2.5f;

        [Header("Motion curve (shared by visual loom and tactile-only timing)")]
        [Tooltip("Normalized loom progress t ∈ [0,1] → curved progress. Stage thresholds are 0.25/0.5/0.75 on the curved axis.")]
        [SerializeField] AnimationCurve m_MotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Spatial layout (meters)")]
        [SerializeField] float m_WideSeparation = 0.40f;
        [SerializeField] float m_NarrowSeparation = 0.16f;
        [SerializeField] float m_LedHeight = 0.15f;

        [Tooltip("Distance from body to D4 (far stage / spawn)")]
        [SerializeField] float m_DistanceD4 = 2.0f;
        [SerializeField] float m_DistanceD3 = 1.5f;
        [SerializeField] float m_DistanceD2 = 1.0f;
        [Tooltip("Distance from body to D1 (near stage / vanish)")]
        [SerializeField] float m_DistanceD1 = 0.6f;

        [Header("Scale growth (looming cue)")]
        [SerializeField] Vector3 m_ScaleAtD4 = new(0.02f, 0.02f, 0.02f);
        [SerializeField] Vector3 m_ScaleAtD1 = new(0.09f, 0.09f, 0.09f);

        [Header("Vibrotactile")]
        [SerializeField] float m_VibrationDurationMs = 300f;
        [Range(0f, 1f)]
        [SerializeField] float m_VibrationIntensity = 1f;

        [Header("Phase durations")]
        [SerializeField] float m_RestDurationSeconds = 30f;

        [Header("Phase content (Localization keys)")]
        [SerializeField] string m_InstructionsKey = "pps.instructions";
        [SerializeField] string m_PracticeIntroKey = "pps.practice_intro";
        [SerializeField] string m_BlockIntroKey = "pps.block_intro";
        [SerializeField] string m_OutroKey = "pps.outro";

        [Header("Trial generator")]
        [SerializeField] TrialOrder m_OrderingStrategy = TrialOrder.Shuffled;

        [Tooltip("-1 = time-seeded (non-reproducible). Any other value = reproducible seed.")]
        [SerializeField] int m_RngSeed = -1;

        // ---- Public getters ----
        public string TaskName => m_TaskName;
        public int BlockCount => m_BlockCount;
        public int TrialsPerBlock => m_TrialsPerBlock;
        public float PercentVT => m_PercentVT;
        public float PercentV => m_PercentV;
        public float PercentT => m_PercentT;

        public float FastDurationSeconds => m_FastDurationSeconds;
        public float SlowDurationSeconds => m_SlowDurationSeconds;
        public float ResponseGracePeriodSeconds => m_ResponseGracePeriodSeconds;

        public float ItiMinSeconds => m_ItiMinSeconds;
        public float ItiMaxSeconds => m_ItiMaxSeconds;

        public AnimationCurve MotionCurve => m_MotionCurve;

        public float WideSeparation => m_WideSeparation;
        public float NarrowSeparation => m_NarrowSeparation;
        public float LedHeight => m_LedHeight;

        public float DistanceD4 => m_DistanceD4;
        public float DistanceD3 => m_DistanceD3;
        public float DistanceD2 => m_DistanceD2;
        public float DistanceD1 => m_DistanceD1;

        public Vector3 ScaleAtD4 => m_ScaleAtD4;
        public Vector3 ScaleAtD1 => m_ScaleAtD1;

        public float VibrationDurationMs => m_VibrationDurationMs;
        public float VibrationIntensity => m_VibrationIntensity;

        public float RestDurationSeconds => m_RestDurationSeconds;

        public string InstructionsKey => m_InstructionsKey;
        public string PracticeIntroKey => m_PracticeIntroKey;
        public string BlockIntroKey => m_BlockIntroKey;
        public string OutroKey => m_OutroKey;

        public TrialOrder OrderingStrategy => m_OrderingStrategy;
        public int? RngSeed => m_RngSeed < 0 ? null : m_RngSeed;

        public float DurationFor(PpsSpeed speed) => speed == PpsSpeed.Fast ? m_FastDurationSeconds : m_SlowDurationSeconds;
        public float SeparationFor(PpsWidth width) => width == PpsWidth.Wide ? m_WideSeparation : m_NarrowSeparation;

        public PpsTrialDefinition[] GenerateBlock(int blockIndex) => PpsTrialGenerator.Generate(this, blockIndex);

        /// <summary>
        /// Elapsed seconds from loom onset at which the motion curve reaches the given stage boundary.
        /// Used by tactile-only trials to fire at a time-matched moment. D4 → 0. Otherwise numerically
        /// inverts MotionCurve to find t such that curve(t) crosses 0.25 / 0.5 / 0.75.
        /// </summary>
        public float TimeToReachStage(PpsSpeed speed, DistanceStage stage)
        {
            float threshold = stage switch
            {
                DistanceStage.D4 => 0f,
                DistanceStage.D3 => 0.25f,
                DistanceStage.D2 => 0.50f,
                DistanceStage.D1 => 0.75f,
                _ => 0f,
            };
            float duration = DurationFor(speed);
            if (threshold <= 0f) return 0f;

            const int steps = 512;
            float prevT = 0f;
            float prevY = m_MotionCurve.Evaluate(0f);
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = m_MotionCurve.Evaluate(t);
                if (y >= threshold)
                {
                    float frac = Mathf.Approximately(y, prevY) ? 0f : (threshold - prevY) / (y - prevY);
                    return Mathf.Lerp(prevT, t, frac) * duration;
                }
                prevT = t;
                prevY = y;
            }
            return duration;
        }

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;
            if (m_TrialsPerBlock < 1) m_TrialsPerBlock = 1;
            if (m_FastDurationSeconds <= 0f) m_FastDurationSeconds = 0.1f;
            if (m_SlowDurationSeconds <= m_FastDurationSeconds) m_SlowDurationSeconds = m_FastDurationSeconds + 0.1f;
            if (m_DistanceD4 <= m_DistanceD3) m_DistanceD4 = m_DistanceD3 + 0.01f;
            if (m_DistanceD3 <= m_DistanceD2) m_DistanceD3 = m_DistanceD2 + 0.01f;
            if (m_DistanceD2 <= m_DistanceD1) m_DistanceD2 = m_DistanceD1 + 0.01f;
            if (m_DistanceD1 <= 0f) m_DistanceD1 = 0.01f;
            if (m_WideSeparation <= m_NarrowSeparation) m_WideSeparation = m_NarrowSeparation + 0.01f;
            if (m_ItiMinSeconds < 0f) m_ItiMinSeconds = 0f;
            if (m_ItiMaxSeconds < m_ItiMinSeconds) m_ItiMaxSeconds = m_ItiMinSeconds;

            float sum = m_PercentVT + m_PercentV + m_PercentT;
            if (sum > 0.0001f && Mathf.Abs(sum - 1f) > 0.001f)
            {
                m_PercentVT /= sum;
                m_PercentV /= sum;
                m_PercentT /= sum;
            }
        }
    }
}
