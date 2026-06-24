using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Generates the per-block trial list. 4 categories × <see cref="TrajectoryTaskAsset.TrialsPerCategory"/>.
    /// Each ball spawns at the same point in front of the player and travels in a straight line
    /// toward an end point laterally offset from the player. The category is encoded entirely in
    /// that final lateral offset, anchored on the participant's shoulder edge:
    ///   • ClearHit  — dead-center on torso.
    ///   • NearHit   — inside the shoulder line (still hits the body).
    ///   • NearMiss  — just outside the shoulder edge by 0–10 cm.
    ///   • ClearMiss — well outside the shoulder edge by 20–35 cm.
    /// All ranges scale with the participant's actual shoulder width so a narrower participant
    /// gets a narrower body but the bands stay anchored on their shoulder edge.
    /// </summary>
    public static class TrialGenerator
    {
        /// <summary>
        /// Returns the per-participant lateral scale factor.
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
                trials.AddRange(GenerateCategory(category,
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

        /// <summary>
        /// Returns exactly TWO demo trials: [clear_hit, clear_miss] in that
        /// fixed order. Used by the ball-demo phase where the participant
        /// just watches — the controller passes these to the TaskManager
        /// with passive=true so input is ignored.
        ///
        /// Both trials run at SlowSpeed so the demo is easy to follow visually
        /// and are flagged isPractice=true so the logger skips them.
        /// </summary>
        public static TrialDefinition[] GenerateBallDemoTrials(
            TrajectoryTaskAsset asset, float shoulderWidthCm = 0f)
        {
            float scale = ComputeShoulderScale(asset, shoulderWidthCm);
            var trials = new List<TrialDefinition>(2);

            trials.AddRange(GenerateCategory(
                TrialCategory.ClearHit, SemanticCommand.Hit, 1,
                asset.SpawnDistance, asset.BallDiameter, asset, scale));
            trials.AddRange(GenerateCategory(
                TrialCategory.ClearMiss, SemanticCommand.Miss, 1,
                asset.SpawnDistance, asset.BallDiameter, asset, scale));

            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                t.trialId = $"DEMO_T{i + 1:D2}";
                t.blockIndex = -1;
                t.trialIndexInBlock = i;
                t.isPractice = true;
                t.speed = asset.SlowSpeed;
                trials[i] = t;
            }
            AssignTrajectoryDescriptors(trials);
            return trials.ToArray();
        }

        /// <summary>
        /// Builds a practice block with an explicit category composition.
        /// Used for the two active-practice phases:
        ///   • Easy:      (2, 2, 0, 0)  → 4 trials
        ///   • Difficult: (2, 2, 3, 3)  → 10 trials
        ///
        /// Trials are shuffled with the same no-consecutive-category rule as
        /// a normal block, run at SlowSpeed (no fast/slow mixing during
        /// practice), and flagged isPractice=true.
        /// </summary>
        public static TrialDefinition[] GeneratePracticeTrialsWithComposition(
            TrajectoryTaskAsset asset, float shoulderWidthCm,
            int clearHits, int clearMisses, int nearHits, int nearMisses,
            string trialIdPrefix = "PRACTICE")
        {
            float scale = ComputeShoulderScale(asset, shoulderWidthCm);
            int total = clearHits + clearMisses + nearHits + nearMisses;

            Debug.LogWarning(
                $"[PracticeGenerator] {trialIdPrefix} composition: " +
                $"clearHits={clearHits}, clearMisses={clearMisses}, " +
                $"nearHits={nearHits}, nearMisses={nearMisses}, total={total}"
            );

            var trials = new List<TrialDefinition>(total);
            
            if (clearHits   > 0) trials.AddRange(GenerateCategory(TrialCategory.ClearHit,  SemanticCommand.Hit,  clearHits,   asset.SpawnDistance, asset.BallDiameter, asset, scale));
            if (nearHits > 0) trials.AddRange(GenerateCategory(TrialCategory.NearHit, SemanticCommand.Miss, nearHits, asset.SpawnDistance, asset.BallDiameter, asset, scale));            
            if (nearMisses  > 0) trials.AddRange(GenerateCategory(TrialCategory.NearMiss,  SemanticCommand.Miss, nearMisses,  asset.SpawnDistance, asset.BallDiameter, asset, scale));
            if (clearMisses > 0) trials.AddRange(GenerateCategory(TrialCategory.ClearMiss, SemanticCommand.Miss, clearMisses, asset.SpawnDistance, asset.BallDiameter, asset, scale));

            ShuffleNoConsecutive(trials);

            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                t.trialId = $"{trialIdPrefix}_T{i + 1:D2}";
                t.blockIndex = -1;
                t.trialIndexInBlock = i;
                t.isPractice = true;
                t.speed = asset.SlowSpeed;
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
            trials.AddRange(GenerateCategory(TrialCategory.ClearHit,  SemanticCommand.Hit,  perCat, spawnDistance, ballDiameter, asset, scale));
            trials.AddRange(GenerateCategory(TrialCategory.NearHit,   SemanticCommand.Miss, perCat, spawnDistance, ballDiameter, asset, scale));            trials.AddRange(GenerateCategory(TrialCategory.NearMiss,  SemanticCommand.Miss, perCat, spawnDistance, ballDiameter, asset, scale));
            trials.AddRange(GenerateCategory(TrialCategory.ClearMiss, SemanticCommand.Miss, perCat, spawnDistance, ballDiameter, asset, scale));

            ShuffleNoConsecutive(trials);
            AssignIds(trials, blockIndex);
            AssignSpeedsBySwitchSchedule(trials, asset.FastSpeed, asset.SlowSpeed);
            AssignRunMetadata(trials);
            ForceRunStartsNearBoundary(trials, spawnDistance, ballDiameter, asset, scale);
            AssignTrajectoryDescriptors(trials);

            return trials.ToArray();
        }

        /// <summary>
        /// The category is encoded by the final lateral offset, anchored on the
        /// participant's shoulder edge. Offset is defined relative to the nearest
        /// edge of the ball:
        ///   • ClearHit  — ball overlaps the body boundary.
        ///   • NearHit   — ball passes just outside the shoulder edge by 1–15 cm.
        ///   • NearMiss  — ball passes outside the shoulder edge by 15–30 cm.
        ///   • ClearMiss — ball passes outside the shoulder edge by 30–60 cm.
        /// Because Unity positions the ball by its center, the ball radius is added
        /// internally when converting edge-based offsets into center coordinates.
        /// </summary>
        static List<TrialDefinition> GenerateCategory(TrialCategory category,
            SemanticCommand expected, int count,
            float spawnDistance, float ballDiameter, TrajectoryTaskAsset asset, float shoulderScale)
        {
            var result = new List<TrialDefinition>(count);

            // Shoulder geometry, scaled to the participant.
            float referenceShoulder = asset != null && asset.ReferenceShoulderWidthCm > 0f
                ? asset.ReferenceShoulderWidthCm
                : 42f;
            float participantShoulderM = (referenceShoulder * shoulderScale) * 0.01f; // cm → m
            float shoulderHalfM = participantShoulderM * 0.5f;
            float ballRadiusM = ballDiameter * 0.5f;

            


                for (int i = 0; i < count; i++)
        {
            /// The category is encoded by the final lateral offset, anchored on the
            /// participant's shoulder edge. Offset is defined relative to the nearest
            /// edge of the ball:
            ///   • ClearHit  — ball overlaps the body boundary.
            ///   • NearHit   — ball passes just outside the shoulder edge by 1–15 cm.
            ///   • NearMiss  — ball passes outside the shoulder edge by 15–30 cm.
            ///   • ClearMiss — ball passes outside the shoulder edge by 30–60 cm.
            /// Because Unity positions the ball by its center, the ball radius is added
            /// internally when converting edge-based offsets into center coordinates.
            float edgeGapFromShoulderM = 0f;
            float offsetFromShoulderEdgeM = 0f;

            switch (category)
            {
                case TrialCategory.ClearHit:
                    /*
                    * Clear hit:
                    * The ball overlaps the body boundary.
                    * Negative edge gap means overlap.
                    */
                    edgeGapFromShoulderM = -Random.Range(0.01f, 0.15f);
                    offsetFromShoulderEdgeM = edgeGapFromShoulderM - ballRadiusM;
                    break;

                case TrialCategory.NearHit:
                    /*
                    * Near hit:
                    * Almost hits, but does NOT hit.
                    * The ball edge clears the shoulder by 1–15 cm.
                    */
                    edgeGapFromShoulderM = Random.Range(0.01f, 0.15f);
                    offsetFromShoulderEdgeM = ballRadiusM + edgeGapFromShoulderM;
                    break;

                case TrialCategory.NearMiss:
                    /*
                    * Near miss:
                    * Misses by a moderate margin.
                    * The ball edge clears the shoulder by 15–30 cm.
                    */
                    edgeGapFromShoulderM = Random.Range(0.15f, 0.30f);
                    offsetFromShoulderEdgeM = ballRadiusM + edgeGapFromShoulderM;
                    break;

                case TrialCategory.ClearMiss:
                    /*
                    * Clear miss:
                    * Obvious miss.
                    * The ball edge clears the shoulder by 30–60 cm.
                    */
                    edgeGapFromShoulderM = Random.Range(0.30f, 0.60f);
                    offsetFromShoulderEdgeM = ballRadiusM + edgeGapFromShoulderM;
                    break;
            }

            /*
            * Convert shoulder-edge-relative offset into the Unity coordinate.
            *
            * shoulderHalfM = distance from body midline to shoulder edge.
            * offsetFromShoulderEdgeM:
            *   negative = inward, toward body center
            *   positive = outward, away from body
            *
            * final ball-center magnitude from body midline:
            */
            float magnitude = shoulderHalfM + offsetFromShoulderEdgeM;
            magnitude = Mathf.Max(0f, magnitude);

            // Randomly assign left/right side.
            float side = Random.value > 0.5f ? 1f : -1f;

            // This is the actual Unity lateral coordinate relative to body midline.
            float lateral = magnitude * side;

            result.Add(new TrialDefinition
            {
                category = category,
                spawnDistance = spawnDistance,
                finalLateralOffset = lateral,
                speed = 0f, // assigned later by group pattern
                ballDiameter = ballDiameter,
                expectedResponse = expected,

                // Add this field to TrialDefinition if you want to log/analyze
                // the continuous experimental offset directly.
                shoulderEdgeOffsetM = offsetFromShoulderEdgeM,
                shoulderEdgeGapM = edgeGapFromShoulderM,
                
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
        /// Assigns slow/fast speeds using the switch schedule:
        ///   1. Start at a randomly chosen speed.
        ///   2. Hold each run for at least 3 trials.
        ///   3. From the fourth trial onward, switch with probability 0.5 before each trial.
        ///   4. Redraw any run longer than 9 trials.
        /// </summary>
        static void AssignSpeedsBySwitchSchedule(
            List<TrialDefinition> trials,
            float fastSpeed,
            float slowSpeed)
        {
            if (trials == null || trials.Count == 0)
                return;

            float currentSpeed = Random.value > 0.5f ? fastSpeed : slowSpeed;

            int trialIdx = 0;

            while (trialIdx < trials.Count)
            {
                int remaining = trials.Count - trialIdx;
                int runLength = DrawSwitchScheduleRunLength(remaining);

                for (int i = 0; i < runLength && trialIdx < trials.Count; i++)
                {
                    var t = trials[trialIdx];
                    t.speed = currentSpeed;
                    trials[trialIdx] = t;
                    trialIdx++;
                }

                // Alternate speed at the next run.
                currentSpeed = Mathf.Approximately(currentSpeed, fastSpeed)
                    ? slowSpeed
                    : fastSpeed;
            }
        }

        /// <summary>
        /// Draws one run length for the switch schedule.
        /// Minimum intended run length is 3.
        /// From trial 4 onward, the run continues with probability 0.5
        /// and switches with probability 0.5 before the next trial.
        /// Runs longer than 9 are rejected and redrawn.
        /// </summary>
        static int DrawSwitchScheduleRunLength(int remainingTrials)
        {
            if (remainingTrials <= 0)
                return 0;

            if (remainingTrials <= 3)
                return remainingTrials;

            while (true)
            {
                int runLength = 3;

                while (true)
                {
                    // If adding another trial would exceed 9, reject/redraw.
                    if (runLength >= 9)
                        break;

                    // From the fourth trial onward:
                    // switch with p = 0.5 before the next trial.
                    bool switchBeforeNextTrial = Random.value < 0.5f;

                    if (switchBeforeNextTrial)
                        break;

                    runLength++;
                }

                // Redraw any run longer than 9.
                if (runLength > 9)
                    continue;

                runLength = Mathf.Min(runLength, remainingTrials);

                // Avoid leaving a final tiny run of 1–2 trials.
                // If only 1 or 2 trials would remain, absorb them into this run
                // as long as the run does not exceed the maximum length.
                int leftover = remainingTrials - runLength;
                if (leftover > 0 && leftover < 3 && runLength + leftover <= 9)
                    runLength += leftover;

                return runLength;
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
        /// Forces the first trial of every speed run to be near-boundary.
        /// About half are NearHit and half are NearMiss.
        /// This is done after run metadata is assigned, so run-start trials
        /// are identified using trialInRun == 1.
        /// </summary>
        static void ForceRunStartsNearBoundary(
            List<TrialDefinition> trials,
            float spawnDistance,
            float ballDiameter,
            TrajectoryTaskAsset asset,
            float shoulderScale)
        {
            if (trials == null || trials.Count == 0)
                return;

            int runStartCount = 0;

            for (int i = 0; i < trials.Count; i++)
            {
                if (trials[i].trialInRun != 1)
                    continue;

                bool useNearHit = runStartCount % 2 == 0;

                // Add a little randomness so the sequence is not perfectly alternating.
                if (Random.value > 0.5f)
                    useNearHit = !useNearHit;

                TrialCategory desiredCategory = useNearHit
                    ? TrialCategory.NearHit
                    : TrialCategory.NearMiss;

                SemanticCommand desiredResponse = SemanticCommand.Miss;

                // Generate one fresh near-boundary trajectory with jittered offset.
                var replacement = GenerateCategory(
                    desiredCategory,
                    desiredResponse,
                    1,
                    spawnDistance,
                    ballDiameter,
                    asset,
                    shoulderScale
                )[0];

                var t = trials[i];

                t.category = replacement.category;
                t.finalLateralOffset = replacement.finalLateralOffset;
                t.ballDiameter = replacement.ballDiameter;
                t.expectedResponse = replacement.expectedResponse;

                // Only keep this if you added shoulderEdgeOffsetM to TrialDefinition.
                t.shoulderEdgeOffsetM = replacement.shoulderEdgeOffsetM;
                t.shoulderEdgeGapM = replacement.shoulderEdgeGapM;

                trials[i] = t;

                runStartCount++;
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