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
        [Tooltip("Duration of the intro/instruction phase in seconds")]
        [SerializeField] float m_IntroDuration = 20f;

        [Tooltip("Duration of the rest period between blocks in seconds")]
        [SerializeField] float m_RestDuration = 30f;

        [Tooltip("Duration of the outro phase in seconds")]
        [SerializeField] float m_OutroDuration = 10f;

        [Header("Trajectory Defaults")]
        [SerializeField] float m_SpawnDistance = 5f;
        [SerializeField] float m_VanishDistance = 1f;
        [SerializeField] float m_Speed = 2.5f;
        [SerializeField] float m_BallDiameter = 0.175f;

        public string TaskName => m_TaskName;
        public int BlockCount => m_BlockCount;
        public int TrialsPerCategory => m_TrialsPerCategory;
        public float IntroDuration => m_IntroDuration;
        public float RestDuration => m_RestDuration;
        public float OutroDuration => m_OutroDuration;
        public float SpawnDistance => m_SpawnDistance;
        public float VanishDistance => m_VanishDistance;
        public float Speed => m_Speed;
        public float BallDiameter => m_BallDiameter;

        public int TrialsPerBlock => m_TrialsPerCategory * 4; // 4 categories

        /// <summary>
        /// Generate trials for a block at runtime using TrialGenerator.
        /// </summary>
        public TrialDefinition[] GenerateBlock(int blockIndex)
        {
            return TrialGenerator.GenerateBlock(blockIndex, m_SpawnDistance, m_VanishDistance, m_Speed, m_BallDiameter);
        }

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;
            if (m_TrialsPerCategory < 1) m_TrialsPerCategory = 1;
            if (m_SpawnDistance <= m_VanishDistance) m_SpawnDistance = m_VanishDistance + 1f;
            if (m_Speed <= 0f) m_Speed = 0.5f;
            if (m_BallDiameter <= 0f) m_BallDiameter = 0.1f;
        }
    }
}
