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

        [Tooltip("Total trials per block. Automatically forced to VT + V + T.")]
        [SerializeField] int m_TrialsPerBlock = 40;

        [Header("Trial counts per block")]

        [Tooltip("Number of visuotactile trials per block: looming + vibration.")]
        [SerializeField, Min(0)] int m_VtTrialsPerBlock = 28;

        [Tooltip("Number of visual-only trials per block: looming, no vibration.")]
        [SerializeField, Min(0)] int m_VisualOnlyTrialsPerBlock = 6;

        [Tooltip("Number of tactile-only trials per block: vibration only, no looming.")]
        [SerializeField, Min(0)] int m_TactileOnlyTrialsPerBlock = 6;

        [Header("Loom timing")]
        [SerializeField] float m_FastDurationSeconds = 1.5f;
        [SerializeField] float m_SlowDurationSeconds = 3.5f;

        [Tooltip("Extra seconds after loom/vibration during which a response is still accepted")]
        [SerializeField] float m_ResponseGracePeriodSeconds = 1.0f;

        [Header("Inter-trial interval (jittered, seconds)")]
        [SerializeField] float m_ItiMinSeconds = 1.5f;
        [SerializeField] float m_ItiMaxSeconds = 2.5f;

        [Header("Motion curve (shared by visual loom and tactile-only timing)")]
        [Tooltip("Normalized loom progress t ∈ [0,1] → curved progress. Stage thresholds are split across the equally spaced distance stages.")]
        [SerializeField] AnimationCurve m_MotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Spatial layout (all values in METERS, measured from the body anchor)")]

        [Tooltip("Distance forward from the body anchor to the fixation crosshair (meters). Usually slightly farther than the loom start distance.")]
        [SerializeField, Min(0.01f)] float m_CrosshairDistance = 2.6f;

        [Tooltip("Vertical offset of the crosshair above the body anchor (meters). Typically eye level.")]
        [SerializeField, Min(0f)] float m_CrosshairHeight = 1.4f;

        [Tooltip("Fallback shoulder width in meters. Used as the narrow LED separation when no participant-specific value is provided.")]
        [SerializeField, Min(0.01f)] float m_DefaultShoulderWidthMeters = 0.40f;

        [Tooltip("Extra meters added to the narrow separation for the WIDE condition.")]
        [SerializeField, Min(0f)] float m_WideOffsetMeters = 0.30f;

        [Tooltip("Vertical offset of the side LEDs relative to the body anchor.")]
        [SerializeField] float m_LedHeight = 0f;

        [Header("Distance stages")]

        [Tooltip("Farthest point / loom start. This corresponds to D7 when using 7 stages.")]
        [SerializeField, Min(0.01f)] float m_LoomStartDistance = 2.4f;

        [Tooltip("Nearest point / loom end. This corresponds to D1 when using 7 stages.")]
        [SerializeField, Min(0.01f)] float m_LoomEndDistance = 0.6f;

        [Tooltip("Number of equally spaced distance stages, including start and end. Use 7 for D7..D1.")]
        [SerializeField, Min(2)] int m_DistanceStageCount = 7;

        [Header("Scale growth (looming cue)")]

        [Tooltip("Scale of the looming lights at the farthest stage.")]
        [SerializeField] Vector3 m_ScaleAtD7 = new(0.02f, 0.02f, 0.02f);

        [Tooltip("Scale of the looming lights at the nearest stage.")]
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

        public int VtTrialsPerBlock => m_VtTrialsPerBlock;
        public int VisualOnlyTrialsPerBlock => m_VisualOnlyTrialsPerBlock;
        public int TactileOnlyTrialsPerBlock => m_TactileOnlyTrialsPerBlock;

        public float FastDurationSeconds => m_FastDurationSeconds;
        public float SlowDurationSeconds => m_SlowDurationSeconds;
        public float ResponseGracePeriodSeconds => m_ResponseGracePeriodSeconds;

        public float ItiMinSeconds => m_ItiMinSeconds;
        public float ItiMaxSeconds => m_ItiMaxSeconds;

        public AnimationCurve MotionCurve => m_MotionCurve;

        public float DefaultShoulderWidthMeters => m_DefaultShoulderWidthMeters;
        public float WideOffsetMeters => m_WideOffsetMeters;

        public float CrosshairDistance => m_CrosshairDistance;
        public float CrosshairHeight => m_CrosshairHeight;

        public float NarrowSeparation => m_DefaultShoulderWidthMeters;
        public float WideSeparation => m_DefaultShoulderWidthMeters + m_WideOffsetMeters;

        public float LedHeight => m_LedHeight;

        public float LoomStartDistance => m_LoomStartDistance;
        public float LoomEndDistance => m_LoomEndDistance;
        public int DistanceStageCount => m_DistanceStageCount;

        public Vector3 ScaleAtD7 => m_ScaleAtD7;

        // Kept for compatibility if LoomingPairController still calls ScaleAtD4.
        // It now returns the far-stage scale, which is D7.
        public Vector3 ScaleAtD4 => m_ScaleAtD7;

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

        // ---- Compatibility getters for existing DistanceLayout code ----
        // These are now computed automatically from start/end/stage count.

        public float DistanceD7 => DistanceForStage(DistanceStage.D7);
        public float DistanceD6 => DistanceForStage(DistanceStage.D6);
        public float DistanceD5 => DistanceForStage(DistanceStage.D5);
        public float DistanceD4 => DistanceForStage(DistanceStage.D4);
        public float DistanceD3 => DistanceForStage(DistanceStage.D3);
        public float DistanceD2 => DistanceForStage(DistanceStage.D2);
        public float DistanceD1 => DistanceForStage(DistanceStage.D1);

        public float DurationFor(PpsSpeed speed)
        {
            return speed == PpsSpeed.Fast ? m_FastDurationSeconds : m_SlowDurationSeconds;
        }

        public float SeparationFor(PpsWidth width)
        {
            return SeparationFor(width, 0f);
        }

        public float SeparationFor(PpsWidth width, float participantShoulderWidthMeters)
        {
            float shoulder = participantShoulderWidthMeters > 0f
                ? participantShoulderWidthMeters
                : m_DefaultShoulderWidthMeters;

            float narrow = Mathf.Max(shoulder, m_DefaultShoulderWidthMeters);

            return width == PpsWidth.Wide
                ? narrow + m_WideOffsetMeters
                : narrow;
        }

        public PpsTrialDefinition[] GenerateBlock(int blockIndex)
        {
            return PpsTrialGenerator.Generate(this, blockIndex);
        }

        /// <summary>
        /// Returns the normalized progress value for a given distance stage.
        /// With 7 stages:
        /// D7 = 0/6 = 0.000
        /// D6 = 1/6 = 0.167
        /// D5 = 2/6 = 0.333
        /// D4 = 3/6 = 0.500
        /// D3 = 4/6 = 0.667
        /// D2 = 5/6 = 0.833
        /// D1 = 6/6 = 1.000
        /// </summary>
        public float ProgressForStage(DistanceStage stage)
        {
            int index = StageIndex(stage);
            int maxIndex = Mathf.Max(1, m_DistanceStageCount - 1);

            return Mathf.Clamp01((float)index / maxIndex);
        }

        /// <summary>
        /// Returns the distance in meters for a given stage.
        /// Intermediate stages are equally spaced between loom start and loom end.
        /// </summary>
        public float DistanceForStage(DistanceStage stage)
        {
            float progress = ProgressForStage(stage);
            return Mathf.Lerp(m_LoomStartDistance, m_LoomEndDistance, progress);
        }

        /// <summary>
        /// Elapsed seconds from loom onset at which the motion curve reaches the given stage.
        /// Used by tactile-only trials to fire at a time-matched moment.
        /// </summary>
        public float TimeToReachStage(PpsSpeed speed, DistanceStage stage)
        {
            float threshold = ProgressForStage(stage);

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
                    float frac = Mathf.Approximately(y, prevY)
                        ? 0f
                        : (threshold - prevY) / (y - prevY);

                    return Mathf.Lerp(prevT, t, frac) * duration;
                }

                prevT = t;
                prevY = y;
            }

            return duration;
        }

        int StageIndex(DistanceStage stage)
        {
            // This assumes the standard PPS labels D7..D1.
            // If m_DistanceStageCount is 7, all stages are used.
            // If fewer stages are used, the index is clamped to the available range.
            int index = stage switch
            {
                DistanceStage.D7 => 0,
                DistanceStage.D6 => 1,
                DistanceStage.D5 => 2,
                DistanceStage.D4 => 3,
                DistanceStage.D3 => 4,
                DistanceStage.D2 => 5,
                DistanceStage.D1 => 6,
                _ => 0,
            };

            int maxIndex = Mathf.Max(1, m_DistanceStageCount - 1);
            return Mathf.Clamp(index, 0, maxIndex);
        }

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;

            if (m_VtTrialsPerBlock < 0) m_VtTrialsPerBlock = 0;
            if (m_VisualOnlyTrialsPerBlock < 0) m_VisualOnlyTrialsPerBlock = 0;
            if (m_TactileOnlyTrialsPerBlock < 0) m_TactileOnlyTrialsPerBlock = 0;

            m_TrialsPerBlock =
                m_VtTrialsPerBlock +
                m_VisualOnlyTrialsPerBlock +
                m_TactileOnlyTrialsPerBlock;

            if (m_TrialsPerBlock < 1)
            {
                m_VtTrialsPerBlock = 1;
                m_VisualOnlyTrialsPerBlock = 0;
                m_TactileOnlyTrialsPerBlock = 0;
                m_TrialsPerBlock = 1;
            }

            if (m_FastDurationSeconds <= 0f) m_FastDurationSeconds = 0.1f;
            if (m_SlowDurationSeconds <= 0f) m_SlowDurationSeconds = 0.1f;

            if (m_LoomStartDistance <= 0f) m_LoomStartDistance = 0.01f;
            if (m_LoomEndDistance <= 0f) m_LoomEndDistance = 0.01f;

            if (m_DistanceStageCount < 2)
                m_DistanceStageCount = 2;

            if (m_LoomStartDistance <= m_LoomEndDistance)
            {
                Debug.LogWarning(
                    $"[PpsTaskAsset] '{name}' has loom distances out of order. " +
                    $"Expected LoomStartDistance > LoomEndDistance. " +
                    $"Got start={m_LoomStartDistance}, end={m_LoomEndDistance}."
                );
            }

            if (m_DefaultShoulderWidthMeters <= 0f)
                m_DefaultShoulderWidthMeters = 0.40f;

            if (m_CrosshairDistance <= 0f) m_CrosshairDistance = 0.1f;
            if (m_CrosshairHeight < 0f) m_CrosshairHeight = 0f;

            if (m_WideOffsetMeters < 0f)
                m_WideOffsetMeters = 0f;

            if (m_ItiMinSeconds < 0f) m_ItiMinSeconds = 0f;
            if (m_ItiMaxSeconds < 0f) m_ItiMaxSeconds = 0f;

            if (m_ScaleAtD7.x <= 0f || m_ScaleAtD7.y <= 0f || m_ScaleAtD7.z <= 0f)
                m_ScaleAtD7 = Vector3.one * 0.02f;

            if (m_ScaleAtD1.x <= 0f || m_ScaleAtD1.y <= 0f || m_ScaleAtD1.z <= 0f)
                m_ScaleAtD1 = Vector3.one * 0.09f;
        }
    }
}