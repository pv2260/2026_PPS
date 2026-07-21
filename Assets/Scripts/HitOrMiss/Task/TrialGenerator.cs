using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Generates the per-block trial list, anchored on the participant's shoulder edge.
    /// The category is encoded entirely in the final lateral offset. Convention
    /// (matches the preregistration): the signed EDGE offset is measured from the
    /// shoulder edge, negative = ball edge INSIDE the body (physical hit), positive =
    /// ball edge OUTSIDE the shoulder (physical miss).
    ///   • ClearHit  — edge well inside  (large negative, e.g. -30..-15). Judged HIT.
    ///   • NearHit   — edge just inside   (small negative, e.g. -15..-2).  Judged HIT, near boundary.
    ///   • NearMiss  — edge just outside  (small positive, e.g.  +2..+15). Judged MISS, near boundary.
    ///   • ClearMiss — edge well outside  (large positive, e.g. +15..+30). Judged MISS.
    ///   • Grey zone — sparse trials within +/- GreyZoneHalfWidthCm of the boundary,
    ///                 labelled NearHit/NearMiss by sign (no fifth enum value).
    ///
    /// The expected response is ALWAYS derived from the sign of the physical edge offset,
    /// never hard-coded per category, so a mislabelled category cannot score a participant
    /// incorrectly. The body half-width tracks the participant's real shoulders; the offset
    /// bands do NOT rescale unless the asset flag ScaleOffsetBandsByShoulderWidth is set.
    /// perceivedBoundaryOffsetCm shifts offset 0 onto the participant's staircase-estimated
    /// collision boundary (default 0 = physical shoulder edge).
    /// </summary>
    public static class TrialGenerator
    {
        /// <summary>
        /// Returns the per-participant lateral scale factor for the BODY half-width.
        /// <paramref name="shoulderWidthCm"/> &lt;= 0 returns 1.0 (no scaling).
        /// </summary>
        static float ComputeShoulderScale(TrajectoryTaskAsset asset, float shoulderWidthCm)
        {
            if (shoulderWidthCm <= 0f) return 1f;
            float reference = asset != null && asset.ReferenceShoulderWidthCm > 0f
                ? asset.ReferenceShoulderWidthCm
                : 42f;
            return shoulderWidthCm / reference;
        }

        static float RandomEdgeGapCmForCategory(
            TrajectoryTaskAsset asset,
            TrialCategory category,
            float shoulderScale)
        {
            float minCm;
            float maxCm;

            switch (category)
            {
                case TrialCategory.ClearHit:
                    minCm = asset.ClearHitMinOffsetCm;
                    maxCm = asset.ClearHitMaxOffsetCm;
                    break;

                case TrialCategory.NearHit:
                    minCm = asset.NearHitMinOffsetCm;
                    maxCm = asset.NearHitMaxOffsetCm;
                    break;

                case TrialCategory.NearMiss:
                    minCm = asset.NearMissMinOffsetCm;
                    maxCm = asset.NearMissMaxOffsetCm;
                    break;

                case TrialCategory.ClearMiss:
                    minCm = asset.ClearMissMinOffsetCm;
                    maxCm = asset.ClearMissMaxOffsetCm;
                    break;

                default:
                    minCm = 0f;
                    maxCm = 0f;
                    break;
            }

            // Only the body half-width tracks shoulder width; the bands stay fixed in cm
            // unless the asset explicitly opts into band scaling.
            float scale = asset.ScaleOffsetBandsByShoulderWidth ? shoulderScale : 1f;

            return Random.Range(minCm, maxCm) * scale;
        }

        /// <summary>
        /// Response is decided ONLY by the sign of the physical edge offset:
        /// edge inside the body (&lt; 0) is a physical hit, edge outside (&gt;= 0) is a miss.
        /// Tangent (exactly 0) counts as a miss (no penetration).
        /// </summary>
        static SemanticCommand ExpectedFromEdgeGap(float physicalEdgeGapM)
        {
            return physicalEdgeGapM < 0f ? SemanticCommand.Hit : SemanticCommand.Miss;
        }


        /// <summary>
        /// Generates a small set of practice trials for the practice phase
        /// (PDF popup 4). Alternates Hit / Miss outcomes so the participant
        /// experiences both possible responses with feedback.
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
        /// just watches.
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
        /// Trials are shuffled with the no-consecutive-category rule, run at
        /// SlowSpeed, and flagged isPractice = true. The expected response for
        /// each trial is derived from its edge-offset sign, so the SemanticCommand
        /// arguments below are only nominal hints.
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
            if (nearHits    > 0) trials.AddRange(GenerateCategory(TrialCategory.NearHit,   SemanticCommand.Hit,  nearHits,    asset.SpawnDistance, asset.BallDiameter, asset, scale));
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


        public static TrialDefinition[] GenerateBlock(
            int blockIndex,
            TrajectoryTaskAsset asset,
            float shoulderWidthCm = 0f,
            float perceivedBoundaryOffsetCm = 0f)
        {
            float spawnDistance = asset.SpawnDistance;
            float ballDiameter = asset.BallDiameter;
            float scale = ComputeShoulderScale(asset, shoulderWidthCm);

            var trials = new List<TrialDefinition>(asset.TrialsPerBlock);

            if (asset.ClearHitTrialsPerBlock > 0)
            {
                trials.AddRange(GenerateCategory(
                    TrialCategory.ClearHit,
                    SemanticCommand.Hit,
                    asset.ClearHitTrialsPerBlock,
                    spawnDistance,
                    ballDiameter,
                    asset,
                    scale,
                    perceivedBoundaryOffsetCm));
            }

            if (asset.NearHitTrialsPerBlock > 0)
            {
                trials.AddRange(GenerateCategory(
                    TrialCategory.NearHit,
                    SemanticCommand.Hit,
                    asset.NearHitTrialsPerBlock,
                    spawnDistance,
                    ballDiameter,
                    asset,
                    scale,
                    perceivedBoundaryOffsetCm));
            }

            if (asset.NearMissTrialsPerBlock > 0)
            {
                trials.AddRange(GenerateCategory(
                    TrialCategory.NearMiss,
                    SemanticCommand.Miss,
                    asset.NearMissTrialsPerBlock,
                    spawnDistance,
                    ballDiameter,
                    asset,
                    scale,
                    perceivedBoundaryOffsetCm));
            }

            if (asset.ClearMissTrialsPerBlock > 0)
            {
                trials.AddRange(GenerateCategory(
                    TrialCategory.ClearMiss,
                    SemanticCommand.Miss,
                    asset.ClearMissTrialsPerBlock,
                    spawnDistance,
                    ballDiameter,
                    asset,
                    scale,
                    perceivedBoundaryOffsetCm));
            }

            // Sparse grey-zone sampling straddling the boundary. Each grey trial
            // is labelled NearHit or NearMiss by the sign of its edge offset.
            if (asset.GreyZoneTrialsPerBlock > 0)
            {
                trials.AddRange(GenerateGreyZone(
                    asset.GreyZoneTrialsPerBlock,
                    spawnDistance,
                    ballDiameter,
                    asset,
                    scale,
                    perceivedBoundaryOffsetCm));
            }

            ShuffleNoConsecutive(trials);
            AssignIds(trials, blockIndex);
            AssignSpeedsBySwitchSchedule(trials, asset.FastSpeed, asset.SlowSpeed);
            AssignRunMetadata(trials);

            // IMPORTANT:
            // Do NOT call ForceRunStartsNearBoundary here if you want exact category counts.
            // That function changes categories after the block has already been built.

            AssignTrajectoryDescriptors(trials);

            return trials.ToArray();
        }



        static List<TrialDefinition> GenerateCategory(
            TrialCategory category,
            SemanticCommand expected,
            int count,
            float spawnDistance,
            float ballDiameter,
            TrajectoryTaskAsset asset,
            float shoulderScale,
            float perceivedBoundaryOffsetCm = 0f)
        {
            var result = new List<TrialDefinition>(count);

            float referenceShoulderCm =
                asset != null && asset.ReferenceShoulderWidthCm > 0f
                    ? asset.ReferenceShoulderWidthCm
                    : 42f;

            // Body half-width tracks the participant's real shoulders.
            float participantShoulderCm = referenceShoulderCm * shoulderScale;
            float shoulderHalfM = participantShoulderCm * 0.01f * 0.5f;
            float ballRadiusM = ballDiameter * 0.5f;

            for (int i = 0; i < count; i++)
            {
                // Offset sampled relative to the participant's (perceived) boundary.
                float sampledEdgeGapCm = RandomEdgeGapCmForCategory(asset, category, shoulderScale);

                // Physical edge offset relative to the shoulder edge. With the
                // staircase term at 0 this equals the sampled value. When wired,
                // the whole sampling window slides onto the participant's crossover
                // while hit/miss ground truth stays physical.
                float physicalEdgeGapCm = sampledEdgeGapCm + perceivedBoundaryOffsetCm;
                float physicalEdgeGapM = physicalEdgeGapCm * 0.01f;

                // Convert edge-based offset to a ball-CENTER coordinate.
                //   nearest ball edge = center - ballRadius
                //   edgeGap = nearest edge - shoulder edge
                //   => center relative to shoulder edge = ballRadius + edgeGap
                float centerOffsetFromShoulderEdgeM = ballRadiusM + physicalEdgeGapM;

                float magnitude = shoulderHalfM + centerOffsetFromShoulderEdgeM;
                magnitude = Mathf.Max(0f, magnitude);

                float side = Random.value > 0.5f ? 1f : -1f;
                float lateral = magnitude * side;

                // Response is decided by physical geometry, not the category label.
                SemanticCommand derivedExpected = ExpectedFromEdgeGap(physicalEdgeGapM);

                result.Add(new TrialDefinition
                {
                    category = category,
                    spawnDistance = spawnDistance,
                    finalLateralOffset = lateral,
                    speed = 0f,
                    ballDiameter = ballDiameter,
                    expectedResponse = derivedExpected,

                    // Continuous physical edge offset: this is the value to log and analyse.
                    shoulderEdgeGapM = physicalEdgeGapM,
                    shoulderEdgeOffsetM = physicalEdgeGapM,
                });
            }

            return result;
        }

        /// <summary>
        /// Sparse trials sampled uniformly within +/- GreyZoneHalfWidthCm of the
        /// boundary. Each is labelled NearHit (edge inside) or NearMiss (edge
        /// outside) by the sign of its physical edge offset, with the expected
        /// response derived the same way. Identify grey trials in analysis by
        /// |shoulderEdgeGapM| &lt;= half width.
        /// </summary>
        static List<TrialDefinition> GenerateGreyZone(
            int count,
            float spawnDistance,
            float ballDiameter,
            TrajectoryTaskAsset asset,
            float shoulderScale,
            float perceivedBoundaryOffsetCm = 0f)
        {
            var result = new List<TrialDefinition>(count);

            float referenceShoulderCm =
                asset != null && asset.ReferenceShoulderWidthCm > 0f
                    ? asset.ReferenceShoulderWidthCm
                    : 42f;

            float participantShoulderCm = referenceShoulderCm * shoulderScale;
            float shoulderHalfM = participantShoulderCm * 0.01f * 0.5f;
            float ballRadiusM = ballDiameter * 0.5f;

            float halfCm = asset != null ? Mathf.Max(0f, asset.GreyZoneHalfWidthCm) : 2f;

            for (int i = 0; i < count; i++)
            {
                float sampledEdgeGapCm = Random.Range(-halfCm, halfCm);
                float physicalEdgeGapCm = sampledEdgeGapCm + perceivedBoundaryOffsetCm;
                float physicalEdgeGapM = physicalEdgeGapCm * 0.01f;

                float centerOffsetFromShoulderEdgeM = ballRadiusM + physicalEdgeGapM;
                float magnitude = Mathf.Max(0f, shoulderHalfM + centerOffsetFromShoulderEdgeM);

                float side = Random.value > 0.5f ? 1f : -1f;
                float lateral = magnitude * side;

                TrialCategory cat = physicalEdgeGapM < 0f ? TrialCategory.NearHit : TrialCategory.NearMiss;
                SemanticCommand exp = ExpectedFromEdgeGap(physicalEdgeGapM);

                result.Add(new TrialDefinition
                {
                    category = cat,
                    spawnDistance = spawnDistance,
                    finalLateralOffset = lateral,
                    speed = 0f,
                    ballDiameter = ballDiameter,
                    expectedResponse = exp,
                    shoulderEdgeGapM = physicalEdgeGapM,
                    shoulderEdgeOffsetM = physicalEdgeGapM,
                });
            }

            return result;
        }


        /// <summary>
        /// Shuffle so no two consecutive trials share a category. Greedy fix-up after Fisher-Yates.
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
            // immediately preceding trial's speed (per spec). The first trial of
            // the block has prevSpeed = 0 so the resolver can detect "start".
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

                // Avoid leaving a final tiny run of 1-2 trials.
                int leftover = remainingTrials - runLength;
                if (leftover > 0 && leftover < 3 && runLength + leftover <= 9)
                    runLength += leftover;

                return runLength;
            }
        }

        /// <summary>
        /// Synthesizes a stable <c>trajectoryId</c> string and approach angle
        /// for each trial. The id encodes category + side + signed EDGE-offset bin
        /// (in cm) so trials sharing a shape group together in analysis. The angle
        /// is derived from <c>finalLateralOffset / spawnDistance</c>.
        /// </summary>
        static void AssignTrajectoryDescriptors(List<TrialDefinition> trials)
        {
            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];
                string side = Mathf.Approximately(t.finalLateralOffset, 0f)
                    ? "C"
                    : (t.finalLateralOffset > 0f ? "R" : "L");

                // Bin on the signed edge offset (inside negative, outside positive),
                // not the full lateral world position.
                int edgeBinCm = Mathf.RoundToInt(t.shoulderEdgeGapM * 100f);
                string sign = edgeBinCm < 0 ? "N" : "P";
                t.trajectoryId = $"{t.category}_{side}_{sign}{Mathf.Abs(edgeBinCm):D2}";

                if (t.spawnDistance > 0.001f)
                    t.trajectoryAngleDeg = Mathf.Rad2Deg * Mathf.Atan2(t.finalLateralOffset, t.spawnDistance);
                else
                    t.trajectoryAngleDeg = 0f;

                trials[i] = t;
            }
        }

        /// <summary>
        /// Forces the first trial of every speed run to be near-boundary.
        /// About half are NearHit and half are NearMiss. Expected responses are
        /// derived from edge-offset sign inside GenerateCategory, so run-start
        /// trials are scored correctly regardless of which side they land on.
        /// Not called by GenerateBlock (it changes category counts); enable only
        /// if you accept approximate per-category counts in exchange for
        /// guaranteed near-boundary run starts.
        /// </summary>
        static void ForceRunStartsNearBoundary(
            List<TrialDefinition> trials,
            float spawnDistance,
            float ballDiameter,
            TrajectoryTaskAsset asset,
            float shoulderScale,
            float perceivedBoundaryOffsetCm = 0f)
        {
            if (trials == null || trials.Count == 0)
                return;

            int runStartCount = 0;

            for (int i = 0; i < trials.Count; i++)
            {
                if (trials[i].trialInRun != 1)
                    continue;

                bool useNearHit = runStartCount % 2 == 0;

                if (Random.value > 0.5f)
                    useNearHit = !useNearHit;

                TrialCategory desiredCategory = useNearHit
                    ? TrialCategory.NearHit
                    : TrialCategory.NearMiss;

                // Nominal hint only; the replacement's response is set by sign.
                SemanticCommand desiredResponse = useNearHit
                    ? SemanticCommand.Hit
                    : SemanticCommand.Miss;

                var replacement = GenerateCategory(
                    desiredCategory,
                    desiredResponse,
                    1,
                    spawnDistance,
                    ballDiameter,
                    asset,
                    shoulderScale,
                    perceivedBoundaryOffsetCm
                )[0];

                var t = trials[i];

                t.category = replacement.category;
                t.finalLateralOffset = replacement.finalLateralOffset;
                t.ballDiameter = replacement.ballDiameter;
                t.expectedResponse = replacement.expectedResponse;
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