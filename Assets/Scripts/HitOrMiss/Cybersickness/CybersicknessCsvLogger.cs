using System.Globalization;
using System.IO;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CybersicknessCsvLogger : MonoBehaviour
    {
        [SerializeField] string m_ParticipantId = "P000";

        StreamWriter m_Writer;
        string m_FilePath;

        public string FilePath => m_FilePath;

        public void BeginSession(string participantId)
        {
            if (!string.IsNullOrWhiteSpace(participantId))
                m_ParticipantId = participantId;

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"cyberbug_{m_ParticipantId}_{timestamp}.csv";

            m_FilePath = Path.Combine(Application.persistentDataPath, fileName);
            m_Writer = new StreamWriter(m_FilePath);

            m_Writer.WriteLine(
                "participant_id," +
                "trial_id," +
                "block_index," +
                "trial_index," +
                "condition," +
                "start_distance," +
                "speed," +
                "lateral_offset," +
                "will_touch," +
                "will_splash," +
                "expected_response," +
                "participant_response," +
                "responded," +
                "correct," +
                "trial_start_time," +
                "response_time," +
                "outcome_time," +
                "reaction_time_ms");

            Debug.Log($"[CybersicknessCsvLogger] Logging to {m_FilePath}");
        }

        public void LogTrial(CyberBugTrialResult r)
        {
            if (m_Writer == null) return;

            string line = string.Join(",",
                Escape(m_ParticipantId),
                Escape(r.trialId),
                r.blockIndex.ToString(CultureInfo.InvariantCulture),
                r.trialIndex.ToString(CultureInfo.InvariantCulture),
                Escape(r.condition.ToString()),
                r.startDistance.ToString(CultureInfo.InvariantCulture),
                r.speed.ToString(CultureInfo.InvariantCulture),
                r.lateralOffset.ToString(CultureInfo.InvariantCulture),
                r.willTouch.ToString(CultureInfo.InvariantCulture),
                r.willSplash.ToString(CultureInfo.InvariantCulture),
                Escape(r.expectedResponse.ToString()),
                Escape(r.participantResponse.ToString()),
                r.responded.ToString(CultureInfo.InvariantCulture),
                r.correct.ToString(CultureInfo.InvariantCulture),
                r.trialStartTime.ToString(CultureInfo.InvariantCulture),
                r.responseTime.ToString(CultureInfo.InvariantCulture),
                r.outcomeTime.ToString(CultureInfo.InvariantCulture),
                r.reactionTimeMs.ToString(CultureInfo.InvariantCulture));

            m_Writer.WriteLine(line);
            m_Writer.Flush();
        }

        public void EndSession()
        {
            if (m_Writer == null) return;

            m_Writer.Flush();
            m_Writer.Close();
            m_Writer = null;

            Debug.Log($"[CybersicknessCsvLogger] Session closed: {m_FilePath}");
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            bool mustQuote =
                value.Contains(",") ||
                value.Contains("\"") ||
                value.Contains("\n") ||
                value.Contains("\r");

            if (!mustQuote) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}