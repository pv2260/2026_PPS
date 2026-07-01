using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Single per-session data writer for both Task 1 (PPS) and Task 2
    /// (Hit-or-Miss). Selects file names, trials schema, and setup.json
    /// content from the <see cref="TaskKind"/> passed to BeginSession.
    ///
    /// Layout per session, under
    /// <c>{cwd}/Logger/{participantId}_{sessionId}/</c>:
    ///
    ///   metadata.json                                  — flat snapshot for server reloads
    ///   sub-{id}_session-{n}_setup.json                — nested spec; only active task's params
    ///   sub-{id}_session-{n}_task{1|2}_trials.csv      — per-task schema
    ///   sub-{id}_session-{n}_task{1|2}_eyetracking.csv — header-only stub
    ///   sub-{id}_session-{n}_task{1|2}_session.json    — final consolidated dump
    ///   progress.json                                  — written on Pause / mid-session Flush
    /// </summary>
    public class TaskLogger : MonoBehaviour
    {
        /// Pam added for logging system
        [Header("Shared Session Folder")]
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;
        string m_ParticipantId;
        string m_SessionId;
        ///


        SessionMetadata m_Metadata;
        bool m_MetadataExplicitlySet;

        TaskKind m_TaskKind = TaskKind.Task2HitOrMiss;

        string m_SessionDir;
        string m_TrialsCsvPath;
        string m_EyeCsvPath;
        string m_SetupJsonPath;
        string m_FinalJsonPath;

        StreamWriter m_TrialsWriter;

        // Each task has its own in-memory trial list because the schemas
        // and final session.json structures differ.
        readonly List<TrialJudgement> m_Task2Judgements = new();
        readonly List<HitOrMiss.Pps.PpsTrialResult> m_Task1Results = new();
        bool m_SessionOpen;

        public string ParticipantId
        {
            get => m_ParticipantId;
            set => m_ParticipantId = value;
        }

        /// <summary>Read-only view of the active session folder. Empty before BeginSession.</summary>
        public string SessionDirectory => m_SessionDir;

        /// <summary>Which task this logger is currently recording. Set by BeginSession.</summary>
        public TaskKind ActiveTaskKind => m_TaskKind;

        /// <summary>True between BeginSession and EndSession. Lets app
        /// controllers expose a real "currently recording" signal to the
        /// clinician UI instead of inferring it from session state.</summary>
        public bool IsSessionOpen => m_SessionOpen;

        /// <summary>
        /// Supplies the session metadata that will be written to
        /// <c>metadata.json</c> and stamped into each trial row. Must be called
        /// before <see cref="BeginSession(TaskKind, string)"/>; otherwise a
        /// default metadata record (using the current participant id) is used.
        /// </summary>
        public void SetMetadata(SessionMetadata metadata)
        {
            m_Metadata = metadata;
            m_MetadataExplicitlySet = true;

        }

        /// <summary>
        /// Back-compat overload. Defaults to Task 2 so existing Task 2 callers
        /// still work. New code should call the TaskKind overload explicitly.
        /// </summary>
        public void BeginSession(string taskName)
            => BeginSession(TaskKind.Task2HitOrMiss, taskName);

        /// <summary>
        /// Opens a new session folder and prepares writers for the given task.
        /// File names and the setup.json contents adapt so a Task 1 session
        /// never contains Task 2 parameters (and vice versa).
        /// </summary>
        public void BeginSession(TaskKind taskKind, string taskName)
        {
            if (m_SessionOpen)
            {
                Debug.LogWarning("[TaskLogger] BeginSession called while a session was still open. Closing previous session first.");
                EndSession();
            }

            m_TaskKind = taskKind;

           // m_SessionDir = Path.Combine(
           //     Directory.GetCurrentDirectory(),
          //      "Logger",
           //     $"{m_ParticipantId}_{m_SessionId}"
           // );
           // Directory.CreateDirectory(m_SessionDir);

            // Pam added for new logger system
            if (m_EegMarkerEmitter == null)
            {
                Debug.LogError("[TaskLogger] Missing EegMarkerEmitter reference. Cannot use shared session folder.");
                return;
            }

            if (string.IsNullOrEmpty(m_EegMarkerEmitter.SessionDirectory))
            {
                Debug.LogError("[TaskLogger] EegMarkerEmitter has not created a session directory yet. Call EegMarkerEmitter.BeginSession(...) before TaskLogger.BeginSession(...).");
                return;
            }

            m_ParticipantId = m_EegMarkerEmitter.ParticipantId;
            m_SessionId = m_EegMarkerEmitter.SessionId;
            m_SessionDir = m_EegMarkerEmitter.SessionDirectory;
            //

            string idSlug = m_ParticipantId.Replace(" ", "_");
            int sn = m_Metadata.sessionNumber > 0 ? m_Metadata.sessionNumber : 1;
            string taskSlug = TaskFileSlug(taskKind);

            m_TrialsCsvPath = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_{taskSlug}_trials.csv");
            m_EyeCsvPath    = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_{taskSlug}_eyetracking.csv");
            m_SetupJsonPath = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_setup.json");
            m_FinalJsonPath = Path.Combine(m_SessionDir, $"sub-{idSlug}_session-{sn}_{taskSlug}_session.json");

            EnsureMetadataDefaults(taskName);
            WriteMetadataJson();

            m_TrialsWriter = new StreamWriter(m_TrialsCsvPath, false, Encoding.UTF8);
            m_TrialsWriter.WriteLine(TrialsHeaderFor(taskKind));
            m_TrialsWriter.Flush();

            // Eye-tracking CSV is stubbed (header only) for now so the
            // EyeTrackingLogger has a known sibling file to append to once
            // wired up. Same header is fine for both tasks.
            using (var eye = new StreamWriter(m_EyeCsvPath, false, Encoding.UTF8))
            {
                eye.WriteLine("trial_id,block_number,timestamp,gaze_origin_x,gaze_origin_y,gaze_origin_z,gaze_dir_x,gaze_dir_y,gaze_dir_z,left_pupil_diam_mm,right_pupil_diam_mm");
            }

            m_Task1Results.Clear();
            m_Task2Judgements.Clear();
            m_SessionOpen = true;

            Debug.Log($"[TaskLogger] Session started ({taskKind}). Folder: {m_SessionDir}");
        }

        /// <summary>Task 2 trial row. Skips practice trials.</summary>
        public void LogTrial(TrialJudgement j)
        {
            if (!m_SessionOpen) return;
            if (m_TaskKind != TaskKind.Task2HitOrMiss)
            {
                Debug.LogWarning("[TaskLogger] LogTrial(TrialJudgement) called during a Task 1 session. Ignored.");
                return;
            }

            // Practice trials are not stored per spec. Practice carries
            // blockIndex == -1 or trialId starting with "PRACTICE_".
            if (j.blockIndex < 0 || (j.trialId != null && j.trialId.StartsWith("PRACTICE_")))
                return;

            m_Task2Judgements.Add(j);
            m_TrialsWriter.WriteLine(BuildTask2Row(j));
            m_TrialsWriter.Flush();
        }

        /// <summary>Task 1 trial row. Skips practice trials.</summary>
        public void LogTrial(HitOrMiss.Pps.PpsTrialResult result)
        {
            if (m_TaskKind != TaskKind.Task1Pps)
            {
                Debug.LogWarning($"TaskLogger received Task1/PPS result while configured for {m_TaskKind}");
                return;
            }

            m_Task1Results.Add(result);

            m_TrialsWriter.WriteLine(
                result.ToCsvRow(m_Metadata.participantId, m_Metadata.sessionNumber)
            );

            m_TrialsWriter.Flush();
        }

        public void EndSession()
        {
            if (!m_SessionOpen) return;

            m_TrialsWriter?.Close();
            m_TrialsWriter = null;

            string json = m_TaskKind == TaskKind.Task1Pps
                ? JsonUtility.ToJson(new Task1SessionLog
                {
                    metadata    = m_Metadata,
                    timestamp   = DateTime.Now.ToString("o"),
                    totalTrials = m_Task1Results.Count,
                    results     = m_Task1Results.ToArray(),
                }, true)
                : JsonUtility.ToJson(new Task2SessionLog
                {
                    metadata    = m_Metadata,
                    timestamp   = DateTime.Now.ToString("o"),
                    totalTrials = m_Task2Judgements.Count,
                    judgements  = m_Task2Judgements.ToArray(),
                }, true);

            File.WriteAllText(m_FinalJsonPath, json, Encoding.UTF8);

            m_SessionOpen = false;
            Debug.Log($"[TaskLogger] Session ended. Logs saved to {m_SessionDir}");
        }

        /// <summary>
        /// Flushes the trials CSV and writes a small progress snapshot so a
        /// paused or interrupted session can be inspected or resumed later.
        /// Called by the app controller on pause.
        /// </summary>
        public void Flush(int currentBlockIndex, int nextTrialIndex)
        {
            if (!m_SessionOpen) return;

            m_TrialsWriter?.Flush();

            int trialsCompleted = m_TaskKind == TaskKind.Task1Pps
                ? m_Task1Results.Count
                : m_Task2Judgements.Count;

            string progressPath = Path.Combine(m_SessionDir, "progress.json");
            string json = JsonUtility.ToJson(new ProgressSnapshot
            {
                participantId     = m_ParticipantId,
                sessionId         = m_SessionId,
                taskKind          = m_TaskKind.ToString(),
                timestamp         = DateTime.Now.ToString("o"),
                currentBlockIndex = currentBlockIndex,
                nextTrialIndex    = nextTrialIndex,
                trialsCompleted   = trialsCompleted,
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

        static string TaskFileSlug(TaskKind kind) => kind switch
        {
            TaskKind.Task1Pps        => "task1",
            TaskKind.Task2HitOrMiss  => "task2",
            _                        => "task",
        };

        static string TrialsHeaderFor(TaskKind kind) => kind switch
        {
            TaskKind.Task1Pps        => HitOrMiss.Pps.PpsTrialResult.CsvHeader,
            TaskKind.Task2HitOrMiss  => BuildTask2Header(),
            _                        => "",
        };

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
            // setup.json: only the active task's parameter block is included
            // so Task 1 sessions don't carry Task 2 settings (and vice versa).
            File.WriteAllText(m_SetupJsonPath, m_Metadata.ToSetupJson(m_TaskKind), Encoding.UTF8);
            // metadata.json: flat JsonUtility format for server-side reload
            // (sessions browser uses this to read back participant id /
            // session date when listing past sessions).
            string flatPath = Path.Combine(m_SessionDir, "metadata.json");
            File.WriteAllText(flatPath, JsonUtility.ToJson(m_Metadata, true), Encoding.UTF8);
        }

        // ---- Task 2 row + header ----

        // CSV column order matches the spec's Task 2 trials.csv layout
        // (TASK 2 — HIT OR MISS TASK LOGIC.txt).
        static string BuildTask2Header() =>
            "subject_id,session_number,block_number,trial_number," +
            "trial_type,correct_response," +
            "previous_speed,current_speed," +
            "speed_sequence,transition_status," +
            "trajectory_offset_cm," +
            "trial_trigger_code,response_trigger_code,trigger_timestamp," +
            "participant_response,reaction_time_ms,accuracy," +
            "trial_interrupted,timestamp";

        string BuildTask2Row(TrialJudgement j)
        {
            var inv = CultureInfo.InvariantCulture;
            string F(double v) => double.IsNaN(v) ? "" : v.ToString("F4", inv);
            string F2(double v) => double.IsNaN(v) ? "" : v.ToString("F2", inv);
            string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", ";");

            int blockNumber = j.blockIndex + 1;
            int accuracy = j.isCorrect ? 1 : 0;
            int interrupted = j.trialInterrupted ? 1 : 0;
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

        // ---- Session.json structs ----

        [Serializable]
        struct Task2SessionLog
        {
            public SessionMetadata metadata;
            public string timestamp;
            public int totalTrials;
            public TrialJudgement[] judgements;
        }

        [Serializable]
        struct Task1SessionLog
        {
            public SessionMetadata metadata;
            public string timestamp;
            public int totalTrials;
            public HitOrMiss.Pps.PpsTrialResult[] results;
        }

        [Serializable]
        struct ProgressSnapshot
        {
            public string participantId;
            public string sessionId;
            public string taskKind;
            public string timestamp;
            public int currentBlockIndex;
            public int nextTrialIndex;
            public int trialsCompleted;
        }
    }
}
