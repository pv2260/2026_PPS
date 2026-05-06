using System;

namespace HitOrMiss
{
    /// <summary>
    /// Per-session metadata captured by the clinician on the New Session form.
    /// Serialized as <c>metadata.json</c> alongside the trial CSV in each
    /// session folder. Also embedded into every trial row via the columns
    /// the PDF spec requires (participant_id, session_date, session_type,
    /// dbs_status, etc.) so each CSV is self-contained.
    /// </summary>
    [Serializable]
    public struct SessionMetadata
    {
        // ---- Identity ----
        public string participantId;
        public string clinicianInitials;
        public string sessionDate;     // ISO 8601 date (yyyy-MM-dd)
        public string sessionId;       // monotonic session timestamp (yyyyMMdd_HHmmss)
        public int sessionNumber;      // 1, 2, 3 … (re-runs after a crash get the next index)

        // ---- Participant ----
        public int ageYears;
        public DominantHand dominantHand;
        public float heightCm;
        public float shoulderWidthCm;  // drives Hit/Miss lateral offsets — see Phase E

        // ---- Condition ----
        public SessionType sessionType;
        public DbsStatus dbsStatus;

        // ---- Task config snapshot (mirrors TrajectoryTaskAsset at start of session) ----
        public int blockCount;
        public int trialsPerBlock;
        public float itiMinSeconds;
        public float itiMaxSeconds;
        public float breakDurationSec;
        public float fastSpeedMps;
        public float slowSpeedMps;
        public float spawnDistanceM;
        public float ballDiameterM;

        // ---- Free-form ----
        public string notes;

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
                sessionType = SessionType.HealthySession1,
                dbsStatus = DbsStatus.Na,
                blockCount = 0,
                trialsPerBlock = 0,
                itiMinSeconds = 0f,
                itiMaxSeconds = 0f,
                breakDurationSec = 0f,
                fastSpeedMps = 0f,
                slowSpeedMps = 0f,
                spawnDistanceM = 0f,
                ballDiameterM = 0f,
                notes = string.Empty,
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
            blockCount = asset.BlockCount;
            trialsPerBlock = asset.TrialsPerBlock;
            itiMinSeconds = asset.ItiMinSeconds;
            itiMaxSeconds = asset.ItiMaxSeconds;
            breakDurationSec = asset.RestDuration;
            fastSpeedMps = asset.FastSpeed;
            slowSpeedMps = asset.SlowSpeed;
            spawnDistanceM = asset.SpawnDistance;
            ballDiameterM = asset.BallDiameter;
        }

        /// <summary>
        /// String form used in the CSV's <c>session_type</c> column. Matches
        /// the PDF spec: <c>med_high</c>, <c>med_low</c>, <c>healthy_s1</c>,
        /// <c>healthy_s2</c>.
        /// </summary>
        public string SessionTypeCode => sessionType switch
        {
            SessionType.MedicationHigh   => "med_high",
            SessionType.MedicationLow    => "med_low",
            SessionType.HealthySession1  => "healthy_s1",
            SessionType.HealthySession2  => "healthy_s2",
            _ => sessionType.ToString().ToLowerInvariant(),
        };

        /// <summary>
        /// String form used in the CSV's <c>dbs_status</c> column.
        /// </summary>
        public string DbsStatusCode => dbsStatus switch
        {
            DbsStatus.On  => "on",
            DbsStatus.Off => "off",
            _             => "na",
        };
    }
}
