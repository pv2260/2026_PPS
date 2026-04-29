using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Generates the per-block trial list. 4 categories × <see cref="TrajectoryTaskAsset.TrialsPerCategory"/>.
    /// Each ball spawns at the same point in front of the player and travels toward the player.
    /// Lateral curve magnitude scales with category — Hits are straight, Misses curve hard outward.
    /// </summary>
    public static class TrialGenerator
    {
        // Final lateral offset ranges per category (meters from player center where the ball ends)
        static readonly Vector2 HitRange      = new(0f,    0f);    // dead-center, on body
        static readonly Vector2 NearHitRange  = new(0f,    0.10f); // 0–10 cm
        static readonly Vector2 NearMissRange = new(0.10f, 0.25f); // 10–25 cm
        static readonly Vector2 MissRange     = new(0.30f, 0.45f); // 30–45 cm

        public static TrialDefinition[] GenerateBlock(int blockIndex, TrajectoryTaskAsset asset)
        {
            int perCat = asset.TrialsPerCategory;
            float spawnDistance = asset.SpawnDistance;
            float ballDiameter = asset.BallDiameter;

            var trials = new List<TrialDefinition>(perCat * 4);
            trials.AddRange(GenerateCategory(TrialCategory.Hit,      HitRange,      SemanticCommand.Hit,  perCat, spawnDistance, ballDiameter, asset));
            trials.AddRange(GenerateCategory(TrialCategory.NearHit,  NearHitRange,  SemanticCommand.Hit,  perCat, spawnDistance, ballDiameter, asset));
            trials.AddRange(GenerateCategory(TrialCategory.NearMiss, NearMissRange, SemanticCommand.Miss, perCat, spawnDistance, ballDiameter, asset));
            trials.AddRange(GenerateCategory(TrialCategory.Miss,     MissRange,     SemanticCommand.Miss, perCat, spawnDistance, ballDiameter, asset));

            ShuffleNoConsecutive(trials);
            AssignIds(trials, blockIndex);
            AssignSpeedsByGroupPattern(trials, asset.SpeedGroupPatterns, asset.FastSpeed, asset.SlowSpeed);

            return trials.ToArray();
        }

        static List<TrialDefinition> GenerateCategory(TrialCategory category,
            Vector2 offsetRange, SemanticCommand expected, int count,
            float spawnDistance, float ballDiameter, TrajectoryTaskAsset asset)
        {
            var result = new List<TrialDefinition>(count);
            float baseCurve = asset.CurveMagnitudeFor(category);

            for (int i = 0; i < count; i++)
            {
                float magnitude = Random.Range(offsetRange.x, Mathf.Max(offsetRange.x + 0.001f, offsetRange.y));
                float side = Random.value > 0.5f ? 1f : -1f;
                float lateral = magnitude * side;

                // Slight per-trial variation around the per-category curve magnitude.
                float curveJitter = baseCurve > 0f ? Random.Range(-0.05f, 0.05f) : 0f;
                float curve = Mathf.Max(0f, baseCurve + curveJitter);

                result.Add(new TrialDefinition
                {
                    category = category,
                    spawnDistance = spawnDistance,
                    finalLateralOffset = lateral,
                    curveMagnitude = curve,
                    speed = 0f, // assigned later by group pattern
                    ballDiameter = ballDiameter,
                    expectedResponse = expected,
                });
            }
            return result;
        }

        /// <summary>
        /// Shuffle so no two consecutive trials share a category. Greedy fix-up after Fisher–Yates.
        /// </summary>
        static void ShuffleNoConsecutive(List<TrialDefinition> trials)
        {
            for (int i = trials.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (trials[i], trials[j]) = (trials[j], trials[i]);
            }

            const int maxPasses = 100;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool conflict = false;
                for (int i = 1; i < trials.Count; i++)
                {
                    if (trials[i].category != trials[i - 1].category) continue;
                    conflict = true;

                    bool swapped = false;
                    for (int k = i + 1; k < trials.Count; k++)
                    {
                        bool safeHere = trials[k].category != trials[i - 1].category;
                        bool safeThere = (k + 1 >= trials.Count || trials[i].category != trials[k + 1].category)
                                         && (trials[i].category != trials[k - 1].category);
                        if (safeHere && safeThere)
                        {
                            (trials[i], trials[k]) = (trials[k], trials[i]);
                            swapped = true;
                            break;
                        }
                    }
                    if (!swapped)
                    {
                        int r = Random.Range(Mathf.Min(i + 2, trials.Count - 1), trials.Count);
                        (trials[i], trials[r]) = (trials[r], trials[i]);
                    }
                }
                if (!conflict) break;
            }
        }

        static void AssignIds(List<TrialDefinition> trials, int blockIndex)
        {
            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                t.trialId = $"B{blockIndex + 1}_T{i + 1:D2}";
                trials[i] = t;
            }
        }

        /// <summary>
        /// Walk trials in order; consume <see cref="SpeedGroupPattern"/> entries one group at a time.
        /// If the pattern list is shorter than the number of groups in the block, it cycles.
        /// </summary>
        static void AssignSpeedsByGroupPattern(List<TrialDefinition> trials,
            SpeedGroupPattern[] patterns, float fastSpeed, float slowSpeed)
        {
            if (patterns == null || patterns.Length == 0)
            {
                for (int i = 0; i < trials.Count; i++)
                {
                    var t = trials[i];
                    t.speed = fastSpeed;
                    trials[i] = t;
                }
                return;
            }

            int trialIdx = 0;
            int groupIdx = 0;
            while (trialIdx < trials.Count)
            {
                var pattern = patterns[groupIdx % patterns.Length];
                int size = pattern.GroupSize;
                for (int wg = 0; wg < size && trialIdx < trials.Count; wg++)
                {
                    var t = trials[trialIdx];
                    t.speed = pattern.IsFastAtIndex(wg) ? fastSpeed : slowSpeed;
                    trials[trialIdx] = t;
                    trialIdx++;
                }
                groupIdx++;
            }
        }
    }
}
