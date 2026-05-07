using UnityEngine;

namespace HitOrMiss
{
    [CreateAssetMenu(fileName = "TrajectoryTask", menuName = "Parkinson/HitOrMiss/Task Asset")]
    public class TrajectoryTaskAsset : ScriptableObject
    {
        [SerializeField] string m_TaskName = "Hit Or Miss Task";

        [Header("Protocol")]
        [Tooltip("Number of blocks (default 3)")]
        [SerializeField] int m_BlockCount = 3;

        [Tooltip("Trials per category per block (default 20, so 80 total per block)")]
        [SerializeField] int m_TrialsPerCategory = 20;

        [Header("Timing")]
        [SerializeField] float m_IntroDuration = 20f;
        [SerializeField] float m_RestDuration = 30f;
        [SerializeField] float m_OutroDuration = 10f;

        [Header("Trajectory (player-anchored, ball travels in toward player)")]
        [Tooltip("Distance in front of the player where every ball spawns (meters)")]
        [SerializeField] float m_SpawnDistance = 5f;

        [SerializeField] float m_BallDiameter = 0.175f;

        [Header("Per-participant scaling")]
        [Tooltip("Reference shoulder width in cm. Lateral offsets and curve magnitudes scale by " +
                 "(participant.shoulderWidthCm / this) so a wider participant gets proportionally wider " +
                 "near-hit / near-miss / miss bands. Default 42 cm (the PDF spec example value).")]
        [SerializeField] float m_ReferenceShoulderWidthCm = 42f;

        [Header("Lateral curve magnitude per category (peak outward bow at midpoint, meters)")]
        [Tooltip("Hit: 0 = straight line. The ball arrives at the player with no lateral deflection.")]
        [SerializeField] float m_HitCurveMagnitude = 0f;
        [Tooltip("NearHit: small outward curve.")]
        [SerializeField] float m_NearHitCurveMagnitude = 0.10f;
        [Tooltip("NearMiss: moderate outward curve.")]
        [SerializeField] float m_NearMissCurveMagnitude = 0.35f;
        [Tooltip("Miss: pronounced outward curve.")]
        [SerializeField] float m_MissCurveMagnitude = 0.65f;

        [Header("Speeds")]
        [SerializeField] float m_FastSpeed = 3.5f;
        [SerializeField] float m_SlowSpeed = 1.5f;

        [Header("Inter-trial interval (jittered, scheduled after each trial's response window closes)")]
        [SerializeField] float m_ItiMinSeconds = 1.5f;
        [SerializeField] float m_ItiMaxSeconds = 2.5f;

        [Header("Break between blocks")]
        [Tooltip("Countdown shown to the participant between blocks (seconds). The spec calls this 'Y minute breaks'.")]
        [SerializeField] float m_BreakDurationSeconds = 60f;

        [Header("Practice")]
        [Tooltip("Number of practice trials run after the LEFT/RIGHT mapping popups. Default 2.")]
        [SerializeField] int m_PracticeTrialCount = 2;

        [Tooltip("Seconds the HIT/MISS feedback flash stays on after each practice response.")]
        [SerializeField] float m_PracticeFeedbackSeconds = 1.0f;

        [Header("Popup localization keys (popups 1, 5, 6, 7, 8, 9)")]
        [SerializeField] string m_Popup1IntroKey  = "popup1_intro";
        [SerializeField] string m_Popup2LeftKey   = "popup2_left";
        [SerializeField] string m_Popup3RightKey  = "popup3_right";
        [SerializeField] string m_Popup4PracticeKey = "popup4_practice";
        [SerializeField] string m_Popup5ReadyKey  = "popup5_ready";
        [SerializeField] string m_Popup6BlockIntroKey = "popup6_block_intro";
        [SerializeField] string m_Popup7BreakKey  = "popup7_break";
        [SerializeField] string m_Popup8NextBlockKey = "popup8_next_block";
        [SerializeField] string m_Popup9OutroKey  = "popup9_outro";

        [Header("Speed grouping (consumed sequentially within each block; cycles if shorter than block)")]
        [Tooltip("Each entry defines one group: how many fast vs slow, and which comes first. Default: 7F3S, 3F7S, 7S3F, 6F4S, then cycle.")]
        [SerializeField]
        SpeedGroupPattern[] m_SpeedGroupPatterns =
        {
            new() { fastCount = 7, slowCount = 3, fastFirst = true  }, // 7 fast, 3 slow
            new() { fastCount = 3, slowCount = 7, fastFirst = true  }, // 3 fast, 7 slow
            new() { fastCount = 3, slowCount = 7, fastFirst = false }, // 7 slow, 3 fast
            new() { fastCount = 6, slowCount = 4, fastFirst = true  }, // 6 fast, 4 slow
        };

        public string TaskName => m_TaskName;
        public int BlockCount => m_BlockCount;
        public int TrialsPerCategory => m_TrialsPerCategory;
        public float IntroDuration => m_IntroDuration;
        public float RestDuration => m_RestDuration;
        public float OutroDuration => m_OutroDuration;

        public float SpawnDistance => m_SpawnDistance;
        public float BallDiameter => m_BallDiameter;

        public float HitCurveMagnitude => m_HitCurveMagnitude;
        public float NearHitCurveMagnitude => m_NearHitCurveMagnitude;
        public float NearMissCurveMagnitude => m_NearMissCurveMagnitude;
        public float MissCurveMagnitude => m_MissCurveMagnitude;

        public float FastSpeed => m_FastSpeed;
        public float SlowSpeed => m_SlowSpeed;
        public float ItiMinSeconds => m_ItiMinSeconds;
        public float ItiMaxSeconds => m_ItiMaxSeconds;
        public SpeedGroupPattern[] SpeedGroupPatterns => m_SpeedGroupPatterns;

        public float BreakDurationSeconds => m_BreakDurationSeconds;
        public int PracticeTrialCount => m_PracticeTrialCount;
        public float PracticeFeedbackSeconds => m_PracticeFeedbackSeconds;

        public string Popup1IntroKey       => m_Popup1IntroKey;
        public string Popup2LeftKey        => m_Popup2LeftKey;
        public string Popup3RightKey       => m_Popup3RightKey;
        public string Popup4PracticeKey    => m_Popup4PracticeKey;
        public string Popup5ReadyKey       => m_Popup5ReadyKey;
        public string Popup6BlockIntroKey  => m_Popup6BlockIntroKey;
        public string Popup7BreakKey       => m_Popup7BreakKey;
        public string Popup8NextBlockKey   => m_Popup8NextBlockKey;
        public string Popup9OutroKey       => m_Popup9OutroKey;

        public float ReferenceShoulderWidthCm => m_ReferenceShoulderWidthCm;

        public int TrialsPerBlock => m_TrialsPerCategory * 4; // 4 categories

        public float CurveMagnitudeFor(TrialCategory category) => category switch
        {
            TrialCategory.ClearHit  => m_HitCurveMagnitude,
            TrialCategory.NearHit   => m_NearHitCurveMagnitude,
            TrialCategory.NearMiss  => m_NearMissCurveMagnitude,
            TrialCategory.ClearMiss => m_MissCurveMagnitude,
            _ => 0f,
        };

        public TrialDefinition[] GenerateBlock(int blockIndex)
        {
            return TrialGenerator.GenerateBlock(blockIndex, this, 0f);
        }

        /// <summary>
        /// Builds a per-block trial list scaled to the participant's shoulder
        /// width. Pass 0 (or anything ≤ 0) to skip scaling and use the
        /// reference geometry. Called from
        /// <see cref="HitOrMissAppController"/> with the value from
        /// <see cref="SessionMetadata.shoulderWidthCm"/>.
        /// </summary>
        public TrialDefinition[] GenerateBlock(int blockIndex, float participantShoulderWidthCm)
        {
            return TrialGenerator.GenerateBlock(blockIndex, this, participantShoulderWidthCm);
        }

        public TrialDefinition[] GeneratePracticeTrials()
        {
            return TrialGenerator.GeneratePracticeTrials(this, 0f);
        }

        public TrialDefinition[] GeneratePracticeTrials(float participantShoulderWidthCm)
        {
            return TrialGenerator.GeneratePracticeTrials(this, participantShoulderWidthCm);
        }

        /// <summary>
        /// Returns a runtime-only clone of this asset. Modifications to the
        /// clone (via <see cref="ApplyTask2SessionOverrides"/> etc.) do not
        /// touch the on-disk source asset. Used by
        /// <see cref="HitOrMissAppController"/> at session start to apply
        /// clinician-form overrides without persisting them.
        /// </summary>
        public TrajectoryTaskAsset CreateSessionClone()
        {
            // Object.Instantiate copies every [SerializeField] member.
            var clone = Instantiate(this);
            clone.name = name + " (Session Clone)";
            return clone;
        }

        /// <summary>
        /// Mutates this asset (intended to be called only on a clone — see
        /// <see cref="CreateSessionClone"/>) so the values from the clinician
        /// form's task2_parameters drive the actual run.
        ///
        /// Form fields applied:
        ///   number_of_blocks       → BlockCount
        ///   trials_per_block       → TrialsPerCategory  (= total / 4)
        ///   break_duration_seconds → BreakDurationSeconds + RestDuration
        ///
        /// Offsets (hit/near_miss/miss) and ball_speeds are not yet mapped —
        /// the static category-offset bands and FastSpeed/SlowSpeed live on
        /// the asset directly. Future work: expose those as overridable too.
        /// </summary>
        public void ApplyTask2SessionOverrides(SessionMetadata md)
        {
            if (md.task2NumberOfBlocks > 0)
                m_BlockCount = md.task2NumberOfBlocks;

            if (md.task2TrialsPerBlock > 0)
            {
                // Asset stores trials per category; total per block = perCat * 4.
                int perCat = Mathf.Max(1, md.task2TrialsPerBlock / 4);
                m_TrialsPerCategory = perCat;
            }

            if (md.task2BreakDurationSeconds > 0f)
            {
                m_BreakDurationSeconds = md.task2BreakDurationSeconds;
                m_RestDuration = md.task2BreakDurationSeconds; // legacy alias
            }
        }

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;
            if (m_TrialsPerCategory < 1) m_TrialsPerCategory = 1;
            if (m_SpawnDistance <= 0f) m_SpawnDistance = 1f;
            if (m_FastSpeed <= 0f) m_FastSpeed = 0.5f;
            if (m_SlowSpeed <= 0f) m_SlowSpeed = 0.25f;
            if (m_FastSpeed <= m_SlowSpeed) m_FastSpeed = m_SlowSpeed + 0.1f;
            if (m_BallDiameter <= 0f) m_BallDiameter = 0.1f;
            if (m_HitCurveMagnitude < 0f) m_HitCurveMagnitude = 0f;
            if (m_NearHitCurveMagnitude < 0f) m_NearHitCurveMagnitude = 0f;
            if (m_NearMissCurveMagnitude < 0f) m_NearMissCurveMagnitude = 0f;
            if (m_MissCurveMagnitude < 0f) m_MissCurveMagnitude = 0f;
            if (m_ItiMinSeconds < 0f) m_ItiMinSeconds = 0f;
            if (m_ItiMaxSeconds < m_ItiMinSeconds) m_ItiMaxSeconds = m_ItiMinSeconds;
        }
    }
}
