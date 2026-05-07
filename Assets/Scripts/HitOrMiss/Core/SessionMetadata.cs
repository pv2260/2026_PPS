using System;

namespace HitOrMiss
{
    /// <summary>
    /// Per-session metadata captured by the clinician on the New Session form.
    /// Serialized as <c>setup.json</c> in each session folder using the
    /// nested schema from the spec (subject / session / equipment /
    /// task1_parameters / task2_parameters / comments). Trial rows still
    /// embed the participant + condition fields via flatten methods so each
    /// trials.csv is self-contained.
    /// </summary>
    [Serializable]
    public struct SessionMetadata
    {
        // ---- Flat working fields used by the runtime + filled by the GUI ----
        public string participantId;
        public string clinicianInitials;
        public string sessionDate;     // yyyy-MM-dd
        public string sessionId;       // yyyyMMdd_HHmmss
        public int sessionNumber;

        // Subject
        public int ageYears;
        public DominantHand dominantHand;
        public float heightCm;
        public float shoulderWidthCm;
        public string subjectGroup;    // "patient" | "healthy"
        public bool hasDbs;

        // Session condition
        public SessionType sessionType;
        public DbsStatus dbsStatus;
        public string language;        // "english" | "french"

        // Equipment flags (per spec setup.json.equipment)
        public bool eegEnabled;
        public bool emgEnabled;
        public bool heartRateBandEnabled;
        public bool eyeTrackingEnabled;

        // Task config snapshot — Task 1 (PPS)
        public int task1NumberOfBlocks;
        public int task1TrialsPerBlock;
        public float task1BreakDurationSeconds;
        public float task1NarrowOffsetCm;
        public float task1WideOffsetCm;
        public string[] task1LoomingSpeeds;       // {"slow","medium","fast"} subset
        public int task1PracticeVtOnlyTrials;
        public int task1PracticeVtVisualTrials;

        // Task config snapshot — Task 2 (Hit-or-Miss)
        public int task2NumberOfBlocks;
        public int task2TrialsPerBlock;
        public float task2BreakDurationSeconds;
        public float task2HitOffsetCm;
        public float task2NearMissOffsetCm;
        public float task2MissOffsetCm;
        public string[] task2BallSpeeds;          // {"slow","medium","fast"} subset

        // Free-form
        public string clinicianNotes;

        public static SessionMetadata CreateDefault(string participantId)
        {
            return new SessionMetadata
            {
                participantId = string.IsNullOrEmpty(participantId) ? "P000" : participantId,
                clinicianInitials = string.Empty,
                sessionDate = DateTime.Now.ToString("yyyy-MM-dd"),
                sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                sessionNumber = 1,

                ageYears = 0,
                dominantHand = DominantHand.Unspecified,
                heightCm = 0f,
                shoulderWidthCm = 42f,
                subjectGroup = "healthy",
                hasDbs = false,

                sessionType = SessionType.HealthySession1,
                dbsStatus = DbsStatus.Na,
                language = "english",

                eegEnabled = false,
                emgEnabled = false,
                heartRateBandEnabled = false,
                eyeTrackingEnabled = false,

                task1NumberOfBlocks = 4,
                task1TrialsPerBlock = 40,
                task1BreakDurationSeconds = 30f,
                task1NarrowOffsetCm = 5f,
                task1WideOffsetCm = 15f,
                task1LoomingSpeeds = new[] { "slow", "fast" },
                task1PracticeVtOnlyTrials = 2,
                task1PracticeVtVisualTrials = 4,

                task2NumberOfBlocks = 4,
                task2TrialsPerBlock = 40,
                task2BreakDurationSeconds = 30f,
                task2HitOffsetCm = 0f,
                task2NearMissOffsetCm = 5f,
                task2MissOffsetCm = 15f,
                task2BallSpeeds = new[] { "slow", "fast" },

                clinicianNotes = string.Empty,
            };
        }

        /// <summary>
        /// Copy task-config fields off the supplied asset. Called by the
        /// app controller right before <see cref="TaskLogger.BeginSession"/>
        /// so the CSV/JSON capture exactly the protocol that ran.
        /// </summary>
        public void PopulateFromTaskAsset(TrajectoryTaskAsset asset)
        {
            if (asset == null) return;
            task2NumberOfBlocks = asset.BlockCount;
            task2TrialsPerBlock = asset.TrialsPerBlock;
            task2BreakDurationSeconds = asset.BreakDurationSeconds;
            // Offsets from generator are intrinsically per-category;
            // only the rough boundaries are captured here for analysts.
            task2NearMissOffsetCm = 5f;
            task2MissOffsetCm = 15f;
            task2HitOffsetCm = 0f;
            task2BallSpeeds = new[] { "slow", "fast" };
        }

        /// <summary>
        /// CSV's <c>session_type</c> code: med_high / med_low / healthy_s1 / healthy_s2.
        /// </summary>
        public string SessionTypeCode => sessionType switch
        {
            SessionType.MedicationHigh   => "med_high",
            SessionType.MedicationLow    => "med_low",
            SessionType.HealthySession1  => "healthy_s1",
            SessionType.HealthySession2  => "healthy_s2",
            _ => sessionType.ToString().ToLowerInvariant(),
        };

        public string DbsStatusCode => dbsStatus switch
        {
            DbsStatus.On  => "on",
            DbsStatus.Off => "off",
            _             => "na",
        };

        /// <summary>
        /// Renders the metadata as the spec's nested setup.json structure.
        /// Hand-formatted JSON so JsonUtility's flat layout doesn't constrain
        /// the schema. Indentation is 2 spaces.
        /// </summary>
        public string ToSetupJson()
        {
            string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string ArrJson(string[] a)
            {
                if (a == null || a.Length == 0) return "[]";
                var parts = new System.Text.StringBuilder("[");
                for (int i = 0; i < a.Length; i++)
                {
                    if (i > 0) parts.Append(", ");
                    parts.Append('"').Append(Esc(a[i])).Append('"');
                }
                parts.Append(']');
                return parts.ToString();
            }
            string B(bool v) => v ? "true" : "false";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"subject\": {");
            sb.AppendLine($"    \"subject_id\": \"{Esc(participantId)}\",");
            sb.AppendLine($"    \"age_years\": {ageYears},");
            sb.AppendLine($"    \"dominant_hand\": \"{dominantHand.ToString().ToLowerInvariant()}\",");
            sb.AppendLine($"    \"height_cm\": {heightCm},");
            sb.AppendLine($"    \"shoulder_width_cm\": {shoulderWidthCm},");
            sb.AppendLine($"    \"group\": \"{Esc(subjectGroup)}\",");
            sb.AppendLine($"    \"has_DBS\": {B(hasDbs)}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"session\": {");
            sb.AppendLine($"    \"session_number\": {sessionNumber},");
            sb.AppendLine($"    \"session_id\": \"{Esc(sessionId)}\",");
            sb.AppendLine($"    \"session_date\": \"{Esc(sessionDate)}\",");
            sb.AppendLine($"    \"session_type\": \"{SessionTypeCode}\",");
            sb.AppendLine($"    \"dbs_status\": \"{DbsStatusCode}\",");
            sb.AppendLine($"    \"clinician_initials\": \"{Esc(clinicianInitials)}\",");
            sb.AppendLine($"    \"language\": \"{Esc(language)}\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"equipment\": {");
            sb.AppendLine($"    \"EEG\": {B(eegEnabled)},");
            sb.AppendLine($"    \"EMG\": {B(emgEnabled)},");
            sb.AppendLine($"    \"heart_rate_band\": {B(heartRateBandEnabled)},");
            sb.AppendLine($"    \"eye_tracking\": {B(eyeTrackingEnabled)}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"task1_parameters\": {");
            sb.AppendLine($"    \"number_of_blocks\": {task1NumberOfBlocks},");
            sb.AppendLine($"    \"trials_per_block\": {task1TrialsPerBlock},");
            sb.AppendLine($"    \"break_duration_seconds\": {task1BreakDurationSeconds},");
            sb.AppendLine($"    \"narrow_offset_cm\": {task1NarrowOffsetCm},");
            sb.AppendLine($"    \"wide_offset_cm\": {task1WideOffsetCm},");
            sb.AppendLine($"    \"looming_speeds\": {ArrJson(task1LoomingSpeeds)},");
            sb.AppendLine($"    \"practice_vt_only_trials\": {task1PracticeVtOnlyTrials},");
            sb.AppendLine($"    \"practice_vt_visual_trials\": {task1PracticeVtVisualTrials}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"task2_parameters\": {");
            sb.AppendLine($"    \"number_of_blocks\": {task2NumberOfBlocks},");
            sb.AppendLine($"    \"trials_per_block\": {task2TrialsPerBlock},");
            sb.AppendLine($"    \"break_duration_seconds\": {task2BreakDurationSeconds},");
            sb.AppendLine($"    \"hit_offset_cm\": {task2HitOffsetCm},");
            sb.AppendLine($"    \"near_miss_offset_cm\": {task2NearMissOffsetCm},");
            sb.AppendLine($"    \"miss_offset_cm\": {task2MissOffsetCm},");
            sb.AppendLine($"    \"ball_speeds\": {ArrJson(task2BallSpeeds)}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"comments\": {");
            sb.AppendLine($"    \"clinician_notes\": \"{Esc(clinicianNotes)}\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
