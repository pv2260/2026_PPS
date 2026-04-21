using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Generates 80 trial definitions per block with parametric variation.
    /// 20 per category: Hit, NearHit, NearMiss, Miss.
    /// Each trial has unique approach angle and curve direction with small random jitter.
    /// Trials are shuffled with a no-consecutive-same-category constraint.
    /// </summary>
    public static class TrialGenerator
    {
        // Category offset ranges (meters from body center)
        static readonly Vector2 HitRange = new(0f, 0f);            // On body (0 offset)
        static readonly Vector2 NearHitRange = new(0f, 0.10f);     // 0-10 cm
        static readonly Vector2 NearMissRange = new(0.10f, 0.25f); // 10-25 cm
        static readonly Vector2 MissRange = new(0.30f, 0.45f);     // 30-45 cm

        const int TrialsPerCategory = 20;
        const float DefaultSpawnDistance = 7f;
        const float DefaultVanishDistance = 1f;
        const float DefaultSpeed = 2.5f;
        const float DefaultBallDiameter = 0.175f;
        const float DefaultCurvatureMagnitude = 0.40f; // 30-50 cm displacement from straight line

        public static TrialDefinition[] GenerateBlock(int blockIndex, float spawnDistance = DefaultSpawnDistance,
            float vanishDistance = DefaultVanishDistance, float speed = DefaultSpeed, float ballDiameter = DefaultBallDiameter)
        {
            var trials = new List<TrialDefinition>();

            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.Hit, HitRange, SemanticCommand.Hit, spawnDistance, vanishDistance, speed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.NearHit, NearHitRange, SemanticCommand.Hit, spawnDistance, vanishDistance, speed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.NearMiss, NearMissRange, SemanticCommand.Miss, spawnDistance, vanishDistance, speed, ballDiameter));
            trials.AddRange(GenerateCategory(blockIndex, TrialCategory.Miss, MissRange, SemanticCommand.Miss, spawnDistance, vanishDistance, speed, ballDiameter));

            ShuffleNoConsecutive(trials);
            AssignTimings(trials);

            return trials.ToArray();
        }

        static List<TrialDefinition> GenerateCategory(int blockIndex, TrialCategory category,
            Vector2 offsetRange, SemanticCommand expected, float spawnDist, float vanishDist, float speed, float diameter)
        {
            var result = new List<TrialDefinition>(TrialsPerCategory);

            for (int i = 0; i < TrialsPerCategory; i++)
            {
                // Vary approach angle: spread across -30 to +30 degrees
                float approachAngle = Mathf.Lerp(-30f, 30f, (float)i / (TrialsPerCategory - 1));
                approachAngle += Random.Range(-3f, 3f); // small jitter

                // Alternate curve direction and add slight variation
                float curveDir = (i % 2 == 0) ? 1f : -1f;

                // Final lateral offset: random within category range, with random side
                float offsetMagnitude = Random.Range(offsetRange.x, Mathf.Max(offsetRange.x + 0.001f, offsetRange.y));
                float side = (Random.value > 0.5f) ? 1f : -1f;
                float finalOffset = offsetMagnitude * side;

                // Curvature magnitude with small variation (15-25 cm as per spec)
                float curvature = DefaultCurvatureMagnitude + Random.Range(-0.10f, 0.10f);

                result.Add(new TrialDefinition
                {
                    trialId = $"B{blockIndex + 1}_T{result.Count + 1 + (int)category * TrialsPerCategory:D2}",
                    category = category,
                    approachAngleDeg = approachAngle,
                    curveDirection = curveDir,
                    curvatureMagnitude = curvature,
                    finalLateralOffset = finalOffset,
                    spawnDistance = spawnDist,
                    vanishDistance = vanishDist,
                    speed = speed,
                    ballDiameter = diameter,
                    expectedResponse = expected
                });
            }

            return result;
        }

        /// <summary>
        /// Shuffle trials so no two consecutive trials share the same category.
        /// Uses a greedy swap approach.
        /// </summary>
        static void ShuffleNoConsecutive(List<TrialDefinition> trials)
        {
            // First: Fisher-Yates shuffle
            for (int i = trials.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (trials[i], trials[j]) = (trials[j], trials[i]);
            }

            // Then: fix consecutive same-category pairs
            int maxPasses = 100;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool foundConflict = false;
                for (int i = 1; i < trials.Count; i++)
                {
                    if (trials[i].category == trials[i - 1].category)
                    {
                        foundConflict = true;
                        // Find a swap candidate later in the list
                        bool swapped = false;
                        for (int k = i + 1; k < trials.Count; k++)
                        {
                            bool safeHere = trials[k].category != trials[i - 1].category;
                            bool safeThere = (k + 1 >= trials.Count || trials[i].category != trials[k + 1].category)
                                          && (k - 1 < 0 || trials[i].category != trials[k - 1].category);

                            if (safeHere && safeThere)
                            {
                                (trials[i], trials[k]) = (trials[k], trials[i]);
                                swapped = true;
                                break;
                            }
                        }
                        if (!swapped)
                        {
                            // Fallback: just swap with a random non-adjacent position
                            int r = Random.Range(Mathf.Min(i + 2, trials.Count - 1), trials.Count);
                            (trials[i], trials[r]) = (trials[r], trials[i]);
                        }
                    }
                }
                if (!foundConflict) break;
            }
        }

        /// <summary>
        /// Assign block-relative start times with ~4s inter-trial interval + small jitter.
        /// </summary>
        static void AssignTimings(List<TrialDefinition> trials)
        {
            float time = 0f;
            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                t.trialId = $"{t.trialId.Split('_')[0]}_T{i + 1:D2}";
                time += (i == 0) ? 0f : 4f + Random.Range(-0.3f, 0.3f); // ~4s ITI with jitter
                trials[i] = t;
            }
        }
    }
}
