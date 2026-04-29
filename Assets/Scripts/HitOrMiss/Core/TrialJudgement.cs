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
        public float lateralOffsetMeters;  // final lateral offset of the arc end
        public float speedMps;             // travel speed assigned by the speed-grouping pattern
        public string failureReason;
    }
}
