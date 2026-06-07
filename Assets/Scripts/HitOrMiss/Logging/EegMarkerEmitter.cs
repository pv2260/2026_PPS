using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;


/// <summary>
/// Sends EEG trigger codes to the Python trigger_server.py over a local TCP
/// socket, and logs every event to a CSV file.
///
/// Python owns the serial port to the Arduino exclusively.
/// Unity never opens a COM port.
///
/// ── Setup ────────────────────────────────────────────────────────────────────
///  1. Start trigger_server.py BEFORE hitting Play in Unity.
///  2. Attach this script to any persistent GameObject.
///  3. Call  Emit("trial_start")  from any other script.
/// </summary>
public class EegMarkerEmitter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Participant")]
    [SerializeField] string m_ParticipantId = "P000";
    [Header("Trigger Server (Python)")]
    //[SerializeField] bool   m_UseTrigger    = true; temporarily disable
    [SerializeField] bool   m_UseTrigger    = false;
    [Tooltip("Must match trigger_server.py → TCP_HOST")]
    [SerializeField] string m_TcpHost       = "127.0.0.1";
    [Tooltip("Must match trigger_server.py → TCP_PORT")]
    [SerializeField] int    m_TcpPort       = 5005;
    [Header("Trigger Pulse")]
    [Tooltip("Seconds before a reset (0) is sent after each trigger")]
    [SerializeField] float  m_TriggerDuration = 0.01f;

    // ── Private state ─────────────────────────────────────────────────────────

    TcpClient    m_Client;
    NetworkStream m_Stream;
    StreamWriter  m_CsvWriter;
    string        m_LogPath;

    // ── Public API ────────────────────────────────────────────────────────────

    public void BeginSession(string sessionId)
    {
        // CSV log
        string dir = Path.Combine(Application.persistentDataPath, "EEG_Markers");
        Directory.CreateDirectory(dir);
        m_LogPath   = Path.Combine(dir, $"{m_ParticipantId}_{sessionId}_markers.csv");
        m_CsvWriter = new StreamWriter(m_LogPath, append: false);
        m_CsvWriter.WriteLine("Time,EventCode,TriggerValue");
        m_CsvWriter.Flush();

        // TCP connection
        if (m_UseTrigger)
            OpenTcp();

        Emit("session_start");
        Debug.Log($"[ArduinoTrigger] Session started. Log: {m_LogPath}");
    }

    public void Emit(string eventCode, string trialId = "", string category = "",
        string expected = "", string received = "", string extra = "")
    {
        double time = Time.timeAsDouble;
        int triggerValue;

        if (!string.IsNullOrEmpty(extra) && int.TryParse(extra, out int customCode))
        {
            // Add a condition if trial ID is not empty
            // else if(!string.IsNullOrEmpty(trialId))
            //{triggerValue = GetTriggerCode(eventCode, category, "", "", trialID);}
            // else {}
            // Or put trial ID and category ... in the call
            triggerValue = GetTriggerCode(eventCode, "", "", extra);
        }
        else if(!string.IsNullOrEmpty(received))
        {
            triggerValue = GetTriggerCode(eventCode, "", received);
        }
        else if(!string.IsNullOrEmpty(category))
        {
            triggerValue = GetTriggerCode(eventCode, category);
        }
        else if(!string.IsNullOrEmpty(trialId))
        {
            triggerValue = GetTriggerCode(eventCode, "", "", "", trialId);
        }
        else
        {
            triggerValue = GetTriggerCode(eventCode);
        }

        // 1. Write to local CSV
        if (m_CsvWriter != null)
        {
            m_CsvWriter.WriteLine($"{time:F6},{eventCode},{trialId},{category},{expected},{received},{extra}");
            m_CsvWriter.Flush();
        }

        // TCP
        if (m_UseTrigger && m_Stream != null)
        {
            SendValue(triggerValue);
        }
        Debug.LogWarning($"[ArduinoTrigger] Emit: {eventCode} → {triggerValue}");
    }

    public void EndSession()
    {
        Emit("session_end");

        m_CsvWriter?.Close();
        m_CsvWriter = null;

        CloseTcp();
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void OnDestroy()
    {
        EndSession();
    }

    // ── Trigger code map ──────────────────────────────────────────────────────
    int GetTriggerCode(string eventCode, string category = "", string received = "", string extra = "", string trialId = "")
    {
        // If extra is provided and is a valid integer, use it as the trigger code
        if (!string.IsNullOrEmpty(extra) && int.TryParse(extra, out int customCode))
        {
            switch(eventCode)
            {
                case "trial_spawn": return customCode;
                case "pps_loom_onset": return customCode; // Check 6 pin encoding
                case "pps_trial_end": return customCode;
                Debug.LogWarning($"[ArduinoTrigger] '{eventCode}' → {customCode}");
                default:
                    Debug.LogWarning($"[ArduinoTrigger] Unknown RECEIVED: '{eventCode}' → 159");
                    return 159;
            }
            
        }
        else
        {
            switch (eventCode)
            {
                // Task 1
                case "pps_session_start": return 140;
                case "pps_session_end": return 141;
                case "pps_vib_fired": return 64;
                case "pps_vib_fired_trigger": return 62;
                case "pps_response": return 61;

                // Task 2
                case "session_start": return 130;
                case "phase_intro": return 131;
                case "phase_practice": return 132;
                case "phase_rest": return 133;
                case "phase_outro": return 134;
                case "session_paused":    return 135;
                case "session_resumed":   return 136;
                case "session_end":   return 137;
                case "phase_controller_practice":   return 138;
                case "phase_ball_demo":   return 139;
                case "phase_easy_practice":   return 140;
                case "phase_difficult_practice":   return 141;

                case "phase_block":       return 25;
                case "block_start": return 26;
                case "block_restart":     return 27;
                case "block_paused":      return 28;
                case "trial_timeout":     return 29;
                case "block_end":         return 30;

                // controller response
                case "controller_left":   return 40;
                case "controller_right":  return 41;
                case "trial_no_response": return 42;
                case "trial_too_slow": return 43;
                default:
                        Debug.LogWarning($"[ArduinoTrigger] Unknown event: '{eventCode}' → 159");
                        return 159;
            }
        }      
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    void OpenTcp()
    {
        try
        {
            m_Client = new TcpClient(m_TcpHost, m_TcpPort);
            m_Stream = m_Client.GetStream();
            Debug.Log($"[ArduinoTrigger] Connected to trigger server at {m_TcpHost}:{m_TcpPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ArduinoTrigger] TCP connect failed: {ex.Message}\n" +
                "Make sure trigger_server.py is running before pressing Play." );
        }
    }

    void CloseTcp()
    {
        m_Stream?.Close();
        m_Client?.Close();
        m_Stream = null;
        m_Client = null;
        Debug.Log("[ArduinoTrigger] TCP connection closed.");
    }

    /// <summary>Sends an integer as a newline-terminated UTF-8 string.</summary>
    void SendValue(int value)
    {
        if (m_Stream == null) return;
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value + "\n");
            m_Stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArduinoTrigger] TCP send failed: {ex.Message}");
        }
    }

}


// using System;
// using System.IO;
// using System.Net;
// using System.Net.Sockets;
// using System.Text;
// using UnityEngine;

// namespace HitOrMiss
// {
//     /// <summary>
//     /// Emits event markers for EEG synchronization.
//     /// Writes to a local CSV log and sends markers over UDP to an external receiver
//     /// (e.g. a Python script that forwards them to the EEG acquisition system).
//     ///
//     /// UDP protocol (JSON per packet):
//     /// {"engineTime":1.234,"eventCode":"trial_spawn","trialId":"B1_T01","category":"Hit","expected":"Hit","received":"","extra":""}
//     ///
//     /// Enable m_UseNetworkBridge in the Inspector and configure host/port to match
//     /// your Python receiver. Default: 127.0.0.1:12345 (same machine).
//     /// </summary>
//     public class EegMarkerEmitter : MonoBehaviour
//     {
//         [Header("Local Logging")]
//         [SerializeField] string m_ParticipantId = "P000";

//         [Header("Network Bridge (UDP to Python)")]
//         [Tooltip("Enable to send markers over UDP to an external receiver (e.g. Python EEG bridge)")]
//         [SerializeField] bool m_UseNetworkBridge;

//         [Tooltip("IP address of the Python receiver. Use 127.0.0.1 if running on the same machine")]
//         [SerializeField] string m_BridgeHost = "127.0.0.1";

//         [Tooltip("UDP port the Python receiver is listening on")]
//         [SerializeField] int m_BridgePort = 12345;

//         StreamWriter m_Writer;
//         string m_LogPath;
//         UdpClient m_UdpClient;
//         IPEndPoint m_RemoteEndPoint;

//         /// <summary>
//         /// C# event fired every time a marker is emitted.
//         /// Subscribe from any Unity component to react to EEG events in real time.
//         /// </summary>
//         public event Action<EegMarker> MarkerEmitted;

//         /// <summary>
//         /// Whether the UDP network bridge is currently active.
//         /// </summary>
//         public bool IsNetworkBridgeActive => m_UseNetworkBridge && m_UdpClient != null;

//         /// <summary>
//         /// The host address the UDP bridge sends to.
//         /// Can be changed at runtime before calling BeginSession().
//         /// </summary>
//         public string BridgeHost
//         {
//             get => m_BridgeHost;
//             set => m_BridgeHost = value;
//         }

//         /// <summary>
//         /// The UDP port the bridge sends to.
//         /// Can be changed at runtime before calling BeginSession().
//         /// </summary>
//         public int BridgePort
//         {
//             get => m_BridgePort;
//             set => m_BridgePort = value;
//         }

//         /// <summary>
//         /// Enable or disable the network bridge at runtime.
//         /// If enabled while a session is active, opens the UDP socket immediately.
//         /// </summary>
//         public bool UseNetworkBridge
//         {
//             get => m_UseNetworkBridge;
//             set
//             {
//                 m_UseNetworkBridge = value;
//                 if (value)
//                     OpenUdpSocket();
//                 else
//                     CloseUdpSocket();
//             }
//         }

//         public void BeginSession(string sessionId)
//         {
//             // --- Local CSV log ---
//             string dir = Path.Combine(Application.persistentDataPath, "EEG_Markers");
//             Directory.CreateDirectory(dir);

//             m_LogPath = Path.Combine(dir, $"{m_ParticipantId}_{sessionId}_markers.csv");
//             m_Writer = new StreamWriter(m_LogPath, false, Encoding.UTF8);
//             m_Writer.WriteLine("EngineTime,EventCode,TrialId,Category,Expected,Received,Extra");
//             m_Writer.Flush();

//             // --- UDP socket ---
//             if (m_UseNetworkBridge)
//                 OpenUdpSocket();

//             Emit("session_start", "", "", "", "", "");
//             Debug.Log($"[EegMarkerEmitter] Marker log: {m_LogPath}");

//             if (m_UseNetworkBridge)
//                 Debug.Log($"[EegMarkerEmitter] UDP bridge active -> {m_BridgeHost}:{m_BridgePort}");
//         }

//         public void Emit(string eventCode, string trialId = "", string category = "",
//             string expected = "", string received = "", string extra = "")
//         {
//             double time = Time.timeAsDouble;

//             var marker = new EegMarker
//             {
//                 engineTime = time,
//                 eventCode = eventCode,
//                 trialId = trialId,
//                 category = category,
//                 expected = expected,
//                 received = received,
//                 extra = extra
//             };

//             // 1. Write to local CSV
//             if (m_Writer != null)
//             {
//                 m_Writer.WriteLine($"{time:F6},{eventCode},{trialId},{category},{expected},{received},{extra}");
//                 m_Writer.Flush();
//             }

//             // 2. Send over UDP to Python bridge
//             if (m_UseNetworkBridge)
//                 SendOverNetwork(marker);

//             // 3. Fire C# event for any Unity subscribers
//             MarkerEmitted?.Invoke(marker);
//         }

//         void SendOverNetwork(EegMarker marker)
//         {
//             if (m_UdpClient == null) return;

//             try
//             {
//                 // JSON format - easy to parse in Python with json.loads()
//                 string json = JsonUtility.ToJson(marker);
//                 byte[] data = Encoding.UTF8.GetBytes(json);
//                 m_UdpClient.Send(data, data.Length, m_RemoteEndPoint);
//             }
//             catch (SocketException ex)
//             {
//                 // UDP send failure is non-fatal - log but don't interrupt the task
//                 Debug.LogWarning($"[EegMarkerEmitter] UDP send failed: {ex.Message}");
//             }
//         }

//         void OpenUdpSocket()
//         {
//             if (m_UdpClient != null) return;

//             try
//             {
//                 m_UdpClient = new UdpClient();
//                 m_RemoteEndPoint = new IPEndPoint(IPAddress.Parse(m_BridgeHost), m_BridgePort);
//                 Debug.Log($"[EegMarkerEmitter] UDP socket opened -> {m_BridgeHost}:{m_BridgePort}");
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"[EegMarkerEmitter] Failed to open UDP socket: {ex.Message}");
//                 m_UdpClient = null;
//             }
//         }

//         void CloseUdpSocket()
//         {
//             m_UdpClient?.Close();
//             m_UdpClient = null;
//         }

//         public void EndSession()
//         {
//             Emit("session_end");
//             m_Writer?.Close();
//             m_Writer = null;
//             CloseUdpSocket();
//         }

//         void OnDestroy()
//         {
//             EndSession();
//         }
//     }

//     /// <summary>
//     /// Data structure for a single EEG marker event.
//     /// Serialized as JSON when sent over UDP to the Python bridge.
//     /// </summary>
//     [Serializable]
//     public struct EegMarker
//     {
//         public double engineTime;
//         public string eventCode;
//         public string trialId;
//         public string category;
//         public string expected;
//         public string received;
//         public string extra;
//     }
// }
