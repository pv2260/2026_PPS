using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Emits event markers for EEG synchronization.
    /// Writes to a local CSV log and sends markers over UDP to an external receiver
    /// (e.g. a Python script that forwards them to the EEG acquisition system).
    ///
    /// UDP protocol (JSON per packet):
    /// {"engineTime":1.234,"eventCode":"trial_spawn","trialId":"B1_T01","category":"Hit","expected":"Hit","received":"","extra":""}
    ///
    /// Enable m_UseNetworkBridge in the Inspector and configure host/port to match
    /// your Python receiver. Default: 127.0.0.1:12345 (same machine).
    /// </summary>
    public class EegMarkerEmitter : MonoBehaviour
    {
        [Header("Local Logging")]
        [SerializeField] string m_ParticipantId = "P000";

        [Header("Network Bridge (UDP to Python)")]
        [Tooltip("Enable to send markers over UDP to an external receiver (e.g. Python EEG bridge)")]
        [SerializeField] bool m_UseNetworkBridge;

        [Tooltip("IP address of the Python receiver. Use 127.0.0.1 if running on the same machine")]
        [SerializeField] string m_BridgeHost = "127.0.0.1";

        [Tooltip("UDP port the Python receiver is listening on")]
        [SerializeField] int m_BridgePort = 12345;

        StreamWriter m_Writer;
        string m_LogPath;
        UdpClient m_UdpClient;
        IPEndPoint m_RemoteEndPoint;

        /// <summary>
        /// C# event fired every time a marker is emitted.
        /// Subscribe from any Unity component to react to EEG events in real time.
        /// </summary>
        public event Action<EegMarker> MarkerEmitted;

        /// <summary>
        /// Whether the UDP network bridge is currently active.
        /// </summary>
        public bool IsNetworkBridgeActive => m_UseNetworkBridge && m_UdpClient != null;

        /// <summary>
        /// The host address the UDP bridge sends to.
        /// Can be changed at runtime before calling BeginSession().
        /// </summary>
        public string BridgeHost
        {
            get => m_BridgeHost;
            set => m_BridgeHost = value;
        }

        /// <summary>
        /// The UDP port the bridge sends to.
        /// Can be changed at runtime before calling BeginSession().
        /// </summary>
        public int BridgePort
        {
            get => m_BridgePort;
            set => m_BridgePort = value;
        }

        /// <summary>
        /// Enable or disable the network bridge at runtime.
        /// If enabled while a session is active, opens the UDP socket immediately.
        /// </summary>
        public bool UseNetworkBridge
        {
            get => m_UseNetworkBridge;
            set
            {
                m_UseNetworkBridge = value;
                if (value)
                    OpenUdpSocket();
                else
                    CloseUdpSocket();
            }
        }

        public void BeginSession(string sessionId)
        {
            // --- Local CSV log ---
            string dir = Path.Combine(Application.persistentDataPath, "EEG_Markers");
            Directory.CreateDirectory(dir);

            m_LogPath = Path.Combine(dir, $"{m_ParticipantId}_{sessionId}_markers.csv");
            m_Writer = new StreamWriter(m_LogPath, false, Encoding.UTF8);
            m_Writer.WriteLine("EngineTime,EventCode,TrialId,Category,Expected,Received,Extra");
            m_Writer.Flush();

            // --- UDP socket ---
            if (m_UseNetworkBridge)
                OpenUdpSocket();

            Emit("session_start", "", "", "", "", "");
            Debug.Log($"[EegMarkerEmitter] Marker log: {m_LogPath}");

            if (m_UseNetworkBridge)
                Debug.Log($"[EegMarkerEmitter] UDP bridge active -> {m_BridgeHost}:{m_BridgePort}");
        }

        public void Emit(string eventCode, string trialId = "", string category = "",
            string expected = "", string received = "", string extra = "")
        {
            double time = Time.timeAsDouble;

            var marker = new EegMarker
            {
                engineTime = time,
                eventCode = eventCode,
                trialId = trialId,
                category = category,
                expected = expected,
                received = received,
                extra = extra
            };

            // 1. Write to local CSV
            if (m_Writer != null)
            {
                m_Writer.WriteLine($"{time:F6},{eventCode},{trialId},{category},{expected},{received},{extra}");
                m_Writer.Flush();
            }

            // 2. Send over UDP to Python bridge
            if (m_UseNetworkBridge)
                SendOverNetwork(marker);

            // 3. Fire C# event for any Unity subscribers
            MarkerEmitted?.Invoke(marker);
        }

        void SendOverNetwork(EegMarker marker)
        {
            if (m_UdpClient == null) return;

            try
            {
                // JSON format - easy to parse in Python with json.loads()
                string json = JsonUtility.ToJson(marker);
                byte[] data = Encoding.UTF8.GetBytes(json);
                m_UdpClient.Send(data, data.Length, m_RemoteEndPoint);
            }
            catch (SocketException ex)
            {
                // UDP send failure is non-fatal - log but don't interrupt the task
                Debug.LogWarning($"[EegMarkerEmitter] UDP send failed: {ex.Message}");
            }
        }

        void OpenUdpSocket()
        {
            if (m_UdpClient != null) return;

            try
            {
                m_UdpClient = new UdpClient();
                m_RemoteEndPoint = new IPEndPoint(IPAddress.Parse(m_BridgeHost), m_BridgePort);
                Debug.Log($"[EegMarkerEmitter] UDP socket opened -> {m_BridgeHost}:{m_BridgePort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EegMarkerEmitter] Failed to open UDP socket: {ex.Message}");
                m_UdpClient = null;
            }
        }

        void CloseUdpSocket()
        {
            m_UdpClient?.Close();
            m_UdpClient = null;
        }

        public void EndSession()
        {
            Emit("session_end");
            m_Writer?.Close();
            m_Writer = null;
            CloseUdpSocket();
        }

        void OnDestroy()
        {
            EndSession();
        }
    }

    /// <summary>
    /// Data structure for a single EEG marker event.
    /// Serialized as JSON when sent over UDP to the Python bridge.
    /// </summary>
    [Serializable]
    public struct EegMarker
    {
        public double engineTime;
        public string eventCode;
        public string trialId;
        public string category;
        public string expected;
        public string received;
        public string extra;
    }
}
