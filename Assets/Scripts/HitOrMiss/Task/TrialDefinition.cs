using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Defines a single trial. The ball spawns at a single point in front of the
    /// player (origin + forward × <see cref="spawnDistance"/>) and travels toward
    /// the player. The lateral curvature and final lateral offset together determine
    /// the category:
    ///   • Hit       — straight line ending at the player (no curve, no offset)
    ///   • NearHit   — slight outward curve, lands very close to the player
    ///   • NearMiss  — moderate outward curve, lands within ~10–25 cm of the player
    ///   • Miss      — pronounced outward curve, passes 30–45 cm to the side
    /// </summary>
    [System.Serializable]
    public struct TrialDefinition
    {
        [Tooltip("Unique trial identifier (e.g. B1_T01)")]
        public string trialId;

        [Tooltip("Hit, NearHit, NearMiss, or Miss")]
        public TrialCategory category;

        [Tooltip("0-based block index. Convert to 1-based block_number for CSV.")]
        public int blockIndex;

        [Tooltip("0-based trial index within the block. Convert to 1-based trial_number for CSV.")]
        public int trialIndexInBlock;

        [Tooltip("True for practice-phase trials. TaskLogger filters these out so they don't appear in trials.csv.")]
        public bool isPractice;

        [Header("Trajectory (player-anchored)")]
        [Tooltip("Distance from the player where the ball spawns, along the player's forward axis (meters)")]
        public float spawnDistance;

        [Tooltip("Signed lateral offset from the player at the trajectory's end (meters; sign chooses side)")]
        public float finalLateralOffset;

        [Tooltip("Peak lateral bow of the arc at the midpoint (meters; always curves outward, away from the player)")]
        public float curveMagnitude;

        [Tooltip("Travel speed (m/s). Set per-trial by the speed-grouping pattern.")]
        public float speed;

        [Tooltip("Speed of the previous trial in this block (m/s). 0 for the first trial of a block.")]
        public float prevSpeed;

        [Tooltip("Ball diameter in meters")]
        public float ballDiameter;

        [Tooltip("The correct response for this trial")]
        public SemanticCommand expectedResponse;

        [Header("Run / speed-grouping bookkeeping")]
        [Tooltip("0-based run index. A run is a maximal contiguous chunk of trials sharing the same speed.")]
        public int runId;

        [Tooltip("1-based position within the run (1..runLength).")]
        public int trialInRun;

        [Tooltip("Total length of the run this trial belongs to.")]
        public int runLength;

        [Tooltip("Number of trials since the most recent speed switch. 0 on the trial *of* the switch.")]
        public int trialsSinceLastSwitch;

        [Tooltip("True iff this trial is the first one after a speed change.")]
        public bool isSwitchTrial;

        [Header("Trajectory descriptor (for log)")]
        [Tooltip("Stable identifier for the trajectory shape (category + side + bin). Used for analysis grouping.")]
        public string trajectoryId;

        [Tooltip("Approach angle in degrees (0 = straight at the participant; positive = right side).")]
        public float trajectoryAngleDeg;

        [Tooltip("Spawn position in world space, captured by TrajectoryTaskManager when the trial spawns.")]
        public Vector3 spawnWorldPosition;

        [Tooltip("End position in world space, captured by TrajectoryTaskManager when the trial spawns.")]
        public Vector3 endWorldPosition;

        /// <summary>Travel duration in seconds (straight-line approximation).</summary>
        public float Duration => spawnDistance / Mathf.Max(speed, 0.01f);

        /// <summary>True iff the trial's expected response is "hit" (will-hit ground truth).</summary>
        public bool WillHit => expectedResponse == SemanticCommand.Hit;

        public static TrialDefinition CreateDefault()
        {
            return new TrialDefinition
            {
                spawnDistance = 5f,
                finalLateralOffset = 0f,
                curveMagnitude = 0f,
                speed = 2.5f,
                ballDiameter = 0.175f,
                expectedResponse = SemanticCommand.Hit,
            };
        }
    }

    /// <summary>
    /// One speed-grouping pattern. Within each group, <paramref name="fastCount"/>
    /// trials are assigned the fast speed and <paramref name="slowCount"/> are slow.
    /// <see cref="fastFirst"/> chooses which run comes first inside the group.
    /// </summary>
    [System.Serializable]
    public struct SpeedGroupPattern
    {
        [Min(0)] public int fastCount;
        [Min(0)] public int slowCount;
        [Tooltip("If true, the group lays out fast trials first then slow. Otherwise slow first.")]
        public bool fastFirst;

        public int GroupSize => Mathf.Max(1, fastCount + slowCount);

        public bool IsFastAtIndex(int withinGroup)
        {
            int size = GroupSize;
            int wg = ((withinGroup % size) + size) % size;
            return fastFirst ? wg < fastCount : wg >= slowCount;
        }
    }
}
