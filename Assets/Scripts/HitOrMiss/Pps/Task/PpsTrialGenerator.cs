using System;
using System.Collections.Generic;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Builds the per-block trial list from a <see cref="PpsTaskAsset"/>.
    ///
    /// The design is fully crossed and count-driven. Repetitions are DERIVED by
    /// dividing the per-type trial count by the number of cells. Division no
    /// longer has to be exact: the remainder is spread over distinct cells so no
    /// cell is ever more than ONE trial ahead of another, and which cells get the
    /// surplus ROTATES with the block index so the imbalance cancels across the
    /// session rather than sitting on the same cells every block.
    ///
    /// This replaces the old throw-on-indivisible behaviour. Throwing protected
    /// against a silently lopsided block, but it also made proportion-driven
    /// designs (for example a fixed 30 percent catch rate) impossible to express.
    /// A bounded, rotating imbalance of at most one trial per cell per block is a
    /// smaller cost than forcing the counts onto a multiple of the cell count.
    ///
    /// CELLS
    ///   VT: distances x speeds x widths
    ///   T:  distances x speeds            (width is NOT crossed, see below)
    ///   V:  speeds x widths               (no distance: no vibration fires)
    ///
    /// WIDTH
    ///   Width only changes the lateral separation of the LEDs, so it exists only
    ///   for trials that RENDER something. Tactile-only trials draw nothing and
    ///   never call SeparationFor(), so crossing width there would double the cell
    ///   count and halve the trials per timing cell for exactly no information.
    ///   The pilot did cross it, which is why the T baseline had only 6 trials per
    ///   (distance x speed) across the whole session and the facilitation came out
    ///   as noise.
    ///
    ///   Set PpsTaskAsset.UseWidthFactor to turn the factor on or off. When off,
    ///   every trial uses PpsTaskAsset.DefaultWidth (Narrow).
    /// </summary>
    public static class PpsTrialGenerator
    {
        static readonly PpsSpeed[] Speeds =
        {
            PpsSpeed.Fast,
            PpsSpeed.Slow
        };

        public static PpsTrialDefinition[] Generate(PpsTaskAsset asset, int blockIndex)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var rng = asset.RngSeed.HasValue
                ? new System.Random(asset.RngSeed.Value + blockIndex)
                : new System.Random();

            // Rotation index for the remainder allocation. Practice blocks pass -1;
            // clamp so the offset arithmetic stays non-negative.
            int rotation = UnityEngine.Mathf.Max(0, blockIndex);

            // The stages actually sampled, farthest first. With DistanceStageCount = N
            // this is the N nearest labels: 7 -> D7..D1, 6 -> D6..D1.
            DistanceStage[] stages = asset.ActiveStages;

            // Widths crossed on VISUAL trials. One entry when the factor is off.
            PpsWidth[] widths = asset.ActiveWidths;

            // Tactile-only trials render nothing, so width is fixed and meaningless.
            // It is still written to the CSV so the schema stays constant.
            PpsWidth tactileWidth = asset.DefaultWidth;

            var trials = new List<PpsTrialDefinition>();

            // ------------------------------------------------------------
            // 1. VISUOTACTILE
            // ------------------------------------------------------------

            var vtCellList = new List<(DistanceStage stage, PpsSpeed speed, PpsWidth width)>();
            foreach (var stage in stages)
                foreach (var speed in Speeds)
                    foreach (var width in widths)
                        vtCellList.Add((stage, speed, width));

            int[] vtCounts = AllocateCounts(asset.VtTrialsPerBlock, vtCellList.Count, rotation, "VT");

            for (int c = 0; c < vtCellList.Count; c++)
                for (int rep = 0; rep < vtCounts[c]; rep++)
                    trials.Add(PpsTrialDefinition.CreateBoth(
                        blockIndex, vtCellList[c].speed, vtCellList[c].width, vtCellList[c].stage));

            // ------------------------------------------------------------
            // 2. TACTILE-ONLY (baseline)
            // ------------------------------------------------------------
            // A silent clone of the VT timeline. Speed IS crossed, because speed sets
            // the warm-up lead and the firing time, and the baseline has to be sampled
            // at the same point on the temporal hazard curve as the VT trial it will be
            // subtracted from. Width is NOT crossed: nothing is rendered.

            var tCellList = new List<(DistanceStage stage, PpsSpeed speed)>();
            foreach (var stage in stages)
                foreach (var speed in Speeds)
                    tCellList.Add((stage, speed));

            int[] tCounts = AllocateCounts(asset.TactileOnlyTrialsPerBlock, tCellList.Count, rotation, "T");

            for (int c = 0; c < tCellList.Count; c++)
                for (int rep = 0; rep < tCounts[c]; rep++)
                    trials.Add(PpsTrialDefinition.CreateTactileOnly(
                        blockIndex, tCellList[c].speed, tactileWidth, tCellList[c].stage));

            // ------------------------------------------------------------
            // 3. VISUAL-ONLY (catch)
            // ------------------------------------------------------------
            // No vibration ever fires, so there is no distance stage to assign and any
            // press is a false alarm. These are what stop the participant learning that
            // "LEDs approaching" means "press soon" (Kandula et al. 2017).

            var vCellList = new List<(PpsSpeed speed, PpsWidth width)>();
            foreach (var speed in Speeds)
                foreach (var width in widths)
                    vCellList.Add((speed, width));

            int[] vCounts = AllocateCounts(asset.VisualOnlyTrialsPerBlock, vCellList.Count, rotation, "V");

            for (int c = 0; c < vCellList.Count; c++)
                for (int rep = 0; rep < vCounts[c]; rep++)
                    trials.Add(PpsTrialDefinition.CreateVisualOnly(
                        blockIndex, vCellList[c].speed, vCellList[c].width));

            // ------------------------------------------------------------
            // 4. SAFETY CHECK
            // ------------------------------------------------------------
            // Totals are exact by construction now, so this only catches a coding
            // error in the allocation, never an Inspector value.

            int expectedTotal =
                asset.VtTrialsPerBlock +
                asset.TactileOnlyTrialsPerBlock +
                asset.VisualOnlyTrialsPerBlock;

            if (trials.Count != expectedTotal)
            {
                throw new InvalidOperationException(
                    $"PPS trial generation error: expected {expectedTotal} trials " +
                    $"(VT {asset.VtTrialsPerBlock} + T {asset.TactileOnlyTrialsPerBlock} + " +
                    $"V {asset.VisualOnlyTrialsPerBlock}), generated {trials.Count}."
                );
            }

            if (asset.TrialsPerBlock != expectedTotal)
            {
                throw new InvalidOperationException(
                    $"PpsTaskAsset.TrialsPerBlock is {asset.TrialsPerBlock}, but the per-type " +
                    $"counts sum to {expectedTotal}. OnValidate should keep these in lockstep; " +
                    $"re-select the asset in the Inspector to force a refresh."
                );
            }

            UnityEngine.Debug.Log(
                $"[PpsTrialGenerator] block {blockIndex + 1}: {trials.Count} trials | " +
                $"VT {asset.VtTrialsPerBlock} ({DescribeAllocation(asset.VtTrialsPerBlock, vtCellList.Count)}) | " +
                $"T {asset.TactileOnlyTrialsPerBlock} ({DescribeAllocation(asset.TactileOnlyTrialsPerBlock, tCellList.Count)}) | " +
                $"V {asset.VisualOnlyTrialsPerBlock} ({DescribeAllocation(asset.VisualOnlyTrialsPerBlock, vCellList.Count)}) | " +
                $"width factor {(asset.UseWidthFactor ? "ON" : $"OFF, all {asset.DefaultWidth}")} | " +
                $"catch rate {100f * asset.VisualOnlyTrialsPerBlock / trials.Count:F1}%"
            );

            if (asset.OrderingStrategy == TrialOrder.Shuffled)
                Shuffle(trials, rng);

            AssignIds(trials, blockIndex);

            return trials.ToArray();
        }

        /// <summary>
        /// Trials per cell for one trial type.
        ///
        /// Every cell gets floor(total / cells). The remaining trials are handed out
        /// one each to distinct consecutive cells, starting at an offset that ADVANCES
        /// with the block index. Two consequences that matter:
        ///
        ///   1. Within a block the largest gap between any two cells is one trial.
        ///   2. Across blocks the surplus walks around the cell list, so a cell that
        ///      was over-sampled in block 1 is under-sampled later. With V = 29 over
        ///      2 cells the session comes out exactly even at 4 blocks; with VT = 53
        ///      over 14 cells every cell ends the session within one trial of every
        ///      other.
        ///
        /// Cell ORDER here is the canonical nested-loop order, not shuffled. The trial
        /// list itself is shuffled afterwards, so presentation order is unaffected.
        /// </summary>
        static int[] AllocateCounts(int totalTrials, int cellCount, int rotation, string label)
        {
            if (cellCount <= 0)
                throw new InvalidOperationException($"PPS {label}: cell count is {cellCount}.");

            var counts = new int[cellCount];

            int baseReps  = totalTrials / cellCount;
            int remainder = totalTrials - baseReps * cellCount;

            for (int i = 0; i < cellCount; i++)
                counts[i] = baseReps;

            if (remainder > 0)
            {
                int offset = (rotation * remainder) % cellCount;

                for (int k = 0; k < remainder; k++)
                    counts[(offset + k) % cellCount]++;
            }

            if (baseReps == 0 && totalTrials > 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[PpsTrialGenerator] {label}: {totalTrials} trials for {cellCount} cells means " +
                    $"{cellCount - totalTrials} cell(s) are EMPTY this block. Each cell is sampled " +
                    $"roughly once every {(float)cellCount / UnityEngine.Mathf.Max(1, totalTrials):F1} blocks."
                );
            }

            return counts;
        }

        static string DescribeAllocation(int totalTrials, int cellCount)
        {
            if (cellCount <= 0) return "0 cells";

            int baseReps  = totalTrials / cellCount;
            int remainder = totalTrials - baseReps * cellCount;

            return remainder == 0
                ? $"{baseReps} reps x {cellCount} cells"
                : $"{cellCount} cells: {cellCount - remainder} x {baseReps}, {remainder} x {baseReps + 1}";
        }

        /// <summary>
        /// Practice block: one of each trial type. Uses the asset's default width and a
        /// mid-range stage so nothing about the practice depends on the width factor.
        /// </summary>
        public static PpsTrialDefinition[] GeneratePractice(PpsTaskAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            DistanceStage mid = MidStage(asset);
            PpsWidth w = asset.DefaultWidth;

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateVisualOnly(-1, PpsSpeed.Slow, w, isPractice: true),
                PpsTrialDefinition.CreateTactileOnly(-1, PpsSpeed.Slow, w, mid, isPractice: true),
                PpsTrialDefinition.CreateBoth(-1, PpsSpeed.Slow, w, mid, isPractice: true),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        public static PpsTrialDefinition[] GenerateVTOnlyPractice(PpsTaskAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            DistanceStage mid = MidStage(asset);
            PpsWidth w = asset.DefaultWidth;

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateTactileOnly(-1, PpsSpeed.Slow, w, mid, isPractice: true),
                PpsTrialDefinition.CreateTactileOnly(-1, PpsSpeed.Fast, w, mid, isPractice: true),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        public static PpsTrialDefinition[] GenerateVOnlyPractice(PpsTaskAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            PpsWidth w = asset.DefaultWidth;

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateVisualOnly(-1, PpsSpeed.Slow, w, isPractice: true),
                PpsTrialDefinition.CreateVisualOnly(-1, PpsSpeed.Fast, w, isPractice: true),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        public static PpsTrialDefinition[] GenerateVTVisualPractice(PpsTaskAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            DistanceStage mid = MidStage(asset);
            PpsWidth w = asset.DefaultWidth;

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateVisualOnly(-1, PpsSpeed.Slow, w, isPractice: true),
                PpsTrialDefinition.CreateBoth(-1, PpsSpeed.Slow, w, mid, isPractice: true),
                PpsTrialDefinition.CreateBoth(-1, PpsSpeed.Fast, w, mid, isPractice: true),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        /// <summary>
        /// A stage roughly in the middle of the active range. Practice trials should not
        /// fire at the farthest stage (progress 0, so the vibration lands immediately at
        /// the start of the scored window) nor at D1 (the very last frame of the loom).
        /// </summary>
        static DistanceStage MidStage(PpsTaskAsset asset)
        {
            var stages = asset.ActiveStages;
            return stages[stages.Length / 2];
        }

        static void Shuffle(List<PpsTrialDefinition> trials, System.Random rng)
        {
            for (int i = trials.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (trials[i], trials[j]) = (trials[j], trials[i]);
            }
        }

        static void AssignIds(List<PpsTrialDefinition> trials, int blockIndex, bool practice = false)
        {
            string prefix = practice ? "PRACTICE" : $"B{blockIndex + 1}";

            for (int i = 0; i < trials.Count; i++)
            {
                var t = trials[i];

                t.trialId = $"{prefix}_T{i + 1:D2}";
                t.trialIndex = i;

                trials[i] = t;
            }
        }
    }
}