namespace HitOrMiss
{
    [System.Serializable]
    public struct TrialJudgement
    {
        public string trialId;
        public int blockIndex;
        public TrialCategory category;
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
