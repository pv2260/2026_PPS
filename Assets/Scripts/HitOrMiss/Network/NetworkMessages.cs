using System;

namespace HitOrMiss.Network
{
    /// <summary>
    /// Wire-format constants. Bumped only on breaking changes to the JSON
    /// schemas below. Keep server and clinician GUI in lockstep on this value.
    /// </summary>
    public static class Protocol
    {
        public const string Version = "1.0.0";
        public const int DefaultPort = 7777;
    }

    // ---------- Server -> Client (WebSocket events) ----------

    /// <summary>
    /// Generic envelope used for all WebSocket pushes. <c>type</c> tells the
    /// clinician GUI which payload struct to parse.
    /// </summary>
    [Serializable]
    public struct WsEnvelope
    {
        public string type;
        public string payload;     // JSON-encoded inner payload (string so JsonUtility can roundtrip)
        public string serverTime;  // ISO 8601, set by the server when sending

        public static WsEnvelope For<T>(string type, T payload)
        {
            return new WsEnvelope
            {
                type = type,
                payload = UnityEngine.JsonUtility.ToJson(payload),
                serverTime = DateTime.UtcNow.ToString("o"),
            };
        }
    }

    [Serializable]
    public struct ServerStatusEvent
    {
        public string protocolVersion;
        public string sessionId;
        public string participantId;
        public string phase;          // TaskPhase.ToString()
        public int currentBlockIndex; // 0-based
        public int trialsCompletedInBlock;
        public int totalTrialsInBlock;
        public bool isRunning;
        public bool isPaused;
    }

    [Serializable]
    public struct PhaseChangedEvent
    {
        public string phase;
        public int currentBlockIndex;
    }

    [Serializable]
    public struct TrialStartedEvent
    {
        public string trialId;
        public int blockIndex;
        public int trialNumberInBlock;
        public string category;
        public string trajectoryId;
        public float speedMps;
        public bool isSwitchTrial;
    }

    [Serializable]
    public struct TrialCompletedEvent
    {
        public string trialId;
        public int blockIndex;
        public int trialNumberInBlock;
        public string category;
        public string expected;          // SemanticCommand.ToString()
        public string received;
        public string result;            // TrialResult.ToString()
        public bool isCorrect;
        public double reactionTimeMs;
        public float speedMps;
        public bool isSwitchTrial;
    }

    [Serializable]
    public struct SimpleEvent
    {
        public string note;
    }

    // ---------- Client -> Server (HTTP requests) ----------

    /// <summary>
    /// POST /api/session/start
    /// Body: this struct. The server seeds the active session, applies the
    /// metadata, and starts the experiment.
    /// </summary>
    [Serializable]
    public struct StartSessionRequest
    {
        public SessionMetadata metadata;
    }

    /// <summary>
    /// POST /api/session/parameters (optional, before start)
    /// Mutates the live <see cref="TrajectoryTaskAsset"/> values for this
    /// session only — written to a session-local clone so the on-disk asset
    /// is not modified.
    /// </summary>
    [Serializable]
    public struct TaskParametersRequest
    {
        public int blockCount;
        public int trialsPerCategory;
        public float fastSpeedMps;
        public float slowSpeedMps;
        public float itiMinSeconds;
        public float itiMaxSeconds;
        public float restDurationSeconds;
    }

    [Serializable]
    public struct AckResponse
    {
        public bool ok;
        public string error;
        public string detail;

        public static AckResponse Ok() => new() { ok = true };
        public static AckResponse Fail(string error, string detail = "") =>
            new() { ok = false, error = error, detail = detail };
    }

    // ---------- Session library (GET endpoints) ----------

    [Serializable]
    public struct SessionSummary
    {
        public string sessionFolder;     // e.g. "P001_20260423_095115"
        public string participantId;
        public string sessionDate;
        public string sessionId;
        public int trialCount;
        public bool hasProgressSnapshot;
    }

    [Serializable]
    public struct SessionListResponse
    {
        public string protocolVersion;
        public SessionSummary[] sessions;
    }
}
