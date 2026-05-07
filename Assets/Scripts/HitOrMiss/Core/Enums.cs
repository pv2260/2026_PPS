namespace HitOrMiss
{
    /// <summary>
    /// Trajectory category for Task 2 (Hit-or-Miss). Names match the spec —
    /// see <c>TASK 2 — HIT OR MISS TASK LOGIC.txt</c>. The CSV-friendly
    /// snake_case form is produced by <see cref="TrialCategoryCodes.ToCode"/>.
    /// </summary>
    public enum TrialCategory
    {
        ClearHit,    // trajectory clearly inside shoulder boundary       → HIT
        NearHit,     // trajectory just inside shoulder boundary          → HIT
        NearMiss,    // trajectory just outside shoulder boundary         → MISS
        ClearMiss,   // trajectory clearly outside shoulder boundary      → MISS
    }

    /// <summary>
    /// Snake-case codes matching the spec's CSV column values
    /// ("clear_hit", "near_hit", "near_miss", "clear_miss") and the
    /// numeric BCD digit B used by <see cref="TriggerEncoder"/>.
    /// </summary>
    public static class TrialCategoryCodes
    {
        public static string ToCode(this TrialCategory c) => c switch
        {
            TrialCategory.ClearHit  => "clear_hit",
            TrialCategory.NearHit   => "near_hit",
            TrialCategory.NearMiss  => "near_miss",
            TrialCategory.ClearMiss => "clear_miss",
            _ => c.ToString().ToLowerInvariant(),
        };

        /// <summary>BCD digit "B" for <see cref="TriggerEncoder"/>: 1=clear_hit, 2=near_hit, 3=near_miss, 4=clear_miss.</summary>
        public static int ToTriggerDigit(this TrialCategory c) => c switch
        {
            TrialCategory.ClearHit  => 1,
            TrialCategory.NearHit   => 2,
            TrialCategory.NearMiss  => 3,
            TrialCategory.ClearMiss => 4,
            _ => 0,
        };
    }

    /// <summary>
    /// Three-level speed condition shared by Task 1 and Task 2. The setup.json
    /// "looming_speeds" / "ball_speeds" array can list any subset.
    /// </summary>
    public enum SpeedLevel
    {
        Slow,
        Medium,
        Fast,
    }

    /// <summary>
    /// Relationship between the current trial's speed and the previous trial's.
    /// "Start" is the first trial of a block (no previous). "Repetition" =
    /// same speed as previous; "Transition" = different speed.
    /// </summary>
    public enum TransitionStatus
    {
        Start,
        Repetition,
        Transition,
    }

    public static class TransitionStatusCodes
    {
        public static string ToCode(this TransitionStatus t) => t switch
        {
            TransitionStatus.Start      => "start",
            TransitionStatus.Repetition => "repetition",
            TransitionStatus.Transition => "transition",
            _ => t.ToString().ToLowerInvariant(),
        };

        /// <summary>BCD digit "D": 1=repetition, 2=transition. "Start" emits 0.</summary>
        public static int ToTriggerDigit(this TransitionStatus t) => t switch
        {
            TransitionStatus.Repetition => 1,
            TransitionStatus.Transition => 2,
            TransitionStatus.Start      => 0,
            _ => 0,
        };
    }

    public static class SpeedLevelCodes
    {
        public static string ToCode(this SpeedLevel s) => s.ToString().ToLowerInvariant();

        /// <summary>BCD digit "C": 1=slow, 2=fast. Medium emits 3 in extended schemes.</summary>
        public static int ToTriggerDigit(this SpeedLevel s) => s switch
        {
            SpeedLevel.Slow   => 1,
            SpeedLevel.Fast   => 2,
            SpeedLevel.Medium => 3,
            _ => 0,
        };
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
