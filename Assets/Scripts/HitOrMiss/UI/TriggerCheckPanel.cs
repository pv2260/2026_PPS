using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss
{
    /// <summary>
    /// Shared panel (both tasks) that sends a known "test trigger" code to
    /// the EEG/EMG marker stream so the clinician can verify the recording
    /// chain before any task data is collected. Spec calls for this between
    /// the Welcome and Instructions panels.
    ///
    /// Behavior:
    ///   - "Send Test Trigger" button emits a string marker
    ///     <c>test_trigger</c> and the numeric BCD code
    ///     <see cref="TriggerEncoder.TestTrigger"/> (95).
    ///   - Status text below the button confirms the emission.
    ///   - "Continue" advances the popup flow; "Stop" cancels the session.
    ///
    /// Designed to slot into the controller's
    /// <c>m_PrePracticePopups[]</c> array. It implements
    /// <see cref="TaskPopupPanel"/>'s <see cref="PopupBehavior.WaitForButton"/>
    /// flow but adds the test-trigger button as a sibling control.
    /// </summary>
    [RequireComponent(typeof(TaskPopupPanel))]
    public class TriggerCheckPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] EegMarkerEmitter m_MarkerEmitter;
        [SerializeField] Button m_SendTestTriggerButton;
        [SerializeField] TMP_Text m_StatusText;
        [SerializeField] Button m_StopButton;
        [SerializeField] HitOrMissAppController m_AppController;

        [Header("Localization keys (resolved against the active term table)")]
        [SerializeField] string m_StatusReadyKey = "trigger_check_ready";
        [SerializeField] string m_StatusReadyFallback = "Press 'Send Test Trigger' to verify the EEG/EMG link.";
        [SerializeField] string m_StatusSentKey = "trigger_check_sent";
        [SerializeField] string m_StatusSentFallback = "Test trigger sent (code 95). Confirm reception, then press Continue.";

        [Tooltip("Optional: how many test triggers may be sent in this session before further presses are ignored. 0 = unlimited.")]
        [SerializeField] int m_MaxTestSends = 0;

        int m_SendCount;

        void Awake()
        {
            if (m_SendTestTriggerButton != null)
                m_SendTestTriggerButton.onClick.AddListener(OnSendTestTrigger);
            if (m_StopButton != null)
                m_StopButton.onClick.AddListener(OnStopPressed);
            ResetStatus();
        }

        void OnDestroy()
        {
            if (m_SendTestTriggerButton != null)
                m_SendTestTriggerButton.onClick.RemoveListener(OnSendTestTrigger);
            if (m_StopButton != null)
                m_StopButton.onClick.RemoveListener(OnStopPressed);
        }

        void OnEnable() => ResetStatus();

        void ResetStatus()
        {
            m_SendCount = 0;
            if (m_StatusText != null)
                m_StatusText.text = m_StatusReadyFallback;
        }

        void OnSendTestTrigger()
        {
            if (m_MaxTestSends > 0 && m_SendCount >= m_MaxTestSends) return;
            m_SendCount++;
            int code = TriggerEncoder.TestTrigger;
            // String marker for human-readable log + BCD as the numeric payload.
            m_MarkerEmitter?.Emit("test_trigger", "", "", "", extra: code.ToString());
            if (m_StatusText != null)
                m_StatusText.text = $"{m_StatusSentFallback}  ({m_SendCount} sent)";
            Debug.Log($"[TriggerCheckPanel] Test trigger emitted (code={code}, count={m_SendCount}).");
        }

        void OnStopPressed()
        {
            Debug.Log("[TriggerCheckPanel] Stop pressed — cancelling session.");
            if (m_AppController != null) m_AppController.StopSession();
        }
    }
}
