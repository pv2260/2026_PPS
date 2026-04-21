namespace HitOrMiss.Pps
{
    public enum PpsModality
    {
        VisualOnly,
        TactileOnly,
        Both
    }

    public enum PpsSpeed
    {
        Fast,
        Slow
    }

    public enum PpsWidth
    {
        Wide,
        Narrow
    }

    public enum DistanceStage
    {
        None,
        D4,
        D3,
        D2,
        D1
    }

    public enum PpsPhase
    {
        Idle,
        SubjectId,
        Instructions,
        PracticeIntro,
        Practice,
        BlockIntro,
        Block,
        Rest,
        Outro
    }

    public enum TrialOrder
    {
        Sequential,
        Shuffled
        // CounterbalancedLatinSquare reserved — see PpsTrialGenerator.
    }
}
