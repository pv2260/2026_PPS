using System;
using System.Collections.Generic;

namespace HitOrMiss.Pps
{
    public static class PpsTrialGenerator
    {
        static readonly PpsSpeed[] Speeds = { PpsSpeed.Fast, PpsSpeed.Slow };
        static readonly PpsWidth[] Widths = { PpsWidth.Wide, PpsWidth.Narrow };
        static readonly DistanceStage[] VibStages = { DistanceStage.D4, DistanceStage.D3, DistanceStage.D2, DistanceStage.D1 };

        public static PpsTrialDefinition[] Generate(PpsTaskAsset asset, int blockIndex)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var rng = asset.RngSeed.HasValue
                ? new System.Random(asset.RngSeed.Value + blockIndex)
                : new System.Random();

            int total = asset.TrialsPerBlock;
            int nVT = (int)Math.Round(total * asset.PercentVT);
            int nV = (int)Math.Round(total * asset.PercentV);
            int nT = Math.Max(0, total - nVT - nV);

            var trials = new List<PpsTrialDefinition>(total);

            for (int i = 0; i < nVT; i++)
                trials.Add(PpsTrialDefinition.CreateBoth(
                    blockIndex, Pick(Speeds, rng), Pick(Widths, rng), Pick(VibStages, rng)));

            for (int i = 0; i < nV; i++)
                trials.Add(PpsTrialDefinition.CreateVisualOnly(
                    blockIndex, Pick(Speeds, rng), Pick(Widths, rng)));

            for (int i = 0; i < nT; i++)
                trials.Add(PpsTrialDefinition.CreateTactileOnly(
                    blockIndex, Pick(Speeds, rng), Pick(Widths, rng), Pick(VibStages, rng)));

            if (asset.OrderingStrategy == TrialOrder.Shuffled)
                Shuffle(trials, rng);

            AssignIds(trials, blockIndex);
            return trials.ToArray();
        }

        public static PpsTrialDefinition[] GeneratePractice(PpsTaskAsset asset)
        {
            return GenerateChestVibrationPractice(asset);
        }

        public static PpsTrialDefinition[] GenerateChestVibrationPractice(PpsTaskAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateTactileOnly(
                    -1, PpsSpeed.Slow, PpsWidth.Wide, DistanceStage.D2, isPractice: true),

                PpsTrialDefinition.CreateTactileOnly(
                    -1, PpsSpeed.Slow, PpsWidth.Wide, DistanceStage.D2, isPractice: true),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        public static PpsTrialDefinition[] GenerateLightsAndVibrationPractice(PpsTaskAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var trials = new List<PpsTrialDefinition>
            {
                PpsTrialDefinition.CreateBoth(
                    -1, PpsSpeed.Slow, PpsWidth.Wide, DistanceStage.D2, isPractice: true),

                PpsTrialDefinition.CreateBoth(
                    -1, PpsSpeed.Slow, PpsWidth.Wide, DistanceStage.D2, isPractice: true),
            };

            AssignIds(trials, blockIndex: 0, practice: true);
            return trials.ToArray();
        }

        static T Pick<T>(T[] arr, System.Random rng) => arr[rng.Next(0, arr.Length)];

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
                trials[i] = t;
            }
        }
    }
}