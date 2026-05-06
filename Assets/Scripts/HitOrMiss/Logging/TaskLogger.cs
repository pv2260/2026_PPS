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

            m_TrialsCsvPath    = Path.Combine(m_SessionDir, "trials.csv");
            m_EyeCsvPath       = Path.Combine(m_SessionDir, "eyetracking.csv");
            m_MetadataJsonPath = Path.Combine(m_SessionDir, "metadata.json");
            m_FinalJsonPath    = Path.Combine(m_SessionDir, "session.json");

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
            string json = JsonUtility.ToJson(m_Metadata, true);
            File.WriteAllText(m_MetadataJsonPath, json, Encoding.UTF8);
        }

        // CSV column order matches the PDF's 28-column layout exactly.
        static string BuildTrialsHeader() =>
            "participant_id,session_date,session_type,dbs_status," +
            "block_number,trial_number,run_id,trial_in_run,run_length," +
            "trial_since_last_switch,is_switch," +
            "ball_speed,prev_speed,speed_change,abs_speed_change,change_direction," +
            "trajectory_id,trajectory_angle_deg," +
            "start_x,start_y,start_z,end_x,end_y,end_z," +
            "will_hit,button_pressed,participant_response,reaction_time_ms," +
            "trial_start_time,ball_motion_start_time,response_time,inter_trial_interval_ms," +
            "category,result,is_correct,failure_reason";

        string BuildTrialRow(TrialJudgement j)
        {
            var inv = CultureInfo.InvariantCulture;
            string F(double v) => double.IsNaN(v) ? "" : v.ToString("F4", inv);
            string F2(double v) => double.IsNaN(v) ? "" : v.ToString("F2", inv);
            string F6(float v) => v.ToString("F6", inv);
            string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", ";");

            int blockNumber = j.blockIndex + 1;
            int willHit = j.expected == SemanticCommand.Hit ? 1 : 0;
            int isSwitch = j.isSwitchTrial ? 1 : 0;
            int isCorrect = j.isCorrect ? 1 : 0;

            return string.Join(",",
                Esc(m_Metadata.participantId),
                Esc(m_Metadata.sessionDate),
                Esc(m_Metadata.SessionTypeCode),
                Esc(m_Metadata.DbsStatusCode),
                blockNumber.ToString(inv),
                j.trialNumberInBlock.ToString(inv),
                j.runId.ToString(inv),
                j.trialInRun.ToString(inv),
                j.runLength.ToString(inv),
                j.trialsSinceLastSwitch.ToString(inv),
                isSwitch.ToString(inv),
                F2(j.speedMps),
                F2(j.prevSpeedMps),
                F2(j.speedChange),
                F2(j.absSpeedChange),
                Esc(j.ChangeDirectionCode),
                Esc(j.trajectoryId),
                F2(j.trajectoryAngleDeg),
                F6(j.startWorldPosition.x), F6(j.startWorldPosition.y), F6(j.startWorldPosition.z),
                F6(j.endWorldPosition.x),   F6(j.endWorldPosition.y),   F6(j.endWorldPosition.z),
                willHit.ToString(inv),
                Esc(j.ButtonPressedCode),
                Esc(j.ParticipantResponseCode),
                F2(j.reactionTimeMs),
                F(j.trialStartTime),
                F(j.ballMotionStartTime),
                F(j.responseTime),
                F2(j.interTrialIntervalMs),
                j.category.ToString(),
                j.result.ToString(),
                isCorrect.ToString(inv),
                $"\"{Esc(j.failureReason)}\""
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
