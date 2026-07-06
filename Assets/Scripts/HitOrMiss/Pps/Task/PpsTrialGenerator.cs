using System;
using System.Collections.Generic;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Builds the per-block trial list from a <see cref="PpsTaskAsset"/>.
    /// Composition is percentage-based (VT / V / T) and draws speed, width, and
    /// vibration position uniformly within each condition. Total count matches
    /// <see cref="PpsTaskAsset.TrialsPerBlock"/>.
    /// </summary>
    public static class PpsTrialGenerator
    {
        static readonly PpsSpeed[] Speeds =
        {
            PpsSpeed.Fast,
            PpsSpeed.Slow
        };

        static readonly PpsWidth[] Widths =
        {
            PpsWidth.Wide,
            PpsWidth.Narrow
        };

        // UPDATED: vibration can now occur at any of the seven PPS stages.
        // D7 = farthest, D1 = nearest.
        static readonly DistanceStage[] VibStages =
        {
            DistanceStage.D7,
            DistanceStage.D6,
            DistanceStage.D5,
            DistanceStage.D4,
            DistanceStage.D3,
            DistanceStage.D2,
            DistanceStage.D1
        };

        public static PpsTrialDefinition[] Generate(PpsTaskAsset asset, int blockIndex)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var rng = asset.RngSeed.HasValue
                ? new System.Random(asset.RngSeed.Value + blockIndex)
                : new System.Random();

            var trials = new List<PpsTrialDefinition>();

            // ------------------------------------------------------------
            // 1. VISUOTACTILE TRIALS
            // ------------------------------------------------------------
            // Desired:
            // 3 repetitions x 7 distances x 4 speed-width conditions = 84 VT trials
            //
            // For each distance:
            //   Fast/Wide, Fast/Narrow, Slow/Wide, Slow/Narrow
            // repeated 3 times.
            // ------------------------------------------------------------

            const int vtRepetitionsPerConditionPerDistance = 3;

            for (int rep = 0; rep < vtRepetitionsPerConditionPerDistance; rep++)
            {
                foreach (var stage in VibStages)
                {
                    foreach (var speed in Speeds)
                    {
                        foreach (var width in Widths)
                        {
                           trials.Add(PpsTrialDefinition.CreateBoth(
                            blockIndex,
                            speed,
                            width,
                            stage
                        ));
                        }
                    }
                }
            }

            // ------------------------------------------------------------
            // 2. TACTILE-ONLY TRIALS
            // ------------------------------------------------------------
            // Desired:
            // 7 distances x 4 timing conditions = 28 T trials
            //
            // Even though width has no visual meaning in T-only trials,
            // we keep speed-width combinations so that timing is matched
            // to the VT design and the logged schema stays consistent.
            // ------------------------------------------------------------

            foreach (var stage in VibStages)
            {
                foreach (var speed in Speeds)
                {
                    foreach (var width in Widths)
                    {
                    trials.Add(PpsTrialDefinition.CreateTactileOnly(
                        blockIndex,
                        speed,
                        width,
                        stage
                    ));
                    }
                }
            }

            // ------------------------------------------------------------
            // 3. VISUAL-ONLY TRIALS
            // ------------------------------------------------------------
            // Desired:
            // 7 repetitions x 4 speed-width conditions = 28 V trials
            //
            // Visual-only trials have no vibration stage.
            // ------------------------------------------------------------

            const int visualOnlyRepetitionsPerCondition = 7;

            for (int rep = 0; rep < visualOnlyRepetitionsPerCondition; rep++)
            {
                foreach (var speed in Speeds)
                {
                    foreach (var width in Widths)
                    {
                    trials.Add(PpsTrialDefinition.CreateVisualOnly(
                        blockIndex,
                        speed,
                        width
                    ));
                    }
                }
            }

            // ------------------------------------------------------------
            // 4. SAFETY CHECK
            // ------------------------------------------------------------
            // Expected total:
            // VT = 84
            // T  = 28
            // V  = 28
            // Total = 140
            // ------------------------------------------------------------

            int expectedTotal = 140;

            if (trials.Count != expectedTotal)
            {
                throw new InvalidOperationException(
                    $"PPS trial generation error: expected {expectedTotal} trials, but generated {trials.Count}."
                );
            }

            // Optional: if your asset still has TrialsPerBlock, make sure it agrees.
            if (asset.TrialsPerBlock != expectedTotal)
            {
                throw new InvalidOperationException(
                    $"PpsTaskAsset.TrialsPerBlock is {asset.TrialsPerBlock}, but the balanced generator requires {expectedTotal} trials per block."
                );
            }

            if (asset.OrderingStrategy == TrialOrder.Shuffled)
                Shuffle(trials, rng);

            AssignIds(trials, blockIndex);

            return trials.ToArray();
        }
        /// <summary>
        /// Practice block: one of each trial type (V, T, VT).
        /// </summary>
        public static PpsTrialDefinition[] GeneratePractice(PpsTaskAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateVisualOnly(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Wide,
                    isPractice: true
                ),

                PpsTrialDefinition.CreateTactileOnly(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Wide,
                    DistanceStage.D4,
                    isPractice: true
                ),

                PpsTrialDefinition.CreateBoth(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Wide,
                    DistanceStage.D4,
                    isPractice: true
                ),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        public static PpsTrialDefinition[] GenerateVTOnlyPractice(PpsTaskAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateTactileOnly(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Wide,
                    DistanceStage.D4,
                    isPractice: true
                ),

                PpsTrialDefinition.CreateTactileOnly(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Narrow,
                    DistanceStage.D3,
                    isPractice: true
                )
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        public static PpsTrialDefinition[] GenerateVOnlyPractice(PpsTaskAsset asset)
        {
            return new[]
            {
                new PpsTrialDefinition
                {
                    trialId = "practice_visual_only_slow",
                    modality = PpsModality.VisualOnly,
                    speed = PpsSpeed.Slow,
                    width = PpsWidth.Narrow,
                    vibrationStage = DistanceStage.D4,
                    isPractice = true
                },
                new PpsTrialDefinition
                {
                    trialId = "practice_visual_only_fast",
                    modality = PpsModality.VisualOnly,
                    speed = PpsSpeed.Fast,
                    width = PpsWidth.Narrow,
                    vibrationStage = DistanceStage.D4,
                    isPractice = true
                }
            };
        }

        public static PpsTrialDefinition[] GenerateVTVisualPractice(PpsTaskAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var trials = new List<PpsTrialDefinition>
            {
                // Visual-only catch trial.
                PpsTrialDefinition.CreateVisualOnly(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Wide,
                    isPractice: true
                ),

                // Visuotactile trial.
                PpsTrialDefinition.CreateBoth(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Wide,
                    DistanceStage.D4,
                    isPractice: true
                ),

                PpsTrialDefinition.CreateBoth(
                    -1,
                    PpsSpeed.Slow,
                    PpsWidth.Narrow,
                    DistanceStage.D3,
                    isPractice: true
                )
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        static T Pick<T>(T[] arr, System.Random rng)
        {
            return arr[rng.Next(0, arr.Length)];
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