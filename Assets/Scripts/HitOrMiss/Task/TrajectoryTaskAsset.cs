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

        public int TrialsPerBlock => m_TrialsPerCategory * 4; // 4 categories

        public float CurveMagnitudeFor(TrialCategory category) => category switch
        {
            TrialCategory.Hit      => m_HitCurveMagnitude,
            TrialCategory.NearHit  => m_NearHitCurveMagnitude,
            TrialCategory.NearMiss => m_NearMissCurveMagnitude,
            TrialCategory.Miss     => m_MissCurveMagnitude,
            _ => 0f,
        };

        public TrialDefinition[] GenerateBlock(int blockIndex)
        {
            return TrialGenerator.GenerateBlock(blockIndex, this);
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
