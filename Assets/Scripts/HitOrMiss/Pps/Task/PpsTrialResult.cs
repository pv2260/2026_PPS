using System;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Scored result for one PPS trial. Written to CSV with the schema specified
    /// in the VR Development Brief (trial, block, sensory_condition, position,
    /// speed, width, stimulus_onset_ms, position_D{4..1}_ms, vibrotactile_onset_ms,
    /// response_time_ms, response_made).
    /// </summary>
    [Serializable]
    public struct PpsTrialResult
    {
        public PpsTrialDefinition definition;

        /// <summary>Time.timeAsDouble at loom onset. NaN for T trials (no visual stimulus).</summary>
        public double loomOnsetTime;

        /// <summary>Time.timeAsDouble at each position crossing. NaN for T trials (no crossings exist).</summary>
        public double crossingD4Time;
        public double crossingD3Time;
        public double crossingD2Time;
        public double crossingD1Time;

        /// <summary>Time.timeAsDouble at vibrotactile delivery. NaN for V trials.</summary>
        public double vibrationFiredTime;

        /// <summary>Time.timeAsDouble of the first response. NaN if none.</summary>
        public double responseTime;

        public bool responded;

        /// <summary>Reaction time measured from the vibration event. NaN if no vibration or no response.</summary>
        public float reactionTimeMs;

        public string vibrationDeviceName;

        public static PpsTrialResult Empty(PpsTrialDefinition def) => new()
        {
            definition = def,
            loomOnsetTime = double.NaN,
            crossingD4Time = double.NaN,
            crossingD3Time = double.NaN,
            crossingD2Time = double.NaN,
            crossingD1Time = double.NaN,
            vibrationFiredTime = double.NaN,
            responseTime = double.NaN,
            responded = false,
            reactionTimeMs = float.NaN,
            vibrationDeviceName = string.Empty,
        };

        static string MsOrBlank(double timeSeconds) =>
            double.IsNaN(timeSeconds) ? "" : (timeSeconds * 1000.0).ToString("F3");

        public string ToCsvRow()
        {
            string sensory = definition.modality switch
            {
                PpsModality.Both        => "VT",
                PpsModality.VisualOnly  => "V",
                PpsModality.TactileOnly => "T",
                _ => "?",
            };
            string position = definition.vibrationStage == DistanceStage.None
                ? ""
                : definition.vibrationStage.ToString();

            return string.Join(",",
                definition.trialId,
                (definition.blockIndex + 1).ToString(),
                sensory,
                position,
                definition.speed.ToString().ToLowerInvariant(),
                definition.width.ToString().ToLowerInvariant(),
                MsOrBlank(loomOnsetTime),
                MsOrBlank(crossingD4Time),
                MsOrBlank(crossingD3Time),
                MsOrBlank(crossingD2Time),
                MsOrBlank(crossingD1Time),
                MsOrBlank(vibrationFiredTime),
                MsOrBlank(responseTime),
                responded ? "True" : "False");
        }

        public const string CsvHeader =
            "trial,block,sensory_condition,position,speed,width," +
            "stimulus_onset_ms,position_D4_ms,position_D3_ms,position_D2_ms,position_D1_ms," +
            "vibrotactile_onset_ms,response_time_ms,response_made";
    }
}
