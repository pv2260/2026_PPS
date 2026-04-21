using System;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Scored result for one PPS trial. Written to CSV and broadcast via
    /// <see cref="PpsTaskManager.TrialCompleted"/>.
    /// </summary>
    [Serializable]
    public struct PpsTrialResult
    {
        public PpsTrialDefinition definition;

        /// <summary>Time.timeAsDouble at loom onset (or trial start for tactile-only).</summary>
        public double loomOnsetTime;

        /// <summary>Time.timeAsDouble at which the vibrotactile event fired. NaN if it never fired.</summary>
        public double vibrationFiredTime;

        /// <summary>Time.timeAsDouble of the first response. NaN if none.</summary>
        public double responseTime;

        public bool responded;

        /// <summary>Reaction time in ms measured from the vibration event (not loom onset).</summary>
        public float reactionTimeMs;

        public string vibrationDeviceName;

        public static PpsTrialResult Empty(PpsTrialDefinition def) => new()
        {
            definition = def,
            loomOnsetTime = double.NaN,
            vibrationFiredTime = double.NaN,
            responseTime = double.NaN,
            responded = false,
            reactionTimeMs = float.NaN,
            vibrationDeviceName = string.Empty,
        };

        public string ToCsvRow()
        {
            string Fmt(double v) => double.IsNaN(v) ? "" : v.ToString("F6");
            string Rt = float.IsNaN(reactionTimeMs) ? "" : reactionTimeMs.ToString("F2");

            return string.Join(",",
                definition.trialId,
                definition.blockIndex.ToString(),
                definition.isPractice ? "1" : "0",
                definition.modality.ToString(),
                definition.speed.ToString(),
                definition.width.ToString(),
                definition.vibrationStage.ToString(),
                Fmt(loomOnsetTime),
                Fmt(vibrationFiredTime),
                Fmt(responseTime),
                Rt,
                responded ? "1" : "0",
                vibrationDeviceName);
        }

        public const string CsvHeader =
            "TrialId,Block,IsPractice,Modality,Speed,Width,VibrationStage,LoomOnset,VibrationFired,ResponseTime,RtFromVibMs,Responded,VibrationDevice";
    }
}
