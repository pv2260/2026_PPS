using UnityEngine;

namespace HitOrMiss
{
    [CreateAssetMenu(fileName = "TrajectoryTask", menuName = "Parkinson/HitOrMiss/Task Asset")]
    public class TrajectoryTaskAsset : ScriptableObject
    {
        [SerializeField] string m_TaskName = "Hit Or Miss Task";

        [Header("Protocol")]
        [Tooltip("Number of blocks.")]
        [SerializeField] int m_BlockCount = 2;

        [Header("Trials per block by category")]
        [Tooltip("Clear hit trials per block. Ball enters the shoulder/body boundary.")]
        [SerializeField] int m_ClearHitTrialsPerBlock = 30;

        [Tooltip("Near hit / ambiguous outside trials per block. Ball passes just outside the shoulder edge.")]
        [SerializeField] int m_NearHitTrialsPerBlock = 50;

        [Tooltip("Near miss trials per block. Ball passes outside the shoulder edge by a moderate margin.")]
        [SerializeField] int m_NearMissTrialsPerBlock = 50;

        [Tooltip("Clear miss trials per block. Ball clearly passes outside the shoulder edge.")]
        [SerializeField] int m_ClearMissTrialsPerBlock = 30;

        [Header("Timing")]
        [SerializeField] float m_IntroDuration = 20f;
        [SerializeField] float m_RestDuration = 30f;
        [SerializeField] float m_OutroDuration = 10f;

        [Header("Trajectory (player-anchored, ball travels in toward player)")]
        [Tooltip("Distance in front of the player where every ball spawns (meters)")]
        [SerializeField] float m_SpawnDistance = 5f;

        [SerializeField] float m_BallDiameter = 0.175f;

        [Header("Per-participant scaling")]
        [Tooltip("Design-baseline shoulder width in cm. NOT the current participant's value — that " +
                 "lives in session metadata. Real participants are scaled by " +
                 "(participant.shoulderWidthCm / this), so a wider participant gets proportionally " +
                 "wider near-hit / near-miss / miss bands. Default 42 cm (the PDF spec example).")]
        
        [SerializeField] float m_ReferenceShoulderWidthCm = 42f;
        [Header("Offset bands relative to shoulder edge, in cm")]

        [Tooltip("Clear hit: ball enters the shoulder/body boundary. Negative means inside the shoulder edge.")]
        [SerializeField] float m_ClearHitMinOffsetCm = -15f;

        [SerializeField] float m_ClearHitMaxOffsetCm = 0f;

        [Tooltip("Near hit / ambiguous outside: ball passes very close to the shoulder edge.")]
        [SerializeField] float m_NearHitMinOffsetCm = 1f;

        [SerializeField] float m_NearHitMaxOffsetCm = 15f;

        [Tooltip("Near miss: ball passes outside the shoulder edge by a smaller margin.")]
        [SerializeField] float m_NearMissMinOffsetCm = 15f;

        [SerializeField] float m_NearMissMaxOffsetCm = 30f;

        [Tooltip("Clear miss: ball clearly passes outside the shoulder edge.")]
        [SerializeField] float m_ClearMissMinOffsetCm = 30f;

        [SerializeField] float m_ClearMissMaxOffsetCm = 60f;

        [Tooltip("If false, offset bands remain exactly as entered above.")]
        [SerializeField] bool m_ScaleOffsetBandsByShoulderWidth = true;

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
        [Tooltip("Legacy generic practice trial count. Not used by the current easy/hard practice flow.")]
        [SerializeField] int m_PracticeTrialCount = 2;

        [Tooltip("Seconds the HIT/MISS feedback flash stays on after each practice response.")]
        [SerializeField] float m_PracticeFeedbackSeconds = 1.0f;

        [Header("Easy practice composition")]
        [Tooltip("Easy practice: number of clear hit trials.")]
        [SerializeField] int m_EasyPracticeClearHits = 1;

        [Tooltip("Easy practice: number of clear miss trials.")]
        [SerializeField] int m_EasyPracticeClearMisses = 1;

        [Tooltip("Easy practice: number of near hit trials. Usually 0 because easy practice should be unambiguous.")]
        [SerializeField] int m_EasyPracticeNearHits = 0;

        [Tooltip("Easy practice: number of near miss trials. Usually 0 because easy practice should be unambiguous.")]
        [SerializeField] int m_EasyPracticeNearMisses = 0;

        [Tooltip("If errors are greater than or equal to this value, easy practice repeats and the retry popup is shown.")]
        [SerializeField] int m_EasyPracticeErrorThreshold = 0;

        [Header("Hard / difficult practice composition")]
        [Tooltip("Hard practice: number of clear hit trials.")]
        [SerializeField] int m_HardPracticeClearHits = 1;

        [Tooltip("Hard practice: number of clear miss trials.")]
        [SerializeField] int m_HardPracticeClearMisses = 1;

        [Tooltip("Hard practice: number of near hit trials.")]
        [SerializeField] int m_HardPracticeNearHits = 1;

        [Tooltip("Hard practice: number of near miss trials.")]
        [SerializeField] int m_HardPracticeNearMisses = 1;

        [Tooltip("If errors are greater than or equal to this value, hard/difficult practice repeats and the retry popup is shown.")]
        [SerializeField] int m_HardPracticeErrorThreshold = 3;

        [Header("Popup localization keys (popups 1, 5, 6, 7, 8, 9)")]
        [SerializeField] string m_Popup1IntroKey = "popup1_intro";
        [SerializeField] string m_Popup2LeftKey = "popup2_left";
        [SerializeField] string m_Popup3RightKey = "popup3_right";
        [SerializeField] string m_Popup4PracticeKey = "popup4_practice";
        [SerializeField] string m_Popup5ReadyKey = "popup5_ready";
        [SerializeField] string m_Popup6BlockIntroKey = "popup6_block_intro";
        [SerializeField] string m_Popup7BreakKey = "popup7_break";
        [SerializeField] string m_Popup8NextBlockKey = "popup8_next_block";
        [SerializeField] string m_Popup9OutroKey = "popup9_outro";

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

        public int ClearHitTrialsPerBlock => m_ClearHitTrialsPerBlock;
        public int NearHitTrialsPerBlock => m_NearHitTrialsPerBlock;
        public int NearMissTrialsPerBlock => m_NearMissTrialsPerBlock;
        public int ClearMissTrialsPerBlock => m_ClearMissTrialsPerBlock;

        public float ClearHitMinOffsetCm => m_ClearHitMinOffsetCm;
        public float ClearHitMaxOffsetCm => m_ClearHitMaxOffsetCm;

        public float NearHitMinOffsetCm => m_NearHitMinOffsetCm;
        public float NearHitMaxOffsetCm => m_NearHitMaxOffsetCm;

        public float NearMissMinOffsetCm => m_NearMissMinOffsetCm;
        public float NearMissMaxOffsetCm => m_NearMissMaxOffsetCm;

        public float ClearMissMinOffsetCm => m_ClearMissMinOffsetCm;
        public float ClearMissMaxOffsetCm => m_ClearMissMaxOffsetCm;

        public bool ScaleOffsetBandsByShoulderWidth => m_ScaleOffsetBandsByShoulderWidth;

        public int TrialsPerBlock =>
            m_ClearHitTrialsPerBlock
            + m_NearHitTrialsPerBlock
            + m_NearMissTrialsPerBlock
            + m_ClearMissTrialsPerBlock;


        public float IntroDuration => m_IntroDuration;
        public float RestDuration => m_RestDuration;
        public float OutroDuration => m_OutroDuration;

        public float SpawnDistance => m_SpawnDistance;
        public float BallDiameter => m_BallDiameter;

        public float FastSpeed => m_FastSpeed;
        public float SlowSpeed => m_SlowSpeed;
        public float ItiMinSeconds => m_ItiMinSeconds;
        public float ItiMaxSeconds => m_ItiMaxSeconds;
        public SpeedGroupPattern[] SpeedGroupPatterns => m_SpeedGroupPatterns;

        public float BreakDurationSeconds => m_BreakDurationSeconds;
        public int PracticeTrialCount => m_PracticeTrialCount;
        public float PracticeFeedbackSeconds => m_PracticeFeedbackSeconds;

        public int EasyPracticeClearHits => m_EasyPracticeClearHits;
        public int EasyPracticeClearMisses => m_EasyPracticeClearMisses;
        public int EasyPracticeNearHits => m_EasyPracticeNearHits;
        public int EasyPracticeNearMisses => m_EasyPracticeNearMisses;
        public int EasyPracticeErrorThreshold => m_EasyPracticeErrorThreshold;

        public int HardPracticeClearHits => m_HardPracticeClearHits;
        public int HardPracticeClearMisses => m_HardPracticeClearMisses;
        public int HardPracticeNearHits => m_HardPracticeNearHits;
        public int HardPracticeNearMisses => m_HardPracticeNearMisses;
        public int HardPracticeErrorThreshold => m_HardPracticeErrorThreshold;

        // Aliases kept so the controller can use either "Hard" or "Difficult" naming.
        public int DifficultPracticeClearHits => m_HardPracticeClearHits;
        public int DifficultPracticeClearMisses => m_HardPracticeClearMisses;
        public int DifficultPracticeNearHits => m_HardPracticeNearHits;
        public int DifficultPracticeNearMisses => m_HardPracticeNearMisses;
        public int DifficultPracticeErrorThreshold => m_HardPracticeErrorThreshold;

        public string Popup1IntroKey => m_Popup1IntroKey;
        public string Popup2LeftKey => m_Popup2LeftKey;
        public string Popup3RightKey => m_Popup3RightKey;
        public string Popup4PracticeKey => m_Popup4PracticeKey;
        public string Popup5ReadyKey => m_Popup5ReadyKey;
        public string Popup6BlockIntroKey => m_Popup6BlockIntroKey;
        public string Popup7BreakKey => m_Popup7BreakKey;
        public string Popup8NextBlockKey => m_Popup8NextBlockKey;
        public string Popup9OutroKey => m_Popup9OutroKey;

        public float ReferenceShoulderWidthCm => m_ReferenceShoulderWidthCm;

        public TrialDefinition[] GenerateBlock(int blockIndex)
        {
            return TrialGenerator.GenerateBlock(blockIndex, this, 0f);
        }

        /// <summary>
        /// Builds a per-block trial list scaled to the participant's shoulder
        /// width. Pass 0 (or anything ≤ 0) to skip scaling and use the
        /// reference geometry. Called from HitOrMissAppController with the
        /// value from SessionMetadata.shoulderWidthCm.
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

        public TrialDefinition[] GenerateEasyPracticeTrials(float participantShoulderWidthCm)
        {
            return TrialGenerator.GeneratePracticeTrialsWithComposition(
                this,
                participantShoulderWidthCm,
                m_EasyPracticeClearHits,
                m_EasyPracticeClearMisses,
                m_EasyPracticeNearHits,
                m_EasyPracticeNearMisses,
                "EASY_PRACTICE");
        }

        public TrialDefinition[] GenerateHardPracticeTrials(float participantShoulderWidthCm)
        {
            return TrialGenerator.GeneratePracticeTrialsWithComposition(
                this,
                participantShoulderWidthCm,
                m_HardPracticeClearHits,
                m_HardPracticeClearMisses,
                m_HardPracticeNearHits,
                m_HardPracticeNearMisses,
                "HARD_PRACTICE");
        }

        public TrialDefinition[] GenerateDifficultPracticeTrials(float participantShoulderWidthCm)
        {
            return GenerateHardPracticeTrials(participantShoulderWidthCm);
        }

        /// <summary>
        /// Returns a runtime-only clone of this asset. Modifications to the
        /// clone do not touch the on-disk source asset.
        /// </summary>
        public TrajectoryTaskAsset CreateSessionClone()
        {
            var clone = Instantiate(this);
            clone.name = name + " (Session Clone)";
            return clone;
        }

        public float OffsetScaleForParticipant(float participantShoulderWidthCm)
        {
            if (!m_ScaleOffsetBandsByShoulderWidth)
                return 1f;

            if (participantShoulderWidthCm <= 0f || m_ReferenceShoulderWidthCm <= 0f)
                return 1f;

            return participantShoulderWidthCm / m_ReferenceShoulderWidthCm;
        }

        /// <summary>
        /// Mutates this asset, intended to be called only on a session clone,
        /// so values from the clinician form's task2_parameters drive the run.
        /// </summary>
        public void ApplyTask2SessionOverrides(SessionMetadata md)
        {
            if (md.task2NumberOfBlocks > 0)
                m_BlockCount = md.task2NumberOfBlocks;

            // Legacy fallback:
            // If the session form only gives total trials per block,
            // split them equally across the four categories.
            // Prefer setting category-specific values directly in the asset.
            if (md.task2TrialsPerBlock > 0)
            {
                int total = md.task2TrialsPerBlock;
                int perCat = total / 4;
                int remainder = total % 4;

                m_ClearHitTrialsPerBlock = perCat;
                m_NearHitTrialsPerBlock = perCat;
                m_NearMissTrialsPerBlock = perCat;
                m_ClearMissTrialsPerBlock = perCat;

                // Distribute leftover trials so total stays exact.
                if (remainder > 0) m_NearHitTrialsPerBlock++;
                if (remainder > 1) m_NearMissTrialsPerBlock++;
                if (remainder > 2) m_ClearHitTrialsPerBlock++;
            }
            if (md.task2BreakDurationSeconds > 0f)
            {
                m_BreakDurationSeconds = md.task2BreakDurationSeconds;
                m_RestDuration = md.task2BreakDurationSeconds;
            }

            Debug.Log(
            $"[TrajectoryTaskAsset] Runtime config: " +
            $"blocks={m_BlockCount}, " +
            $"clearHit={m_ClearHitTrialsPerBlock}, " +
            $"nearHit={m_NearHitTrialsPerBlock}, " +
            $"nearMiss={m_NearMissTrialsPerBlock}, " +
            $"clearMiss={m_ClearMissTrialsPerBlock}, " +
            $"TrialsPerBlock={TrialsPerBlock}"
        );
        }

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;
            m_ClearHitTrialsPerBlock = Mathf.Max(0, m_ClearHitTrialsPerBlock);
            m_NearHitTrialsPerBlock = Mathf.Max(0, m_NearHitTrialsPerBlock);
            m_NearMissTrialsPerBlock = Mathf.Max(0, m_NearMissTrialsPerBlock);
            m_ClearMissTrialsPerBlock = Mathf.Max(0, m_ClearMissTrialsPerBlock);

            if (TrialsPerBlock < 1)
                m_ClearHitTrialsPerBlock = 1;

            // Keep offset bands ordered.
            if (m_ClearHitMinOffsetCm > m_ClearHitMaxOffsetCm)
            {
                float temp = m_ClearHitMinOffsetCm;
                m_ClearHitMinOffsetCm = m_ClearHitMaxOffsetCm;
                m_ClearHitMaxOffsetCm = temp;
            }

            if (m_NearHitMinOffsetCm > m_NearHitMaxOffsetCm)
            {
                float temp = m_NearHitMinOffsetCm;
                m_NearHitMinOffsetCm = m_NearHitMaxOffsetCm;
                m_NearHitMaxOffsetCm = temp;
            }

            if (m_NearMissMinOffsetCm > m_NearMissMaxOffsetCm)
            {
                float temp = m_NearMissMinOffsetCm;
                m_NearMissMinOffsetCm = m_NearMissMaxOffsetCm;
                m_NearMissMaxOffsetCm = temp;
            }

            if (m_ClearMissMinOffsetCm > m_ClearMissMaxOffsetCm)
            {
                float temp = m_ClearMissMinOffsetCm;
                m_ClearMissMinOffsetCm = m_ClearMissMaxOffsetCm;
                m_ClearMissMaxOffsetCm = temp;
            }

            // Force your intended category geometry.
            m_ClearHitMinOffsetCm = Mathf.Min(m_ClearHitMinOffsetCm, 0f);
            m_ClearHitMaxOffsetCm = Mathf.Min(m_ClearHitMaxOffsetCm, 0f);

            m_NearHitMinOffsetCm = Mathf.Max(1f, m_NearHitMinOffsetCm);
            m_NearHitMaxOffsetCm = Mathf.Max(m_NearHitMinOffsetCm, m_NearHitMaxOffsetCm);

            m_NearMissMinOffsetCm = Mathf.Max(15f, m_NearMissMinOffsetCm);
            m_NearMissMaxOffsetCm = Mathf.Max(m_NearMissMinOffsetCm, m_NearMissMaxOffsetCm);

            m_ClearMissMinOffsetCm = Mathf.Max(30f, m_ClearMissMinOffsetCm);
            m_ClearMissMaxOffsetCm = Mathf.Max(m_ClearMissMinOffsetCm, m_ClearMissMaxOffsetCm);
            
            if (m_SpawnDistance <= 0f) m_SpawnDistance = 1f;
            if (m_FastSpeed <= 0f) m_FastSpeed = 0.5f;
            if (m_SlowSpeed <= 0f) m_SlowSpeed = 0.25f;
            if (m_FastSpeed <= m_SlowSpeed) m_FastSpeed = m_SlowSpeed + 0.1f;
            if (m_BallDiameter <= 0f) m_BallDiameter = 0.1f;
            if (m_ItiMinSeconds < 0f) m_ItiMinSeconds = 0f;
            if (m_ItiMaxSeconds < m_ItiMinSeconds) m_ItiMaxSeconds = m_ItiMinSeconds;
            if (m_PracticeTrialCount < 1) m_PracticeTrialCount = 1;

            m_EasyPracticeClearHits = Mathf.Max(0, m_EasyPracticeClearHits);
            m_EasyPracticeClearMisses = Mathf.Max(0, m_EasyPracticeClearMisses);
            m_EasyPracticeNearHits = Mathf.Max(0, m_EasyPracticeNearHits);
            m_EasyPracticeNearMisses = Mathf.Max(0, m_EasyPracticeNearMisses);
            m_EasyPracticeErrorThreshold = Mathf.Max(1, m_EasyPracticeErrorThreshold);

            m_HardPracticeClearHits = Mathf.Max(0, m_HardPracticeClearHits);
            m_HardPracticeClearMisses = Mathf.Max(0, m_HardPracticeClearMisses);
            m_HardPracticeNearHits = Mathf.Max(0, m_HardPracticeNearHits);
            m_HardPracticeNearMisses = Mathf.Max(0, m_HardPracticeNearMisses);
            m_HardPracticeErrorThreshold = Mathf.Max(1, m_HardPracticeErrorThreshold);

            if (m_EasyPracticeClearHits + m_EasyPracticeClearMisses + m_EasyPracticeNearHits + m_EasyPracticeNearMisses < 1)
                m_EasyPracticeClearHits = 1;

            if (m_HardPracticeClearHits + m_HardPracticeClearMisses + m_HardPracticeNearHits + m_HardPracticeNearMisses < 1)
                m_HardPracticeNearHits = 1;
        }
    }
}
