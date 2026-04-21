using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Logs trial judgements to CSV and JSON files.
    /// Files are written to Application.persistentDataPath/Logs/.
    /// </summary>
    public class TaskLogger : MonoBehaviour
    {
        [SerializeField] string m_ParticipantId = "P000";
        [SerializeField] string m_SessionId = "";

        string m_LogDir;
        string m_CsvPath;
        string m_JsonPath;
        StreamWriter m_CsvWriter;
        readonly List<TrialJudgement> m_Judgements = new();
        bool m_SessionOpen;

        public string ParticipantId
        {
            get => m_ParticipantId;
            set => m_ParticipantId = value;
        }

        public void BeginSession(string taskName)
        {
            if (string.IsNullOrEmpty(m_SessionId))
                m_SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            m_LogDir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(m_LogDir);

            string baseName = $"{m_ParticipantId}_{m_SessionId}_{taskName}";
            m_CsvPath = Path.Combine(m_LogDir, baseName + ".csv");
            m_JsonPath = Path.Combine(m_LogDir, baseName + ".json");

            m_CsvWriter = new StreamWriter(m_CsvPath, false, Encoding.UTF8);
            m_CsvWriter.WriteLine("TrialId,Block,Category,Expected,Received,Result,IsCorrect,StimulusOnset,ResponseTime,ReactionTimeMs,LateralOffsetM,ApproachAngleDeg,FailureReason");
            m_CsvWriter.Flush();

            m_Judgements.Clear();
            m_SessionOpen = true;

            Debug.Log($"[TaskLogger] Session started. CSV: {m_CsvPath}");
        }

        public void LogTrial(TrialJudgement j)
        {
            if (!m_SessionOpen) return;

            m_Judgements.Add(j);

            m_CsvWriter.WriteLine(
                $"{j.trialId}," +
                $"{j.blockIndex}," +
                $"{j.category}," +
                $"{j.expected}," +
                $"{j.received}," +
                $"{j.result}," +
                $"{j.isCorrect}," +
                $"{j.stimulusOnsetTime:F4}," +
                $"{j.responseTime:F4}," +
                $"{j.reactionTimeMs:F1}," +
                $"{j.lateralOffsetMeters:F4}," +
                $"{j.approachAngleDeg:F1}," +
                $"\"{j.failureReason}\""
            );
            m_CsvWriter.Flush();
        }

        public void EndSession()
        {
            if (!m_SessionOpen) return;

            m_CsvWriter?.Close();
            m_CsvWriter = null;

            string json = JsonUtility.ToJson(new SessionLog
            {
                participantId = m_ParticipantId,
                sessionId = m_SessionId,
                timestamp = DateTime.Now.ToString("o"),
                totalTrials = m_Judgements.Count,
                judgements = m_Judgements.ToArray()
            }, true);
            File.WriteAllText(m_JsonPath, json, Encoding.UTF8);

            m_SessionOpen = false;
            Debug.Log($"[TaskLogger] Session ended. Logs saved to {m_LogDir}");
        }

        void OnDestroy()
        {
            if (m_SessionOpen)
                EndSession();
        }

        [Serializable]
        struct SessionLog
        {
            public string participantId;
            public string sessionId;
            public string timestamp;
            public int totalTrials;
            public TrialJudgement[] judgements;
        }
    }
}
