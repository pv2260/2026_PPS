namespace HitOrMiss
{
    /// <summary>
    /// Generates the numeric BCD trigger codes specified by the lab protocol.
    /// Runs alongside (does not replace) the existing string-based EEG
    /// markers — string markers stay in the Console / log files for human
    /// readability; the numeric codes here are what gets written into the
    /// CSV trigger columns and forwarded to the parallel-port / LSL stream.
    ///
    /// === Task 1 (PPS / vibrotactile) ===
    ///   B = trial type   1=visual_only, 2=vibrotactile_only, 3=visual+vibrotactile
    ///   C = width        1=narrow,      2=wide
    ///   D = speed        1=slow,        2=fast
    ///   Trial trigger    BCD as a 3-digit integer (e.g. 311 = visual+vibrotactile, narrow, slow)
    ///
    /// === Task 2 (Hit-or-Miss) ===
    ///   B = trajectory   1=clear_hit, 2=near_hit, 3=near_miss, 4=clear_miss
    ///   C = speed        1=slow,      2=fast
    ///   D = transition   1=repetition, 2=transition       (0 if start trial)
    ///   Trial trigger    BCD as a 3-digit integer (e.g. 422 = clear_miss, fast, transition)
    ///
    /// === Response triggers (both tasks) ===
    /// Two-digit codes by convention:
    ///   90 = LEFT  / HIT
    ///   91 = RIGHT / MISS
    ///   99 = no response / timeout
    ///   95 = test trigger (sent during TriggerCheckPanel verification)
    /// </summary>
    public static class TriggerEncoder
    {
        // ---- Response codes (shared) ----
        public const int ResponseHit       = 90;
        public const int ResponseMiss      = 91;
        public const int ResponseNoResponse = 99;
        public const int TestTrigger       = 95;

        // ====================================================================
        // Task 1 — PPS / vibrotactile
        // ====================================================================

        public enum Task1TrialType
        {
            VisualOnly         = 1,
            VibrotactileOnly   = 2,
            VisualAndVibrotactile = 3,
        }

        public enum Task1Width
        {
            Narrow = 1,
            Wide   = 2,
        }

        public enum Task1Speed
        {
            Slow = 1,
            Fast = 2,
        }

        /// <summary>
        /// Builds a Task 1 trial trigger code: BCD as a 3-digit integer.
        /// Example: <c>EncodeTask1(VisualAndVibrotactile, Narrow, Slow) = 311</c>.
        /// </summary>
        public static int EncodeTask1(Task1TrialType type, Task1Width width, Task1Speed speed)
            => (int)type * 100 + (int)width * 10 + (int)speed;

        // ====================================================================
        // Task 2 — Hit-or-Miss
        // ====================================================================

        /// <summary>
        /// Builds a Task 2 trial trigger code from the spec-aligned enums.
        /// Example: <c>EncodeTask2(ClearMiss, Fast, Transition) = 422</c>.
        /// First trial of a block has <c>TransitionStatus.Start</c>; D = 0
        /// in that case so analysts can filter start trials trivially.
        /// </summary>
        public static int EncodeTask2(TrialCategory trajectory, SpeedLevel speed, TransitionStatus transition)
        {
            int b = trajectory.ToTriggerDigit();      // 1..4
            int c = speed.ToTriggerDigit();           // 1=slow, 2=fast
            int d = transition.ToTriggerDigit();      // 0=start, 1=repetition, 2=transition
            return b * 100 + c * 10 + d;
        }

        // ====================================================================
        // Response codes
        // ====================================================================

        public static int EncodeResponse(SemanticCommand received) => received switch
        {
            SemanticCommand.Hit  => ResponseHit,
            SemanticCommand.Miss => ResponseMiss,
            _                    => ResponseNoResponse,
        };
    }
}
