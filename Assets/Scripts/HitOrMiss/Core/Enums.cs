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
        Intro,        // Popup 1 (intro text + START PRACTICE)
        Practice,     // Popups 2 + 3 + 4 + practice trials
        Ready,        // Popup 5 (no-feedback warning + START TASK)
        BlockIntro,   // Popup 6 (per-block start)
        Block,        // Active trial spawning
        Rest,         // Popup 7 (break) + countdown
        BlockReady,   // Popup 8 (ready to continue)
        Outro,        // Popup 9 (thanks)
    }

    public enum TrialResult
    {
        Correct,
        Incorrect,
        TooEarly,
        TooLate,
        NoResponse
    }

    /// <summary>
    /// Session type for the participant population/condition. Logged in
    /// metadata.json and as a per-trial column.
    /// </summary>
    public enum SessionType
    {
        MedicationHigh,
        MedicationLow,
        HealthySession1,
        HealthySession2,
    }

    /// <summary>
    /// Deep-brain-stimulation status at session time.
    /// </summary>
    public enum DbsStatus
    {
        Na,   // not applicable (e.g. healthy participant)
        On,
        Off,
    }

    public enum DominantHand
    {
        Unspecified,
        Left,
        Right,
        Ambidextrous,
    }

    /// <summary>
    /// Direction of the speed change between this trial and the previous trial.
    /// </summary>
    public enum SpeedChangeDirection
    {
        None,     // first trial of the block, or speed unchanged from previous
        Increase,
        Decrease,
    }
}
