using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Scored result for a single trial. Contains the trial definition (which
    /// already carries category/run/trajectory metadata), plus per-trial
    /// timing and the participant's response. Serialized to <c>trials.csv</c>
    /// by <see cref="Logging.TaskLogger"/> using the column schema defined
    /// in the AR-Task PDF (29.04.2026 spec).
    /// </summary>
    [System.Serializable]
    public struct TrialJudgement
    {
        public string trialId;
        public int blockIndex;            // 0-based; CSV emits block_number = blockIndex + 1
        public TrialCategory category;
        public SemanticCommand expected;
        public SemanticCommand received;
        public TrialResult result;
        public bool isCorrect;

        // ---- Run / speed-grouping snapshot (copied off TrialDefinition at score time) ----
        public int trialNumberInBlock;    // 1-based
        public int runId;
        public int trialInRun;
        public int runLength;
        public int trialsSinceLastSwitch;
        public bool isSwitchTrial;
        public float speedMps;
        public float prevSpeedMps;
        public float speedChange;         // current - previous
        public float absSpeedChange;
        public SpeedChangeDirection changeDirection;

        // ---- Spec-aligned categorical speed/transition fields ----
        // (TASK 2 — HIT OR MISS TASK LOGIC.txt). The numeric speedMps above
        // is the actual ball speed in m/s; the categorical fields below are
        // what gets logged to trials.csv.
        public SpeedLevel currentSpeedLevel;
        public SpeedLevel previousSpeedLevel;
        public bool hasPreviousSpeed;       // false = first trial of block (start)
        public TransitionStatus transitionStatus;

        // ---- Trajectory snapshot ----
        public string trajectoryId;
        public float trajectoryAngleDeg;
        public Vector3 startWorldPosition;
        public Vector3 endWorldPosition;
        public float lateralOffsetMeters; // signed final lateral offset (kept for compatibility)

        // ---- Timing (seconds since engine start; reaction time in ms) ----
        public double trialStartTime;       // when the trial entered its active window
        public double ballMotionStartTime;  // when the ball began moving
        public double stimulusOnsetTime;    // legacy alias of trialStartTime
        public double responseTime;         // when the participant responded (NaN if no response)
        public double reactionTimeMs;
        public float interTrialIntervalMs;  // ITI that preceded this trial

        public string failureReason;

        // ---- Spec triggers (numeric BCD per TriggerEncoder) ----
        public int trialTriggerCode;
        public int responseTriggerCode;
        public double triggerTimestamp;     // engine seconds when trial trigger was emitted
        public bool trialInterrupted;        // true if trial was paused/aborted

        /// <summary>
        /// Code used for the <c>button_pressed</c> CSV column: "left" / "right" / "".
        /// HIT is bound to LEFT and MISS to RIGHT per the protocol.
        /// </summary>
        public string ButtonPressedCode => received switch
        {
            SemanticCommand.Hit  => "left",
            SemanticCommand.Miss => "right",
            _                    => string.Empty,
        };

        /// <summary>
        /// Code used for the <c>participant_response</c> CSV column.
        /// </summary>
        public string ParticipantResponseCode => received switch
        {
            SemanticCommand.Hit  => "hit",
            SemanticCommand.Miss => "miss",
            _                    => string.Empty,
        };

        public string ChangeDirectionCode => changeDirection switch
        {
            SpeedChangeDirection.Increase => "increase",
            SpeedChangeDirection.Decrease => "decrease",
            _                             => "none",
        };

        /// <summary>
        /// "slow-fast", "fast-slow", "slow-slow", "fast-fast", or
        /// "start-slow"/"start-fast" for the first trial of a block.
        /// Matches the spec's <c>speed_sequence</c> CSV column.
        /// </summary>
        public string SpeedSequenceCode
        {
            get
            {
                string current = currentSpeedLevel.ToCode();
                if (!hasPreviousSpeed) return $"start-{current}";
                string prev = previousSpeedLevel.ToCode();
                return $"{prev}-{current}";
            }
        }

        /// <summary>
        /// "HIT" or "MISS" — the correct response for this trial. Matches the
        /// spec's <c>correct_response</c> CSV column.
        /// </summary>
        public string CorrectResponseCode => expected switch
        {
            SemanticCommand.Hit  => "HIT",
            SemanticCommand.Miss => "MISS",
            _                    => "",
        };

        /// <summary>"HIT", "MISS", or "none" — what the participant pressed.</summary>
        public string ParticipantResponseSpecCode => received switch
        {
            SemanticCommand.Hit  => "HIT",
            SemanticCommand.Miss => "MISS",
            _                    => "none",
        };
    }
}
