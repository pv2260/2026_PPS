using System;

namespace HitOrMiss
{
    /// <summary>
    /// Which task a session belongs to. Drives file naming
    /// (sub-{id}_session-{n}_task1_* vs task2_*) and which parameter block
    /// is included in setup.json so each task's recorded parameters stay
    /// isolated from changes made to the other task afterwards.
    /// </summary>
    public enum TaskKind
    {
        Task1Pps,
        Task2HitOrMiss,
    }

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
        public int task1TrialsPerBlock;            // Derived: VT + V + T
        public int task1VtTrialsPerBlock;          // Visuotactile (loom + vibration)
        public int task1VisualOnlyTrialsPerBlock;  // Loom only, no vibration
        public int task1TactileOnlyTrialsPerBlock; // Vibration only, no loom
        public float task1BreakDurationSeconds;
        // Loom velocities in m/s. PpsTaskAsset stores speed natively, so these
        // are applied as typed. 0 = use the asset value.
        public float task1FastSpeedMps;
        public float task1SlowSpeedMps;
        // Crosshair height above the body anchor, in meters — the participant's
        // eye level. 0 = use the asset value.
        public float task1CrosshairHeightM;
        // True when PpsTaskAsset.UseWidthFactor is on, i.e. narrow AND wide LED
        // separations were both presented. When false only one width ran and
        // task1WideOffsetCm is meaningless, so setup.json writes it as null.
        public bool task1WidthFactor;
        // Lateral LED separation on narrow trials, in cm. This is the
        // participant's shoulder width (floored at the asset default), not an
        // offset from anything. Replaces the old task1NarrowOffsetCm, which was
        // hardcoded to 0 and therefore carried no information.
        public float task1NarrowSeparationCm;
        public float task1WideOffsetCm;
        public string[] task1LoomingSpeeds;       // {"slow","medium","fast"} subset
        public int task1PracticeVtOnlyTrials;
        public int task1PracticeVtVisualTrials;

        // Task config snapshot — Task 2 (Hit-or-Miss)
        public int task2NumberOfBlocks;
        public int task2TrialsPerBlock;            // Derived: the five category counts
        public int task2ClearHitTrialsPerBlock;
        public int task2NearHitTrialsPerBlock;
        public int task2NearMissTrialsPerBlock;
        public int task2ClearMissTrialsPerBlock;
        public int task2GreyZoneTrialsPerBlock;
        public float task2BreakDurationSeconds;
        // Ball velocities in m/s. 0 = use the asset value.
        public float task2FastSpeed;
        public float task2SlowSpeed;
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
                sessionId = string.Empty,
                sessionDate = DateTime.Now.ToString("yyyy-MM-dd"),
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
                task1VtTrialsPerBlock = 28,
                task1VisualOnlyTrialsPerBlock = 6,
                task1TactileOnlyTrialsPerBlock = 6,
                task1BreakDurationSeconds = 30f,
                // 0 = defer to the task asset. The panel leaves these blank for a
                // protocol run and only fills them to slow a participant down.
                task1FastSpeedMps = 0f,
                task1SlowSpeedMps = 0f,
                task1CrosshairHeightM = 0f,
                task1WidthFactor = false,
                task1NarrowSeparationCm = 42f,
                task1WideOffsetCm = 30f,
                task1LoomingSpeeds = new[] { "slow", "fast" },
                task1PracticeVtOnlyTrials = 2,
                task1PracticeVtVisualTrials = 4,

                task2NumberOfBlocks = 4,
                task2TrialsPerBlock = 40,
                task2ClearHitTrialsPerBlock = 0,
                task2NearHitTrialsPerBlock = 0,
                task2NearMissTrialsPerBlock = 0,
                task2ClearMissTrialsPerBlock = 0,
                task2GreyZoneTrialsPerBlock = 0,
                task2BreakDurationSeconds = 30f,
                task2FastSpeed = 0f,
                task2SlowSpeed = 0f,
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
            task2ClearHitTrialsPerBlock = asset.ClearHitTrialsPerBlock;
            task2NearHitTrialsPerBlock = asset.NearHitTrialsPerBlock;
            task2NearMissTrialsPerBlock = asset.NearMissTrialsPerBlock;
            task2ClearMissTrialsPerBlock = asset.ClearMissTrialsPerBlock;
            task2GreyZoneTrialsPerBlock = asset.GreyZoneTrialsPerBlock;
            task2BreakDurationSeconds = asset.BreakDurationSeconds;
            // Record the speeds that actually ran, not the panel request.
            task2FastSpeed = asset.FastSpeed;
            task2SlowSpeed = asset.SlowSpeed;
            // Offsets from generator are intrinsically per-category;
            // only the rough boundaries are captured here for analysts.
            task2NearMissOffsetCm = 5f;
            task2MissOffsetCm = 15f;
            task2HitOffsetCm = 0f;
            task2BallSpeeds = new[] { "slow", "fast" };
        }

        /// <summary>
        /// Copy Task 1 (PPS) config fields off the supplied asset, so
        /// setup.json reflects the actual block count, ITI, speeds, and
        /// LED separation that ran during the session.
        /// </summary>
        public void PopulateFromPpsTaskAsset(HitOrMiss.Pps.PpsTaskAsset asset)
        {
            if (asset == null) return;
            task1NumberOfBlocks = asset.BlockCount;
            task1TrialsPerBlock = asset.TrialsPerBlock;
            task1VtTrialsPerBlock = asset.VtTrialsPerBlock;
            task1VisualOnlyTrialsPerBlock = asset.VisualOnlyTrialsPerBlock;
            task1TactileOnlyTrialsPerBlock = asset.TactileOnlyTrialsPerBlock;
            task1BreakDurationSeconds = asset.RestDurationSeconds;
            // Record what actually ran. Speed is the asset's native unit now.
            task1FastSpeedMps = asset.FastSpeedMps;
            task1SlowSpeedMps = asset.SlowSpeedMps;
            task1CrosshairHeightM = asset.CrosshairHeight;

            // Width factor. When off, every trial ran at DefaultWidth and the wide
            // offset was never applied, so setup.json must say so rather than
            // archiving a number that did not shape the session.
            task1WidthFactor = asset.UseWidthFactor;

            // Narrow separation is the participant's shoulder width, floored at the
            // asset default. Resolved through SeparationFor so this record and the
            // renderer can never disagree about what the LEDs actually did.
            task1NarrowSeparationCm =
                asset.SeparationFor(HitOrMiss.Pps.PpsWidth.Narrow, shoulderWidthCm / 100f) * 100f;

            task1WideOffsetCm = asset.WideOffsetMeters * 100f;
            task1LoomingSpeeds = new[] { "slow", "fast" };
            task1PracticeVtOnlyTrials = 2;
            task1PracticeVtVisualTrials = 4;
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
    
        
        // /// <summary>
        // /// Renders the metadata as the spec's nested setup.json structure for
        // /// the given task. Only the active task's parameter block is included
        // /// so a setup.json written by a Task 1 session never embeds Task 2
        // /// settings (and vice versa). That isolation is what prevents later
        // /// edits to one task's defaults from appearing in the other task's
        // /// historical session records.
        // /// </summary>
        public string ToSetupJson(TaskKind activeTask)
        {
            bool includeTask1 = activeTask == TaskKind.Task1Pps;
            bool includeTask2 = activeTask == TaskKind.Task2HitOrMiss;
            return BuildSetupJson(includeTask1, includeTask2);
        }

        /// <summary>
        /// Back-compat overload. Writes both task1_parameters and
        /// task2_parameters blocks; use the <see cref="ToSetupJson(TaskKind)"/>
        /// overload to keep per-task data isolated.
        /// </summary>
        public string ToSetupJson() => BuildSetupJson(true, true);

        string BuildSetupJson(bool includeTask1, bool includeTask2)
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
            // Floats MUST be written with the invariant culture. String
            // interpolation uses the current culture, which on a French or German
            // Swiss machine emits "1,5" and silently produces invalid JSON.
            string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Writes null instead of a value when the parameter did not apply to
            // this session. A stale number is worse than an explicit null: it reads
            // as a setting that shaped the run when it did not.
            string FOrNull(float v, bool applies) => applies ? F(v) : "null";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"subject\": {");
            sb.AppendLine($"    \"subject_id\": \"{Esc(participantId)}\",");
            sb.AppendLine($"    \"age_years\": {ageYears},");
            sb.AppendLine($"    \"dominant_hand\": \"{dominantHand.ToString().ToLowerInvariant()}\",");
            sb.AppendLine($"    \"height_cm\": {F(heightCm)},");
            sb.AppendLine($"    \"shoulder_width_cm\": {F(shoulderWidthCm)},");
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

            if (includeTask1)
            {
                sb.AppendLine("  \"task1_parameters\": {");
                sb.AppendLine($"    \"number_of_blocks\": {task1NumberOfBlocks},");
                sb.AppendLine($"    \"trials_per_block\": {task1TrialsPerBlock},");

                sb.AppendLine($"    \"vt_trials_per_block\": {task1VtTrialsPerBlock},");
                sb.AppendLine($"    \"visual_only_trials_per_block\": {task1VisualOnlyTrialsPerBlock},");
                sb.AppendLine($"    \"tactile_only_trials_per_block\": {task1TactileOnlyTrialsPerBlock},");
                sb.AppendLine($"    \"break_duration_seconds\": {F(task1BreakDurationSeconds)},");
                sb.AppendLine($"    \"loom_fast_speed_mps\": {F(task1FastSpeedMps)},");
                sb.AppendLine($"    \"loom_slow_speed_mps\": {F(task1SlowSpeedMps)},");
                sb.AppendLine($"    \"crosshair_height_m\": {F(task1CrosshairHeightM)},");
                string[] widthLevels = task1WidthFactor
                    ? new[] { "narrow", "wide" }
                    : new[] { "narrow" };

                sb.AppendLine($"    \"width_factor\": {B(task1WidthFactor)},");
                sb.AppendLine($"    \"width_levels\": {ArrJson(widthLevels)},");
                sb.AppendLine($"    \"narrow_separation_cm\": {F(task1NarrowSeparationCm)},");
                sb.AppendLine($"    \"wide_offset_cm\": {FOrNull(task1WideOffsetCm, task1WidthFactor)},");
                sb.AppendLine($"    \"looming_speeds\": {ArrJson(task1LoomingSpeeds)},");
                sb.AppendLine($"    \"practice_vt_only_trials\": {task1PracticeVtOnlyTrials},");
                sb.AppendLine($"    \"practice_vt_visual_trials\": {task1PracticeVtVisualTrials}");
                sb.AppendLine("  },");
            }
            if (includeTask2)
            {
                sb.AppendLine("  \"task2_parameters\": {");
                sb.AppendLine($"    \"number_of_blocks\": {task2NumberOfBlocks},");
                sb.AppendLine($"    \"trials_per_block\": {task2TrialsPerBlock},");
                sb.AppendLine($"    \"clear_hit_trials_per_block\": {task2ClearHitTrialsPerBlock},");
                sb.AppendLine($"    \"near_hit_trials_per_block\": {task2NearHitTrialsPerBlock},");
                sb.AppendLine($"    \"near_miss_trials_per_block\": {task2NearMissTrialsPerBlock},");
                sb.AppendLine($"    \"clear_miss_trials_per_block\": {task2ClearMissTrialsPerBlock},");
                sb.AppendLine($"    \"grey_zone_trials_per_block\": {task2GreyZoneTrialsPerBlock},");
                sb.AppendLine($"    \"break_duration_seconds\": {F(task2BreakDurationSeconds)},");
                sb.AppendLine($"    \"ball_fast_speed_mps\": {F(task2FastSpeed)},");
                sb.AppendLine($"    \"ball_slow_speed_mps\": {F(task2SlowSpeed)},");
                sb.AppendLine($"    \"hit_offset_cm\": {F(task2HitOffsetCm)},");
                sb.AppendLine($"    \"near_miss_offset_cm\": {F(task2NearMissOffsetCm)},");
                sb.AppendLine($"    \"miss_offset_cm\": {F(task2MissOffsetCm)},");
                sb.AppendLine($"    \"ball_speeds\": {ArrJson(task2BallSpeeds)}");
                sb.AppendLine("  },");
            }

            sb.AppendLine("  \"comments\": {");
            sb.AppendLine($"    \"clinician_notes\": \"{Esc(clinicianNotes)}\"");
            sb.AppendLine("  }");

            sb.AppendLine("}");

            return sb.ToString();
        }

        // /// <summary>
        // /// Backward-compatible fallback.
        // ///
        // /// NOTE:
        // /// Prefer calling ToSetupJson(taskName) from TaskLogger so setup.json
        // /// contains only the parameters for the launched task.
        // /// </summary>
        // public string ToSetupJson()
        // {
        //     return ToSetupJson("");
        // }
    }
}