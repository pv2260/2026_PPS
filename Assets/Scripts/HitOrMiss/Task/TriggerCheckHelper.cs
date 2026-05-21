using UnityEngine;
using HitOrMiss; // Matches your project namespace

public class TriggerCheckHelper : MonoBehaviour
{
    // Drag your EegMarkerEmitter from HitOrMissComponents here in the inspector
    [SerializeField] private EegMarkerEmitter m_EegMarkerEmitter; 

    public void SendTestTrigger()
    {
        if (m_EegMarkerEmitter != null)
        {
            // Emits a specific code to confirm the connection is physically active
            m_EegMarkerEmitter.Emit("test_trigger_check", "test_val", "0");
            Debug.Log("[Trigger Check] Test trigger sent to EEG/EMG system.");
        }
        else
        {
            Debug.LogError("[Trigger Check] EegMarkerEmitter reference is missing!");
        }
    }
}