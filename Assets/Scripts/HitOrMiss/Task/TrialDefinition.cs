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

        [Header("Trajectory (player-anchored)")]
        [Tooltip("Distance from the player where the ball spawns, along the player's forward axis (meters)")]
        public float spawnDistance;

        [Tooltip("Signed lateral offset from the player at the trajectory's end (meters; sign chooses side)")]
        public float finalLateralOffset;

        [Tooltip("Peak lateral bow of the arc at the midpoint (meters; always curves outward, away from the player)")]
        public float curveMagnitude;

        [Tooltip("Travel speed (m/s). Set per-trial by the speed-grouping pattern.")]
        public float speed;

        [Tooltip("Ball diameter in meters")]
        public float ballDiameter;

        [Tooltip("The correct response for this trial")]
        public SemanticCommand expectedResponse;

        /// <summary>Travel duration in seconds (straight-line approximation).</summary>
        public float Duration => spawnDistance / Mathf.Max(speed, 0.01f);

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
