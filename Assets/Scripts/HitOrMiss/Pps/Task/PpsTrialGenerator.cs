using System;
using System.Collections.Generic;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Builds the per-block trial list from a <see cref="PpsTaskAsset"/>.
    ///
    /// The design is fully crossed and count-driven. Repetitions are DERIVED by
    /// dividing the per-type trial count by the number of cells, and generation
    /// THROWS if the division is not exact. That is deliberate: an unbalanced
    /// design is far more damaging than a failed build, and the old generator
    /// hard-coded its repetition counts, so changing a trial count in the
    /// Inspector silently produced either an exception or a lopsided block.
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

            int vtCells = stages.Length * Speeds.Length * widths.Length;
            int vtReps  = DeriveReps(asset.VtTrialsPerBlock, vtCells, "VT",
                                     $"{stages.Length} distances x {Speeds.Length} speeds x {widths.Length} width(s)");

            for (int rep = 0; rep < vtReps; rep++)
                foreach (var stage in stages)
                    foreach (var speed in Speeds)
                        foreach (var width in widths)
                            trials.Add(PpsTrialDefinition.CreateBoth(blockIndex, speed, width, stage));

            // ------------------------------------------------------------
            // 2. TACTILE-ONLY (baseline)
            // ------------------------------------------------------------
            // A silent clone of the VT timeline. Speed IS crossed, because speed sets
            // the warm-up lead and the firing time, and the baseline has to be sampled
            // at the same point on the temporal hazard curve as the VT trial it will be
            // subtracted from. Width is NOT crossed: nothing is rendered.

            int tCells = stages.Length * Speeds.Length;
            int tReps  = DeriveReps(asset.TactileOnlyTrialsPerBlock, tCells, "T",
                                    $"{stages.Length} distances x {Speeds.Length} speeds (width not crossed)");

            for (int rep = 0; rep < tReps; rep++)
                foreach (var stage in stages)
                    foreach (var speed in Speeds)
                        trials.Add(PpsTrialDefinition.CreateTactileOnly(blockIndex, speed, tactileWidth, stage));

            // ------------------------------------------------------------
            // 3. VISUAL-ONLY (catch)
            // ------------------------------------------------------------
            // No vibration ever fires, so there is no distance stage to assign and any
            // press is a false alarm. These are what stop the participant learning that
            // "LEDs approaching" means "press soon" (Kandula et al. 2017).

            int vCells = Speeds.Length * widths.Length;
            int vReps  = DeriveReps(asset.VisualOnlyTrialsPerBlock, vCells, "V",
                                    $"{Speeds.Length} speeds x {widths.Length} width(s)");

            for (int rep = 0; rep < vReps; rep++)
                foreach (var speed in Speeds)
                    foreach (var width in widths)
                        trials.Add(PpsTrialDefinition.CreateVisualOnly(blockIndex, speed, width));

            // ------------------------------------------------------------
            // 4. SAFETY CHECK
            // ------------------------------------------------------------

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
                $"VT {asset.VtTrialsPerBlock} ({vtReps} reps x {vtCells} cells) | " +
                $"T {asset.TactileOnlyTrialsPerBlock} ({tReps} reps x {tCells} cells) | " +
                $"V {asset.VisualOnlyTrialsPerBlock} ({vReps} reps x {vCells} cells) | " +
                $"width factor {(asset.UseWidthFactor ? "ON" : $"OFF, all {asset.DefaultWidth}")} | " +
                $"catch rate {100f * asset.VisualOnlyTrialsPerBlock / trials.Count:F1}%"
            );

            if (asset.OrderingStrategy == TrialOrder.Shuffled)
                Shuffle(trials, rng);

            AssignIds(trials, blockIndex);

            return trials.ToArray();
        }

        /// <summary>
        /// Repetitions per cell, derived from the total. Throws rather than rounding,
        /// because a non-integer here means the block would be unbalanced across cells
        /// and no analysis downstream would notice.
        /// </summary>
        static int DeriveReps(int totalTrials, int cellCount, string label, string cellDescription)
        {
            if (cellCount <= 0)
                throw new InvalidOperationException($"PPS {label}: cell count is {cellCount}.");

            if (totalTrials % cellCount != 0)
            {
                throw new InvalidOperationException(
                    $"PPS {label} trials per block ({totalTrials}) do not divide evenly by " +
                    $"{cellDescription} = {cellCount} cells. The design would be unbalanced. " +
                    $"Set the count to a multiple of {cellCount} " +
                    $"(nearest: {cellCount * UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(totalTrials / (float)cellCount))})."
                );
            }

            return totalTrials / cellCount;
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