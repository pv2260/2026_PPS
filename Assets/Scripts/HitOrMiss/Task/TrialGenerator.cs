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
        // Final lateral offset ranges per category (meters from player center
        // where the ball ends). These are calibrated to the asset's reference
        // shoulder width (default 42 cm) and scaled per-participant by
        // ComputeShoulderScale at generation time.
        static readonly Vector2 HitRange      = new(0f,    0f);    // dead-center, on body
        static readonly Vector2 NearHitRange  = new(0f,    0.10f); // 0–10 cm
        static readonly Vector2 NearMissRange = new(0.10f, 0.25f); // 10–25 cm
        static readonly Vector2 MissRange     = new(0.30f, 0.45f); // 30–45 cm

        /// <summary>
        /// Returns the per-participant lateral / curve scale factor.
        /// <paramref name="shoulderWidthCm"/> ≤ 0 returns 1.0 (no scaling),
        /// useful when metadata isn't yet set.
        /// </summary>
        static float ComputeShoulderScale(TrajectoryTaskAsset asset, float shoulderWidthCm)
        {
            if (shoulderWidthCm <= 0f) return 1f;
            float reference = asset != null && asset.ReferenceShoulderWidthCm > 0f
                ? asset.ReferenceShoulderWidthCm
                : 42f;
            return shoulderWidthCm / reference;
        }

        /// <summary>
        /// Generates a small set of practice trials for the practice phase
        /// (PDF popup 4). Alternates Hit / Miss outcomes so the participant
        /// experiences both possible responses with feedback. Practice trials
        /// are flagged <see cref="TrialDefinition.isPractice"/> = true so the
        /// logger skips them.
        /// </summary>
        public static TrialDefinition[] GeneratePracticeTrials(TrajectoryTaskAsset asset, float shoulderWidthCm = 0f)
        {
            int count = Mathf.Max(1, asset.PracticeTrialCount);
            float scale = ComputeShoulderScale(asset, shoulderWidthCm);
            var trials = new List<TrialDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                bool isHit = (i % 2 == 0);
                var category = isHit ? TrialCategory.ClearHit : TrialCategory.ClearMiss;
                var range = isHit ? HitRange : MissRange;
                trials.AddRange(GenerateCategory(category, range,
                    isHit ? SemanticCommand.Hit : SemanticCommand.Miss,
                    1, asset.SpawnDistance, asset.BallDiameter, asset, scale));
            }
            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                t.trialId = $"PRACTICE_T{i + 1:D2}";
                t.blockIndex = -1;
                t.trialIndexInBlock = i;
                t.isPractice = true;
                t.speed = asset.SlowSpeed; // slower so the participant has time to reason
                trials[i] = t;
            }
            AssignTrajectoryDescriptors(trials);
            return trials.ToArray();
        }

        public static TrialDefinition[] GenerateBlock(int blockIndex, TrajectoryTaskAsset asset, float shoulderWidthCm = 0f)
        {
            int perCat = asset.TrialsPerCategory;
            float spawnDistance = asset.SpawnDistance;
            float ballDiameter = asset.BallDiameter;
            float scale = ComputeShoulderScale(asset, shoulderWidthCm);

            var trials = new List<TrialDefinition>(perCat * 4);
            trials.AddRange(GenerateCategory(TrialCategory.ClearHit,  HitRange,      SemanticCommand.Hit,  perCat, spawnDistance, ballDiameter, asset, scale));
            trials.AddRange(GenerateCategory(TrialCategory.NearHit,   NearHitRange,  SemanticCommand.Hit,  perCat, spawnDistance, ballDiameter, asset, scale));
            trials.AddRange(GenerateCategory(TrialCategory.NearMiss,  NearMissRange, SemanticCommand.Miss, perCat, spawnDistance, ballDiameter, asset, scale));
            trials.AddRange(GenerateCategory(TrialCategory.ClearMiss, MissRange,     SemanticCommand.Miss, perCat, spawnDistance, ballDiameter, asset, scale));

            ShuffleNoConsecutive(trials);
            AssignIds(trials, blockIndex);
            AssignSpeedsByGroupPattern(trials, asset.SpeedGroupPatterns, asset.FastSpeed, asset.SlowSpeed);
            AssignRunMetadata(trials);
            AssignTrajectoryDescriptors(trials);

            return trials.ToArray();
        }

        static List<TrialDefinition> GenerateCategory(TrialCategory category,
            Vector2 offsetRange, SemanticCommand expected, int count,
            float spawnDistance, float ballDiameter, TrajectoryTaskAsset asset, float shoulderScale)
        {
            var result = new List<TrialDefinition>(count);
            float baseCurve = asset.CurveMagnitudeFor(category) * shoulderScale;

            // Scale the lateral-offset band so a wider participant gets a
            // proportionally wider miss band, and a narrower participant a
            // narrower one. Hit (0..0) is a no-op under scaling.
            Vector2 scaledRange = new Vector2(offsetRange.x * shoulderScale, offsetRange.y * shoulderScale);

            for (int i = 0; i < count; i++)
            {
                float magnitude = Random.Range(scaledRange.x, Mathf.Max(scaledRange.x + 0.001f, scaledRange.y));
                float side = Random.value > 0.5f ? 1f : -1f;
                float lateral = magnitude * side;

                // Slight per-trial variation around the per-category curve
                // magnitude. Jitter scales with shoulder width so the relative
                // wobble stays consistent across participants.
                float curveJitter = baseCurve > 0f ? Random.Range(-0.05f * shoulderScale, 0.05f * shoulderScale) : 0f;
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
                t.blockIndex = blockIndex;
                t.trialIndexInBlock = i;
                trials[i] = t;
            }
        }

        /// <summary>
        /// Walks the speed-assigned trial list and labels each with its run
        /// (a maximal contiguous chunk of same-speed trials). Populates
        /// <c>runId</c>, <c>trialInRun</c>, <c>runLength</c>,
        /// <c>trialsSinceLastSwitch</c>, <c>isSwitchTrial</c>, and
        /// <c>prevSpeed</c> on each trial.
        /// </summary>
        static void AssignRunMetadata(List<TrialDefinition> trials)
        {
            if (trials.Count == 0) return;

            // First pass: identify run boundaries.
            var runStarts = new List<int>();
            var runLengths = new List<int>();
            float currentSpeed = trials[0].speed;
            int runStart = 0;
            for (int i = 1; i < trials.Count; i++)
            {
                if (!Mathf.Approximately(trials[i].speed, currentSpeed))
                {
                    runStarts.Add(runStart);
                    runLengths.Add(i - runStart);
                    runStart = i;
                    currentSpeed = trials[i].speed;
                }
            }
            runStarts.Add(runStart);
            runLengths.Add(trials.Count - runStart);

            // Second pass: write per-trial metadata. prevSpeed is the
            // immediately preceding trial's speed (per spec) — NOT the
            // previous run's speed. The first trial of the block has
            // prevSpeed = 0 so the resolver can detect "start".
            float immediatePrevSpeed = 0f;
            for (int run = 0; run < runStarts.Count; run++)
            {
                int start = runStarts[run];
                int length = runLengths[run];
                for (int k = 0; k < length; k++)
                {
                    int idx = start + k;
                    var t = trials[idx];
                    t.runId = run;
                    t.trialInRun = k + 1;
                    t.runLength = length;
                    t.prevSpeed = immediatePrevSpeed;
                    t.isSwitchTrial = (k == 0 && run > 0);
                    t.trialsSinceLastSwitch = k;
                    trials[idx] = t;
                    immediatePrevSpeed = t.speed;
                }
            }
        }

        /// <summary>
        /// Synthesizes a stable <c>trajectoryId</c> string and approach angle
        /// for each trial. The id encodes category + side + lateral-offset bin
        /// so trials sharing a shape group together in analysis. The angle is
        /// derived from <c>finalLateralOffset / spawnDistance</c>.
        /// </summary>
        static void AssignTrajectoryDescriptors(List<TrialDefinition> trials)
        {
            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                string side = Mathf.Approximately(t.finalLateralOffset, 0f)
                    ? "C"
                    : (t.finalLateralOffset > 0f ? "R" : "L");
                int offsetBinCm = Mathf.RoundToInt(Mathf.Abs(t.finalLateralOffset) * 100f);
                t.trajectoryId = $"{t.category}_{side}_{offsetBinCm:D2}";

                if (t.spawnDistance > 0.001f)
                    t.trajectoryAngleDeg = Mathf.Rad2Deg * Mathf.Atan2(t.finalLateralOffset, t.spawnDistance);
                else
                    t.trajectoryAngleDeg = 0f;

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
