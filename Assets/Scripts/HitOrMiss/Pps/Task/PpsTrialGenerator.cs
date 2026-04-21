using System;
using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Builds the per-block trial list from a <see cref="PpsTaskAsset"/>.
    /// Full factorial expansion: (Speed × Width × VisualOnly)
    ///                        + (Speed × Width × {D4..D1} × Both)
    ///                        + ({D4..D1} × TactileOnly)          [optional]
    /// then multiplied by the asset's RepeatsPerCell, then ordered.
    /// </summary>
    public static class PpsTrialGenerator
    {
        static readonly PpsSpeed[] Speeds = { PpsSpeed.Fast, PpsSpeed.Slow };
        static readonly PpsWidth[] Widths = { PpsWidth.Wide, PpsWidth.Narrow };
        static readonly DistanceStage[] VibStages = { DistanceStage.D4, DistanceStage.D3, DistanceStage.D2, DistanceStage.D1 };

        public static PpsTrialDefinition[] Generate(PpsTaskAsset asset, int blockIndex)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var trials = new List<PpsTrialDefinition>();

            for (int rep = 0; rep < asset.RepeatsPerCell; rep++)
            {
                foreach (var s in Speeds)
                    foreach (var w in Widths)
                        trials.Add(PpsTrialDefinition.CreateVisualOnly(blockIndex, s, w));

                foreach (var s in Speeds)
                    foreach (var w in Widths)
                        foreach (var stage in VibStages)
                            trials.Add(PpsTrialDefinition.CreateBoth(blockIndex, s, w, stage));

                if (asset.IncludeTactileOnlyCells)
                    foreach (var stage in VibStages)
                        trials.Add(PpsTrialDefinition.CreateTactileOnly(blockIndex, stage));
            }

            Order(trials, asset.OrderingStrategy, asset.RngSeed);
            AssignIds(trials, blockIndex);
            return trials.ToArray();
        }

        /// <summary>
        /// Practice block: one of each trial type (visual, tactile, both), minimal.
        /// Used by <see cref="PpsTaskManager"/> before the main blocks.
        /// </summary>
        public static PpsTrialDefinition[] GeneratePractice(PpsTaskAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateVisualOnly(-1, PpsSpeed.Slow, PpsWidth.Wide, isPractice: true),
                PpsTrialDefinition.CreateTactileOnly(-1, DistanceStage.D2, isPractice: true),
                PpsTrialDefinition.CreateBoth(-1, PpsSpeed.Slow, PpsWidth.Wide, DistanceStage.D2, isPractice: true),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        static void Order(List<PpsTrialDefinition> trials, TrialOrder strategy, int? seed)
        {
            switch (strategy)
            {
                case TrialOrder.Sequential:
                    return;
                case TrialOrder.Shuffled:
                    Shuffle(trials, seed);
                    return;
                default:
                    Shuffle(trials, seed);
                    return;
            }
        }

        static void Shuffle(List<PpsTrialDefinition> trials, int? seed)
        {
            // Use a local System.Random so the shuffle is independent of UnityEngine.Random state.
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
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
                trials[i] = t;
            }
        }
    }
}
