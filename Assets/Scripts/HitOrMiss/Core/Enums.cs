namespace HitOrMiss
{
    public enum TrialCategory
    {
        Hit,       // Ends on body
        NearHit,   // 0-10 cm from body edge
        NearMiss,  // 10-25 cm from body
        Miss       // 30-45 cm from body
    }

    public enum SemanticCommand
    {
        None,
        Hit,
        Miss
    }

    public enum SupportedLanguage
    {
        English,
        French
    }

    public enum TaskPhase
    {
        Idle,
        Intro,
        Block,
        Rest,
        Outro
    }

    public enum TrialResult
    {
        Correct,
        Incorrect,
        TooEarly,
        TooLate,
        NoResponse
    }
}
