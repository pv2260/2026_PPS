using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Defines a single trial: a curved trajectory from spawn to vanish point.
    /// The ball spawns at spawnDistance, travels at constant speed, and vanishes at vanishDistance.
    /// The trajectory curves laterally so the final lateral offset determines the category.
    /// </summary>
    [System.Serializable]
    public struct TrialDefinition
    {
        [Tooltip("Unique trial identifier (e.g. B1_T01)")]
        public string trialId;

        [Tooltip("Hit, NearHit, NearMiss, or Miss")]
        public TrialCategory category;

        [Header("Trajectory Parameters")]
        [Tooltip("Initial horizontal approach angle in degrees (0 = straight ahead, positive = from right)")]
        public float approachAngleDeg;

        [Tooltip("Direction of the curve: +1 = curves right, -1 = curves left")]
        public float curveDirection;

        [Tooltip("Magnitude of lateral curvature (meters of arc displacement at midpoint)")]
        public float curvatureMagnitude;

        [Tooltip("Final lateral offset from body center (meters). Sign indicates side.")]
        public float finalLateralOffset;

        //Added by pam: speed condition 22.04.26
        [Header("Distances & Speed")]
        [Tooltip("Distance from player where ball spawns (meters)")]
        public float spawnDistance;

        [Tooltip("Distance from player where ball vanishes (meters)")]
        public float vanishDistance;

        [Tooltip("Speed condition for this trial")]
        public SpeedCondition speedCondition;

        [Tooltip("Travel speed for this specific trial (m/s)")]
        public float speed;

        [Tooltip("Ball diameter in meters")]
        public float ballDiameter;
        [Tooltip("The correct response for this trial")]
        public SemanticCommand expectedResponse;

        /// <summary>Travel duration in seconds.</summary>
        public float Duration => (spawnDistance - vanishDistance) / Mathf.Max(speed, 0.01f);

        /// <summary>Time window from spawn until vanish (full duration) for response.</summary>
        public float ResponseWindowDuration => Duration;

        public static TrialDefinition CreateDefault()
        {
            return new TrialDefinition
            {
                spawnDistance = 7f,
                vanishDistance = 1f,
                speedCondition = SpeedCondition.Slow,
                speed = 2.5f,
                ballDiameter = 0.175f,     // 17.5 cm
                curvatureMagnitude = 0.4f,
                curveDirection = 1f,
                approachAngleDeg = 0f,
                finalLateralOffset = 0f,
                expectedResponse = SemanticCommand.Hit
            };
        }
    }
}
