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
    [SerializeField] bool   m_UseTrigger    = true;
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
            triggerValue = GetTriggerCode(eventCode, "", "", extra);
        }
        // For Click triggers send in OnResponseReceived
        else if(!string.IsNullOrEmpty(received))
        {
            triggerValue = GetTriggerCode(eventCode, "", received);
        }
        else if(!string.IsNullOrEmpty(category))
        {
            triggerValue = GetTriggerCode(eventCode, category);
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

    int GetTriggerCode(string eventCode, string category = "", string received = "", string extra = "")
    {
        // If extra is provided and is a valid integer, use it as the trigger code
        if (!string.IsNullOrEmpty(extra) && int.TryParse(extra, out int customCode))
        {
            return customCode;
        }


        else if(!string.IsNullOrEmpty(received))
        {
            Debug.LogWarning($"EventCode: '{eventCode}'");
            Debug.LogWarning($"Received: '{received}'");
            int receive;
            switch (received?.Trim())
            {
                case "controller_left": receive = 1; break;
                case "controller_right": receive = 2; break;
                case "Hit": receive = 3; break;
                case "Miss": receive = 4; break;
            }
            switch (eventCode)
            {
                case "response_hit": return 81;
                case "response_miss": return 82;
                case "trial_resolved_correct": return 83;
                case "trial_resolved_incorrect": return 84;
                default:
                    Debug.LogWarning($"[ArduinoTrigger] Unknown RECEIVED: '{eventCode}' → 255");
                    return 255;
            }
        }


        else if(!string.IsNullOrEmpty(category))
        {
            Debug.LogWarning($"Category: '{category}'");
            
            int cat;
            switch (category?.Trim())
            {
                case "-1": cat = 0; break; // Test trial
                case "0": cat = 1; break; // Test trial

                case "ClearHit": cat = 1; break;
                case "ClearMiss": cat = 2; break;

                default:
                    Debug.LogWarning($"[ArduinoTrigger] Unknown CATEGORZ: '{category}' → 0");
                    cat = 0;
                    break;
            }
            switch (eventCode)
            {
                case "trial_resolved_incorrect": return 190;
                case "trial_resolved_correct": return 190;

                case "phase_block": return 10 + cat; // Called in HItOrMissAppController
                case "trial_block_start": return 20 + cat;

                case "block_restart":     return 20 + cat;
                case "block_end":         return 30 + cat;
                case "block_paused":      return 40 + cat;
                case "trial_timeout":     return 50 + cat;
                case "session_paused": return 1; // Called in HItOrMissAppController
                case "session_resumed": return 2; // Called in HItOrMissAppController
                default:
                    Debug.LogWarning($"[ArduinoTrigger] Unknown EVENT: '{eventCode}' → 255");
                    return 255;
            }
        }



        else
        {
            switch (eventCode)
            {
                case "phase_intro": return 1; // Called in HItOrMissAppController
                case "phase_practice": return 2; // Called in HItOrMissAppController
                case "phase_rest": return 3; // Called in HItOrMissAppController
                case "phase_outro": return 3; // Called in HItOrMissAppController
                case "response_hit": return 2;
                case "session_start": return 3;
                case "session_end":   return 4;
                default:
                        Debug.LogWarning($"[ArduinoTrigger] Unknown event: '{eventCode}' → 255");
                        return 255;
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
                           "Make sure trigger_server.py is running before pressing Play.");
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
