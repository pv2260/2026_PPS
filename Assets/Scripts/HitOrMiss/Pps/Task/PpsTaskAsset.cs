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

        [Tooltip("How many times each factor cell is repeated within a block")]
        [SerializeField] int m_RepeatsPerCell = 2;

        [Tooltip("If false, TactileOnly cells are skipped in the factorial pool")]
        [SerializeField] bool m_IncludeTactileOnlyCells = true;

        [Header("Loom timing")]
        [SerializeField] float m_FastDurationSeconds = 1.5f;
        [SerializeField] float m_SlowDurationSeconds = 3.5f;

        [Tooltip("TactileOnly trials have no visible looming. How long the trial window lasts.")]
        [SerializeField] float m_TactileOnlyDurationSeconds = 2.0f;

        [Tooltip("Extra seconds after loom/vibration ends during which a response is still accepted")]
        [SerializeField] float m_ResponseGracePeriodSeconds = 1.0f;

        [Tooltip("Pause between trials")]
        [SerializeField] float m_InterTrialIntervalSeconds = 1.5f;

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

        [Header("Vibrotactile placeholder")]
        [Tooltip("Duration the on-screen 'VIBRATION NOW' / hardware pulse stays on")]
        [SerializeField] float m_VibrationDurationMs = 300f;
        [Range(0f, 1f)]
        [SerializeField] float m_VibrationIntensity = 1f;

        [Header("Phase durations (auto-advance phases only)")]
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
        public int RepeatsPerCell => m_RepeatsPerCell;
        public bool IncludeTactileOnlyCells => m_IncludeTactileOnlyCells;

        public float FastDurationSeconds => m_FastDurationSeconds;
        public float SlowDurationSeconds => m_SlowDurationSeconds;
        public float TactileOnlyDurationSeconds => m_TactileOnlyDurationSeconds;
        public float ResponseGracePeriodSeconds => m_ResponseGracePeriodSeconds;
        public float InterTrialIntervalSeconds => m_InterTrialIntervalSeconds;

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

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;
            if (m_RepeatsPerCell < 1) m_RepeatsPerCell = 1;
            if (m_FastDurationSeconds <= 0f) m_FastDurationSeconds = 0.1f;
            if (m_SlowDurationSeconds <= m_FastDurationSeconds) m_SlowDurationSeconds = m_FastDurationSeconds + 0.1f;
            if (m_DistanceD4 <= m_DistanceD3) m_DistanceD4 = m_DistanceD3 + 0.01f;
            if (m_DistanceD3 <= m_DistanceD2) m_DistanceD3 = m_DistanceD2 + 0.01f;
            if (m_DistanceD2 <= m_DistanceD1) m_DistanceD2 = m_DistanceD1 + 0.01f;
            if (m_DistanceD1 <= 0f) m_DistanceD1 = 0.01f;
            if (m_WideSeparation <= m_NarrowSeparation) m_WideSeparation = m_NarrowSeparation + 0.01f;
        }
    }
}
