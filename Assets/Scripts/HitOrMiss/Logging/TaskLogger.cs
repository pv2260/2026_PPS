using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Writes per-session data to disk in the layout the AR-Task PDF
    /// (29.04.2026 spec) requires:
    ///
    ///   <c>Logs/{participantId}_{sessionId}/</c>
    ///       metadata.json       — session + task-config snapshot
    ///       trials.csv          — one row per trial (full schema)
    ///       eyetracking.csv     — placeholder; populated by EyeTrackingLogger
    ///                             when eye-tracking is wired up
    ///       progress.json       — written on Pause / mid-session flush
    ///       session.json        — final consolidated dump on EndSession
    ///
    /// The CSV header is the canonical 28-column layout from the PDF; every
    /// trial carries the participant + session metadata so each CSV is
    /// self-contained for downstream analysis.
    /// </summary>
    public class TaskLogger : MonoBehaviour
    {
        [SerializeField] string m_ParticipantId = "P000";
        [SerializeField] string m_SessionId = "";

        SessionMetadata m_Metadata;
        bool m_MetadataExplicitlySet;

        string m_SessionDir;
        string m_TrialsCsvPath;
        string m_EyeCsvPath;
        string m_MetadataJsonPath;
        string m_FinalJsonPath;
        StreamWriter m_TrialsWriter;
        readonly List<TrialJudgement> m_Judgements = new();
        bool m_SessionOpen;

        public string ParticipantId
        {
            get => m_ParticipantId;
            set => m_ParticipantId = value;
        }

        /// <summary>Read-only view of the active session folder. Empty before BeginSession.</summary>
        public string SessionDirectory => m_SessionDir;

        /// <summary>
        /// Supplies the session metadata that will be written to
        /// <c>metadata.json</c> and stamped into each trial row. Must be called
        /// before <see cref="BeginSession"/>; otherwise a default metadata
        /// record (using the current participant id) is used.
        /// </summary>
        public void SetMetadata(SessionMetadata metadata)
        {
            m_Metadata = metadata;
            m_MetadataExplicitlySet = true;

            if (!string.IsNullOrEmpty(metadata.participantId))
                m_ParticipantId = metadata.participantId;
            if (!string.IsNullOrEmpty(metadata.sessionId))
                m_SessionId = metadata.sessionId;
        }

        public void BeginSession(string taskName)
        {
            if (string.IsNullOrEmpty(m_SessionId))
                m_SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Per-session folder. Keeps trials.csv, eyetracking.csv, and
            // metadata.json siblings so the analyst can drop the whole
            // directory into their pipeline.
            string root = Path.Combine(Application.persistentDataPath, "Logs");
            m_SessionDir = Path.Combine(root, $"{m_ParticipantId}_{m_SessionId}");
            Directory.CreateDirectory(m_SessionDir);

            // File names follow the spec naming:
            //   sub-{id}_session-{n}_task2_trials.csv
            //   sub-{id}_session-{n}_setup.json   (nested, spec-compliant)
            //   metadata.json                      (flat, server roundtrip)
            string idSlug = m_ParticipantId.Replace(" ", "_");
            int sn = m_Metadata.sessionNumber > 0 ? m_Metadata.sessionNumber : 1;
            m_TrialsCsvPath    = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_task2_trials.csv");
            m_EyeCsvPath       = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_task2_eyetracking.csv");
            m_MetadataJsonPath = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_setup.json");
            m_FinalJsonPath    = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_task2_session.json");

            EnsureMetadataDefaults(taskName);
            WriteMetadataJson();

            m_TrialsWriter = new StreamWriter(m_TrialsCsvPath, false, Encoding.UTF8);
            m_TrialsWriter.WriteLine(BuildTrialsHeader());
            m_TrialsWriter.Flush();

            // Eye-tracking CSV is stubbed for now: header only. The
            // EyeTrackingLogger (Phase B follow-up) will append rows.
            using (var eye = new StreamWriter(m_EyeCsvPath, false, Encoding.UTF8))
            {
                eye.WriteLine("trial_id,block_number,timestamp,gaze_origin_x,gaze_origin_y,gaze_origin_z,gaze_dir_x,gaze_dir_y,gaze_dir_z,left_pupil_diam_mm,right_pupil_diam_mm");
            }

            m_Judgements.Clear();
            m_SessionOpen = true;

            Debug.Log($"[TaskLogger] Session started. Folder: {m_SessionDir}");
        }

        public void LogTrial(TrialJudgement j)
        {
            if (!m_SessionOpen) return;

            // Practice trials are not stored. The PDF spec ("After pressing
            // START PRACTICE: NONE OF THESE RESPONSES NEED TO BE STORED")
            // requires this. Practice trials carry blockIndex == -1 and
            // trialId starting with "PRACTICE_".
            if (j.blockIndex < 0 || (j.trialId != null && j.trialId.StartsWith("PRACTICE_")))
                return;

            m_Judgements.Add(j);
            m_TrialsWriter.WriteLine(BuildTrialRow(j));
            m_TrialsWriter.Flush();
        }

        public void EndSession()
        {
            if (!m_SessionOpen) return;

            m_TrialsWriter?.Close();
            m_TrialsWriter = null;

            string json = JsonUtility.ToJson(new SessionLog
            {
                metadata = m_Metadata,
                timestamp = DateTime.Now.ToString("o"),
                totalTrials = m_Judgements.Count,
                judgements = m_Judgements.ToArray(),
            }, true);
            File.WriteAllText(m_FinalJsonPath, json, Encoding.UTF8);

            m_SessionOpen = false;
            Debug.Log($"[TaskLogger] Session ended. Logs saved to {m_SessionDir}");
        }

        /// <summary>
        /// Flushes the trials CSV to disk and writes a small progress
        /// snapshot so a paused or interrupted session can be inspected or
        /// resumed later. Called from
        /// <see cref="HitOrMissAppController.PauseSession"/>.
        /// </summary>
        public void Flush(int currentBlockIndex, int nextTrialIndex)
        {
            if (!m_SessionOpen) return;

            m_TrialsWriter?.Flush();

            string progressPath = Path.Combine(m_SessionDir, "progress.json");
            string json = JsonUtility.ToJson(new ProgressSnapshot
            {
                participantId = m_ParticipantId,
                sessionId = m_SessionId,
                timestamp = DateTime.Now.ToString("o"),
                currentBlockIndex = currentBlockIndex,
                nextTrialIndex = nextTrialIndex,
                trialsCompleted = m_Judgements.Count,
            }, true);
            File.WriteAllText(progressPath, json, Encoding.UTF8);

            Debug.Log($"[TaskLogger] Flushed. Progress snapshot: {progressPath}");
        }

        void OnDestroy()
        {
            if (m_SessionOpen)
                EndSession();
        }

        // ---- Helpers ----

        void EnsureMetadataDefaults(string taskName)
        {
            if (!m_MetadataExplicitlySet)
                m_Metadata = SessionMetadata.CreateDefault(m_ParticipantId);

            if (string.IsNullOrEmpty(m_Metadata.participantId))
                m_Metadata.participantId = m_ParticipantId;
            if (string.IsNullOrEmpty(m_Metadata.sessionId))
                m_Metadata.sessionId = m_SessionId;
            if (string.IsNullOrEmpty(m_Metadata.sessionDate))
                m_Metadata.sessionDate = DateTime.Now.ToString("yyyy-MM-dd");
        }

        void WriteMetadataJson()
        {
            // setup.json: spec-compliant nested format for the analysis pipeline.
            File.WriteAllText(m_MetadataJsonPath, m_Metadata.ToSetupJson(), Encoding.UTF8);
            // metadata.json: flat JsonUtility format for server-side reload
            // (sessions browser uses this to read back the participant id /
            // session date when listing past sessions).
            string flatPath = Path.Combine(m_SessionDir, "metadata.json");
            File.WriteAllText(flatPath, JsonUtility.ToJson(m_Metadata, true), Encoding.UTF8);
        }

        // CSV column order matches the spec's Task 2 trials.csv layout
        // (TASK 2 — HIT OR MISS TASK LOGIC.txt).
        static string BuildTrialsHeader() =>
            "subject_id,session_number,block_number,trial_number," +
            "trial_type,correct_response," +
            "previous_speed,current_speed," +
            "speed_sequence,transition_status," +
            "trajectory_offset_cm," +
            "trial_trigger_code,response_trigger_code,trigger_timestamp," +
            "participant_response,reaction_time_ms,accuracy," +
            "trial_interrupted,timestamp";

        string BuildTrialRow(TrialJudgement j)
        {
            var inv = CultureInfo.InvariantCulture;
            string F(double v) => double.IsNaN(v) ? "" : v.ToString("F4", inv);
            string F2(double v) => double.IsNaN(v) ? "" : v.ToString("F2", inv);
            string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", ";");

            int blockNumber = j.blockIndex + 1;
            int accuracy = j.isCorrect ? 1 : 0;
            int interrupted = j.trialInterrupted ? 1 : 0;
            // Trajectory offset in cm (signed) — multiply lateral offset (m)
            // by 100 and sign-flip so "negative = inside body" per spec example.
            float trajectoryOffsetCm = -j.lateralOffsetMeters * 100f;
            string prevSpeed = j.hasPreviousSpeed ? j.previousSpeedLevel.ToCode() : "none";
            string timestamp = System.DateTime.Now.ToString("o");

            return string.Join(",",
                Esc(m_Metadata.participantId),
                m_Metadata.sessionNumber.ToString(inv),
                blockNumber.ToString(inv),
                j.trialNumberInBlock.ToString(inv),
                Esc(j.category.ToCode()),
                Esc(j.CorrectResponseCode),
                Esc(prevSpeed),
                Esc(j.currentSpeedLevel.ToCode()),
                Esc(j.SpeedSequenceCode),
                Esc(j.transitionStatus.ToCode()),
                F2(trajectoryOffsetCm),
                j.trialTriggerCode.ToString(inv),
                j.responseTriggerCode.ToString(inv),
                F(j.triggerTimestamp),
                Esc(j.ParticipantResponseSpecCode),
                F2(j.reactionTimeMs),
                accuracy.ToString(inv),
                interrupted.ToString(inv),
                Esc(timestamp)
            );
        }

        [Serializable]
        struct SessionLog
        {
            public SessionMetadata metadata;
            public string timestamp;
            public int totalTrials;
            public TrialJudgement[] judgements;
        }

        [Serializable]
        struct ProgressSnapshot
        {
            public string participantId;
            public string sessionId;
            public string timestamp;
            public int currentBlockIndex;
            public int nextTrialIndex;
            public int trialsCompleted;
        }
    }
}
