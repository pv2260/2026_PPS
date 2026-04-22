namespace HitOrMiss
{
    [System.Serializable]
    public struct TrialJudgement
    {
        public string trialId;
        public int blockIndex;
        public TrialCategory category;
    // Added by pam: speed condition, and speed meters per second: 22.04.26
        public SpeedCondition speedCondition;
        public float speedMetersPerSecond;
        public SemanticCommand expected;
        public SemanticCommand received;
        public TrialResult result;
        public bool isCorrect;
        public double stimulusOnsetTime;
        public double responseTime;
        public double reactionTimeMs;
        public float lateralOffsetMeters;  // actual final lateral offset
        public float approachAngleDeg;     // initial approach angle
        public string failureReason;
    }
}
