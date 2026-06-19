using System.Collections;
using System.IO;
using System.Text;
using SerialPort = System.IO.Ports.SerialPort;
using UnityEngine;

namespace HitOrMiss
{
    public class ArduinoTrigger : MonoBehaviour
    {
        SerialPort m_SerialPort;

        /// <summary>
        /// Tries to open the given COM port. Returns true on success so the
        /// caller can iterate through candidates when auto-detecting the
        /// Arduino instead of crashing on the first miss.
        /// </summary>
        public bool Open(string comPort, int baudRate)
        {
            try
            {
                m_SerialPort = new SerialPort(comPort, baudRate);
                m_SerialPort.Open();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.Log($"[ArduinoTrigger] {comPort} did not open: {e.Message}");
                m_SerialPort = null;
                return false;
            }
        }

        public void SendTrigger(byte value, float duration)
        {
            StartCoroutine(TriggerCoroutine(value, duration));
        }

        IEnumerator TriggerCoroutine(byte value, float duration)
        {
            SendByte(value, "trigger");
            yield return new WaitForSeconds(duration);
            SendByte(0, "reset");
        }

        void SendByte(byte value, string note = "")
        {
            // Always log to CSV regardless of serial state
            if (m_SerialPort == null || !m_SerialPort.IsOpen)
            {
                Debug.LogWarning($"[ArduinoTrigger] Serial not open — byte {value} logged only");
                return;
            }
            try
            {
                m_SerialPort.Write(new byte[] { value }, 0, 1);
                m_SerialPort.BaseStream.Flush();
                Debug.LogWarning($"[ArduinoTrigger] Sent byte: {value} ({note})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArduinoTrigger] Serial write failed: {e.Message}");
            }
        }

        public void Close()
        {
            SendByte(0, "close");
            m_SerialPort?.Close();
            m_SerialPort = null;
        }

        void OnApplicationQuit() => Close();
        void OnDestroy() => Close();  // ← catches scene stop / editor stop

    }
}