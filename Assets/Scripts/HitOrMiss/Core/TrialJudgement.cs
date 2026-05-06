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
    }
}
