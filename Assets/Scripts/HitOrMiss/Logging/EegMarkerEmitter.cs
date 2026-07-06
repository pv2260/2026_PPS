using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using UnityEngine;

namespace HitOrMiss
{

    /// <summary>
    /// Emits EEG/event markers, logs them to CSV, and optionally sends trigger bytes
    /// to an Arduino through ArduinoTrigger.
    /// </summary>
    public class EegMarkerEmitter : MonoBehaviour
    {
        // Added by Pam: new logging system /////////////////////////////////////////////////////////
        [Header("Participant / Session")]
        [SerializeField] private string m_ParticipantId = "P000";
        [SerializeField] private string m_SessionId = "S1";

        public string ParticipantId => m_ParticipantId;
        public string SessionId => m_SessionId;
        public string SessionDirectory { get; private set; }
        /////////////////////////////////////////////////////////

        [Header("Arduino Serial Trigger")]
        [Tooltip("Enable to send trigger bytes over serial to an Arduino")]
        [SerializeField] bool m_UseSerialBridge = false;

        [Tooltip("If true, the inspector COM port is only a hint. At every session start, the emitter " +
                 "rescans the OS-reported port list and uses the first port that actually opens (preferring " +
                 "the hint if it's present). This avoids the headache of a stale COM number persisting " +
                 "after the Arduino is unplugged + replugged on a different USB port.")]
        [SerializeField] bool m_AutoDetectPort = true;

        [Tooltip("COM port the Arduino is on, e.g. COM8 on Windows. When AutoDetect is on, this is a hint " +
                 "tried first; when off, this port is used as-is.")]
        [SerializeField] string m_ComPort = "COM7";

        [Tooltip("Baud rate — must match the Arduino sketch")]
        [SerializeField] int m_BaudRate = 115200;

        [Tooltip("How long the trigger byte is held before sending 0. 0.01 = 10 ms.")]
        [SerializeField] float m_TriggerDuration = 0.01f;

        // Port that was actually opened this session. NOT serialized — so a
        // runtime resolution never overwrites the inspector hint on disk.
        // Available for logs/diagnostics.
        [System.NonSerialized] string m_ResolvedComPort = "";
        public string ResolvedComPort => m_ResolvedComPort;

        StreamWriter m_CsvWriter;
        string m_LogPath;
        ArduinoTrigger m_Arduino;
        bool m_SessionOpen;

        public struct EegMarker
        {
            public double engineTime;
            public string eventCode;
            public string trialId;
            public string category;
            public string expected;
            public string received;
            public string extra;
            public byte triggerByte;
        }

        public event Action<EegMarker> MarkerEmitted;

        public string ComPort
        {
            get => m_ComPort;
            set => m_ComPort = value;
        }

        public int BaudRate
        {
            get => m_BaudRate;
            set => m_BaudRate = value;
        }

        public bool UseSerialBridge
        {
            get => m_UseSerialBridge;
            set => m_UseSerialBridge = value;
        }

        public void BeginSession()
        {

        // Added by Pam: new logging system /////////////////////////////////////////////////////////
            if (m_SessionOpen)
            {
                Debug.LogWarning("[EegMarkerEmitter] BeginSession called while a session was already open.");
                return;
            }
            
            SessionDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Logger",
                $"{m_ParticipantId}_{m_SessionId}"
            );

            Directory.CreateDirectory(SessionDirectory);
            m_LogPath = Path.Combine(SessionDirectory, $"{m_ParticipantId}_{m_SessionId}_markers.csv");
        /////////////////////////////////////////////////////////

            
            // string dir = Path.Combine(Directory.GetCurrentDirectory, "Logger", $"");
            // Directory.CreateDirectory(dir);
            // m_LogPath = Path.Combine(dir, $"{m_ParticipantId}_{sessionId}_markers.csv");

            m_CsvWriter = new StreamWriter(m_LogPath, append: false);
            m_CsvWriter.WriteLine("Time,EventCode,TrialId,Category,Expected,Received,Extra,TriggerValue");
            m_CsvWriter.Flush();

            m_SessionOpen = true;

            Debug.LogWarning($"[EegMarkerEmitter] Marker log: {m_LogPath}");

            if (m_UseSerialBridge)
            {
                OpenSerialPort();
                Debug.LogWarning(
                    $"[EegMarkerEmitter] Serial bridge active -> {m_ComPort} @ {m_BaudRate} baud | " +
                    $"object={gameObject.name} | instanceID={GetInstanceID()}");

                //Emit("session_start");
            }
        }

        /// Pam: New logger system, to improve compatibility with other tasks using EegMarkerEmitter
        public void BeginSession(string ignoredSessionId)
        {
            BeginSession();
        }

        public void Emit(
            string eventCode,
            string trialId = "",
            string category = "",
            string expected = "",
            string received = "",
            string extra = "",
            byte triggerByte = 0)
        {
            double time = Time.timeAsDouble;

            int triggerValue = GetTriggerCode(eventCode, category, received, extra, trialId);

            var marker = new EegMarker
            {
                engineTime = time,
                eventCode = eventCode,
                trialId = trialId,
                category = category,
                expected = expected,
                received = received,
                extra = extra,
                triggerByte = (byte)Mathf.Clamp(triggerValue, 0, 255)
            };

            // 1. Write to local CSV
            if (m_CsvWriter != null)
            {
                m_CsvWriter.WriteLine(
                    $"{time:F6},{eventCode},{trialId},{category},{expected},{received},{extra},{triggerValue}");
                m_CsvWriter.Flush();
            }

            // 2. Send serial trigger pulse
            if (m_UseSerialBridge && triggerValue > 0)
            {
                if (m_Arduino == null)
                {
                    Debug.LogWarning("[EegMarkerEmitter] Skipping trigger — Arduino not initialized.");
                }
                else
                {
                    float duration = m_TriggerDuration;

                    // Vibration trigger: hold longer so the Arduino/motor detects it clearly.
                    if (triggerValue == 127)
                        duration = 0.1f;

                    m_Arduino.SendTrigger((byte)triggerValue, duration);
                }
            }

            // 3. Fire C# event for Unity subscribers
            MarkerEmitted?.Invoke(marker);
        }

        void OpenSerialPort()
        {
            if (m_Arduino != null)
                return;

            m_Arduino = gameObject.AddComponent<ArduinoTrigger>();
            m_ResolvedComPort = "";

            if (!m_AutoDetectPort)
            {
                if (m_Arduino.Open(m_ComPort, m_BaudRate))
                {
                    m_ResolvedComPort = m_ComPort;
                    Debug.Log($"[EegMarkerEmitter] Arduino opened on {m_ComPort} (auto-detect off).");
                }
                else
                {
                    Debug.LogWarning($"[EegMarkerEmitter] Arduino did NOT open on {m_ComPort} (auto-detect off). Trigger pulses will be skipped.");
                }
                return;
            }

            // Auto-detect: build a candidate list with the inspector hint
            // first (if it's currently present in the OS port list), then
            // every other port the OS reports. Try each until one opens.
            // This avoids the "I unplugged the Arduino, replugged on a
            // different USB port, and now COM8 is stale" problem completely.
            string[] osPorts;
            try { osPorts = SerialPort.GetPortNames(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[EegMarkerEmitter] SerialPort.GetPortNames failed: {e.Message}");
                osPorts = Array.Empty<string>();
            }

            var candidates = new List<string>(osPorts.Length + 1);
            if (!string.IsNullOrEmpty(m_ComPort) && Array.Exists(osPorts, p => string.Equals(p, m_ComPort, StringComparison.OrdinalIgnoreCase)))
                candidates.Add(m_ComPort);
            foreach (var p in osPorts)
            {
                if (!candidates.Exists(c => string.Equals(c, p, StringComparison.OrdinalIgnoreCase)))
                    candidates.Add(p);
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[EegMarkerEmitter] No serial ports detected by the OS. Arduino trigger disabled this session.");
                return;
            }

            Debug.Log($"[EegMarkerEmitter] Auto-detecting Arduino across {candidates.Count} port(s): " +
                      string.Join(", ", candidates));

            foreach (var candidate in candidates)
            {
                if (m_Arduino.Open(candidate, m_BaudRate))
                {
                    m_ResolvedComPort = candidate;
                    Debug.Log($"[EegMarkerEmitter] Arduino opened on {candidate}.");
                    return;
                }
            }

            Debug.LogWarning("[EegMarkerEmitter] Auto-detect tried every available port and none opened. Trigger pulses will be skipped this session.");
        }

        void CloseSerialPort()
        {
            m_Arduino?.Close();
            m_Arduino = null;
        }

        public void EndSession()
        {
            if (!m_SessionOpen)
                return;

            Emit("session_end");

            m_CsvWriter?.Close();
            m_CsvWriter = null;

            CloseSerialPort();

            m_SessionOpen = false;
        }

        void OnDestroy()
        {
            EndSession();
        }

        private int GetTriggerCode(
            string eventCode,
            string category = "",
            string received = "",
            string extra = "",
            string trialId = "")
        {
            // Dynamic trigger codes
            if (eventCode is "trial_spawn" or "pps_trial_start" or "pps_trial_end")
            {
                if (int.TryParse(extra, out int customCode))
                {
                    Debug.LogWarning($"[ArduinoTrigger] Event '{eventCode}' associated with '{customCode}'");
                    return customCode;
                }

                Debug.LogWarning($"[ArduinoTrigger] Dynamic event '{eventCode}' missing valid extra → 159");
                return 159;
            }

            switch (eventCode)
            {
                // Trigger Test
                case "test_trigger":                return 61;

                // Task 1 - PPS
                case "pps_session_start":           return 61;
                case "pps_session_end":             return 61;
                case "pps_vib_fired":               return 64;
                case "pps_response":                return 62;
                case "pps_block_paused":            return 61;
                case "pps_block_resumed":           return 61;
                case "pps_session_paused":          return 61;

                // Task 2 - Session
                case "session_start":               return 50;
                case "phase_intro":                 return 51;
                case "phase_practice":              return 52;
                case "phase_rest":                  return 53;
                case "phase_outro":                 return 54;
                case "session_paused":              return 55;
                case "session_resumed":             return 56;
                case "session_end":                 return 57;

                // Task 2 - Blocks
                case "phase_block":                 return 25;
                case "trial_block_start":           return 26;
                case "block_restart":               return 27;
                case "block_paused":                return 28;
                case "trial_timeout":               return 29;
                case "block_end":                   return 30;
                case "trial_too_slow":              return 31;
                case "phase_controller_practice":   return 32;
                case "phase_ball_demo":             return 33;
                case "phase_easy_practice":         return 34;
                case "phase_difficult_practice":    return 35;

                // Controller responses
                case "controller_left":             return 40;
                case "controller_right":            return 41;
                case "trial_no_response":           return 42;

                default:
                    Debug.LogWarning($"[ArduinoTrigger] Unknown event: '{eventCode}' → 61");
                    return 61;
            }
        }
    }
}


// using System;
// using System.Collections;
// using System.IO;
// using System.IO.Ports;
// using System.Text;
// using UnityEngine;

// namespace HitOrMiss
// {
//     /// <summary>
//     /// Sends EEG trigger codes to the Python trigger_server.py over a local TCP
//     /// socket, and logs every event to a CSV file.
//     ///
//     /// Python owns the serial port to the Arduino exclusively.
//     /// Unity never opens a COM port.
//     ///
//     /// ── Setup ────────────────────────────────────────────────────────────────────
//     ///  1. Start trigger_server.py BEFORE hitting Play in Unity.
//     ///  2. Attach this script to any persistent GameObject.
//     ///  3. Call  Emit("trial_start")  from any other script.
//     /// </summary>
//     public class EegMarkerEmitter : MonoBehaviour
//     {
//         // ── Inspector ─────────────────────────────────────────────────────────────

//         [Header("Participant")]
//         [SerializeField] string m_ParticipantId = "P000";

//         [Header("Arduino Serial Trigger")]
//         [Tooltip("Enable to send trigger bytes over serial to an Arduino")]
//         [SerializeField] bool m_UseSerialBridge = true;

//         [Tooltip("COM port the Arduino is on (e.g. COM3 on Windows, /dev/ttyUSB0 on Linux)")]
//         [SerializeField] string m_ComPort = "COM3";

//         [Tooltip("Baud rate — must match the Arduino sketch")]
//         [SerializeField] int m_BaudRate = 115200;

//         [Tooltip("How long (in seconds) the trigger byte is held before sending 0. 0.01 = 10 ms is usually sufficient.")]
//         [SerializeField] float m_TriggerDuration = 0.01f;

//         // ── Private state ─────────────────────────────────────────────────────────

//         StreamWriter  m_CsvWriter;
//         string        m_LogPath;
//         ArduinoTrigger m_Arduino;

//         // ── Public runtime properties ──────────────────────────────────────────

//         public string ComPort
//         {
//             get => m_ComPort;
//             set => m_ComPort = value;
//         }

//         public int BaudRate
//         {
//             get => m_BaudRate;
//             set => m_BaudRate = value;
//         }
//         public bool UseSerialBridge
//         {
//             get => m_UseSerialBridge;
//             set => m_UseSerialBridge = value;
//         }

//         // ── Public API ────────────────────────────────────────────────────────────

//         public void BeginSession(string sessionId)
//         {
//             // CSV log
//             string dir = Path.Combine(Application.persistentDataPath, "EEG_Markers");
//             Directory.CreateDirectory(dir);
//             m_LogPath   = Path.Combine(dir, $"{m_ParticipantId}_{sessionId}_markers.csv");
//             m_CsvWriter = new StreamWriter(m_LogPath, append: false);
//             m_CsvWriter.WriteLine("Time,EventCode,TriggerValue");
//             m_CsvWriter.Flush();

//             // Serial port
//             if (m_UseSerialBridge)
//             {
//                 OpenSerialPort();    
//             }
//             if (m_UseSerialBridge)
//             {
//                 Debug.LogWarning($"[EegMarkerEmitter] Serial bridge active -> {m_ComPort} @ {m_BaudRate} baud");    
//             }                 
//         }

//         public void Emit(string eventCode, string trialId = "", string category = "",
//             string expected = "", string received = "", string extra = "",
//             byte triggerByte = 0)
//         {
//             double time = Time.timeAsDouble;

//             var marker = new EegMarker
//             {
//                 engineTime  = time,
//                 eventCode   = eventCode,
//                 trialId     = trialId,
//                 category    = category,
//                 expected    = expected,
//                 received    = received,
//                 extra       = extra,
//                 triggerByte = triggerByte
//             };

//             // 1. Write to local CSV
//             if (m_Writer != null)
//             {
//                 m_Writer.WriteLine(
//                     $"{time:F6},{eventCode},{trialId},{category},{expected},{received},{extra},{triggerByte}");
//                 m_Writer.Flush();
//             }

//             // 2. Send serial trigger pulse (non-blocking coroutine)
//             int triggerValue = GetTriggerCode(eventCode, category, received, extra, trialId);
//             if (m_UseSerialBridge && triggerValue > 0)
//             {
//                 if (m_Arduino == null)
//                 {
//                     Debug.LogWarning("[EegMarkerEmitter] Skipping trigger — Arduino not initialized.");
//                     return;
//                 }
//                 if (triggerValue == 201)
//                 {
//                     m_TriggerDuration = 0.1f;
//                 }

//                 m_Arduino.SendTrigger((byte)triggerValue, m_TriggerDuration);
//             }

//             // 3. Fire C# event for any Unity subscribers
//             MarkerEmitted?.Invoke(marker);
//         }

//         void OpenSerialPort()
//         {
//             if (m_Arduino != null) return;
//             m_Arduino = gameObject.AddComponent<ArduinoTrigger>();
//             m_Arduino.Open(m_ComPort, m_BaudRate);
//         }

//         void CloseSerialPort()
//         {
//             m_Arduino?.Close();
//             m_Arduino = null;
//         }

//         public void EndSession()
//         {
//             Emit("session_end");

//             m_CsvWriter?.Close();
//             m_CsvWriter = null;

//             CloseTcp();
//         }

//         // ── Unity lifecycle ───────────────────────────────────────────────────────

//         void OnDestroy()
//         {
//             EndSession();
//         }

//         private int GetTriggerCode(
//             string eventCode,
//             string category = "",
//             string received = "",
//             string extra = "",
//             string trialId = "")
//         {
//             // Dynamic trigger codes
//             if (eventCode is "trial_spawn" or "pps_trial_start" or "pps_trial_end")
//                 {
//                     if (int.TryParse(extra, out int customCode))
//                     {
//                         Debug.LogWarning($"[ArduinoTrigger]  event '{eventCode}' associated with '{customCode}'");
//                         return customCode;
//                     }
//                     else
//                     {
//                         Debug.LogWarning($"[ArduinoTrigger] Dynamic event '{eventCode}' missing valid 'extra' → 159");
//                         return 159;
//                     }
                    
//                 }

//             // Static trigger codes
//             switch (eventCode)
//             {
//                 // ------------------------------------------------------------------
//                 // Trigger Test
//                 // ------------------------------------------------------------------
//                 case "test_trigger":       return 191;

//                 // ------------------------------------------------------------------
//                 // Task 1
//                 // ------------------------------------------------------------------
//                 case "pps_session_start":  return 140;
//                 case "pps_session_end":    return 141;
//                 case "pps_vib_fired":      return 201; // triggerCode  + vibration with pin 6
//                 case "pps_response":       return 63;
//                 case "pps_block_paused":   return 142;
//                 case "pps_block_resumed":   return 143;
                

//                 // ------------------------------------------------------------------
//                 // Task 2 - Session
//                 // ------------------------------------------------------------------
//                 case "session_start":      return 130;
//                 case "phase_intro":        return 131;
//                 case "phase_practice":     return 132;
//                 case "phase_rest":         return 133;
//                 case "phase_outro":        return 134;
//                 case "session_paused":     return 135;
//                 case "session_resumed":    return 136;
//                 case "session_end":        return 137;

//                 // ------------------------------------------------------------------
//                 // Task 2 - Blocks
//                 // ------------------------------------------------------------------
//                 case "phase_block":        return 25;
//                 case "trial_block_start":  return 26;
//                 case "block_restart":      return 27;
//                 case "block_paused":       return 28;
//                 case "trial_timeout":      return 29;
//                 case "block_end":          return 30;
//                 case "trial_too_slow":     return 31;
//                 case "phase_controller_practice":     return 32;
//                 case "phase_ball_demo":               return 33;
//                 case "phase_easy_practice":           return 34;
//                 case "phase_difficult_practice":      return 35;

//                 // ------------------------------------------------------------------
//                 // Controller responses
//                 // ------------------------------------------------------------------
//                 case "controller_left":    return 40;
//                 case "controller_right":   return 41;
//                 case "trial_no_response":  return 42;

//                 // ------------------------------------------------------------------
//                 // Unknown event
//                 // ------------------------------------------------------------------
//                 default:
//                     Debug.LogWarning(
//                         $"[ArduinoTrigger] Unknown event: '{eventCode}' → 200");
//                     return 159;
//             }
//         }
//     }
// }