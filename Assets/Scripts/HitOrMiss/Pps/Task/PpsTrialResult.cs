using System;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Scored result for one PPS trial. Written to CSV with PPS timing schema.
    /// </summary>
    [Serializable]
    public struct PpsTrialResult
    {
        public PpsTrialDefinition definition;
        /// <summary>Time.timeAsDouble at the beginning of the trial.</summary>
        public double trialStartTime;

        /// <summary>Time.timeAsDouble at loom onset. NaN for T trials (no visual stimulus).</summary>
        public double loomOnsetTime;

        /// <summary>Time.timeAsDouble at which the scored window opened (D7).
        /// Equals loomOnsetTime + the warm-up lead. Nothing perceptible happens here;
        /// it is an analysis bookmark, not a stimulus event. NaN for T trials.</summary>
        public double d7OnsetTime;

        /// <summary>Time.timeAsDouble at each position crossing. NaN for T trials (no crossings exist).</summary>
        public double crossingD7Time;
        public double crossingD6Time;
        public double crossingD5Time;
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

        /// <summary>Metric distance of the vibration stage. NaN for V trials.</summary>
        public float vibrationDistanceMeters;

        /// <summary>Loom geometry for this trial, so the CSV is self-describing.</summary>
        public float loomStartMeters;
        public float loomEndMeters;

        public static PpsTrialResult Empty(PpsTrialDefinition def) => new()
        {
            definition = def,
            trialStartTime = double.NaN,
            loomOnsetTime = double.NaN,
            d7OnsetTime = double.NaN,
            crossingD7Time = double.NaN,
            crossingD6Time = double.NaN,
            crossingD5Time = double.NaN,
            crossingD4Time = double.NaN,
            crossingD3Time = double.NaN,
            crossingD2Time = double.NaN,
            crossingD1Time = double.NaN,
            vibrationFiredTime = double.NaN,
            responseTime = double.NaN,
            responded = false,
            reactionTimeMs = float.NaN,
            vibrationDeviceName = string.Empty,
            vibrationDistanceMeters = float.NaN,
            loomStartMeters = float.NaN,
            loomEndMeters = float.NaN,
        };

        static string MsOrBlank(double timeSeconds) =>
            double.IsNaN(timeSeconds) ? "" : (timeSeconds * 1000.0).ToString("F3");

        static string F1OrBlank(float value) =>
            float.IsNaN(value) ? "" : value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        static string F3OrBlank(float value) =>
            float.IsNaN(value) ? "" : value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        static string Esc(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace(",", ";");

        public string ToCsvRow(string subjectId, int sessionNumber)
        {
            string trialType = definition.modality switch
            {
                PpsModality.Both        => "VT",
                PpsModality.VisualOnly  => "V",
                PpsModality.TactileOnly => "T",
                _ => "?"
            };

            string position = definition.vibrationStage == DistanceStage.None
                ? ""
                : definition.vibrationStage.ToString();

            string timestamp = DateTime.Now.ToString("o");

            return string.Join(",",
                Esc(subjectId),
                sessionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (definition.blockIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                (definition.trialIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),

                trialType,
                responded ? "1" : "0",

                definition.speed.ToString().ToLowerInvariant(),
                definition.width.ToString().ToLowerInvariant(),
                position,

                MsOrBlank(trialStartTime),
                MsOrBlank(loomOnsetTime),
                MsOrBlank(d7OnsetTime), 
                MsOrBlank(crossingD7Time),
                MsOrBlank(crossingD6Time),
                MsOrBlank(crossingD5Time),
                MsOrBlank(crossingD4Time),
                MsOrBlank(crossingD3Time),
                MsOrBlank(crossingD2Time),
                MsOrBlank(crossingD1Time),
                MsOrBlank(vibrationFiredTime),
                MsOrBlank(responseTime),
                F1OrBlank(reactionTimeMs),

                F3OrBlank(vibrationDistanceMeters),
                F3OrBlank(loomStartMeters),
                F3OrBlank(loomEndMeters),

                Esc(vibrationDeviceName),
                "0",
                timestamp
            );

        }

        public const string CsvHeader =
                    "subject_id,session_number,block_number,trial_number," +
                    "trial_type,response_made," +
                    "current_speed,width,distance_level," +
                    "trial_start_ms,loom_onset_ms,d7_onset_ms," +
                    "position_D7_ms,position_D6_ms,position_D5_ms,position_D4_ms," +
                    "position_D3_ms,position_D2_ms,position_D1_ms," +
                    "vibrotactile_onset_ms,response_time_ms,reaction_time_ms," +
                    "distance_m,loom_start_m,loom_end_m," +
                    "vibration_device,trial_interrupted,timestamp";
    }
}