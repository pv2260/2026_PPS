using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace HitOrMiss
{
    /// <summary>
    /// Hand pinch input via XR Hands. Left pinch = HIT, right pinch = MISS.
    ///
    /// Requirements on a Meta Quest build:
    ///   • Package <c>com.unity.xr.hands</c> installed.
    ///   • OpenXR Feature "Meta Hand Tracking Aim" (or equivalent provider hand-tracking
    ///     feature) enabled in Project Settings → XR Plug-in Management → OpenXR → Android tab.
    ///   • Hand tracking permission in the AndroidManifest (Meta XR SDK adds this automatically
    ///     when the Hand Tracking feature is enabled).
    ///   • Hand tracking enabled at the system level on the Quest.
    ///
    /// If pinches don't fire on device, enable <see cref="m_VerboseLogging"/> and check
    /// adb logcat / the Quest's developer console for the diagnostic lines below.
    /// </summary>
    public class HandPinchInput : MonoBehaviour, IResponseInputSource
    {
        [Header("Pinch thresholds (thumb-tip ↔ index-tip distance, meters)")]
        [Tooltip("Distance at which pinch is detected (closing). Loosen if pinches don't register on device.")]
        [SerializeField] float m_PinchThreshold = 0.025f;
        [Tooltip("Distance at which pinch is released. Must be > PinchThreshold (hysteresis).")]
        [SerializeField] float m_ReleaseThreshold = 0.045f;

        [Header("Diagnostics")]
        [Tooltip("Log subsystem discovery, hand-tracking state, and per-frame pinch distances when verbose.")]
        [SerializeField] bool m_VerboseLogging = true;
        [Tooltip("Throttle distance logs to one every N seconds when verbose.")]
        [SerializeField] float m_DistanceLogIntervalSeconds = 1f;

        public event Action<ResponseEvent> ResponseReceived;

        bool m_Enabled;
        XRHandSubsystem m_HandSubsystem;
        bool m_Subscribed;
        bool m_LeftPinching;
        bool m_RightPinching;

        // Diagnostics state
        float m_NextDistanceLogTime;
        bool m_LoggedNoSubsystem;
        bool m_LoggedNoHandTracking_Left;
        bool m_LoggedNoHandTracking_Right;

        static readonly List<XRHandSubsystem> s_HandSubsystems = new();

        public void Enable()
        {
            m_Enabled = true;
            m_LeftPinching = false;
            m_RightPinching = false;
            if (m_VerboseLogging) Debug.Log("[HandPinchInput] Enable() called. Subsystem subscribed: " + m_Subscribed);
        }

        public void Disable()
        {
            m_Enabled = false;
            m_LeftPinching = false;
            m_RightPinching = false;
        }

        void Update()
        {
            // Re-attempt subscription whenever we're not subscribed OR the subsystem stopped.
            if (m_Subscribed && m_HandSubsystem != null && m_HandSubsystem.running)
                return;

            if (m_Subscribed)
            {
                // Subsystem dropped — clean up before re-attempting.
                Unsubscribe();
            }

            if (TryGetRunningHandSubsystem(out var subsystem))
            {
                m_HandSubsystem = subsystem;
                Subscribe();
                m_LoggedNoSubsystem = false;
            }
            else if (m_VerboseLogging && !m_LoggedNoSubsystem)
            {
                Debug.LogWarning("[HandPinchInput] No running XRHandSubsystem found. " +
                                 "Verify the OpenXR Hand Tracking feature is enabled and the Quest has hand tracking on.");
                m_LoggedNoSubsystem = true;
            }
        }

        void Subscribe()
        {
            if (m_Subscribed || m_HandSubsystem == null) return;
            m_HandSubsystem.updatedHands += OnUpdatedHands;
            m_Subscribed = true;
            if (m_VerboseLogging)
                Debug.Log("[HandPinchInput] Subscribed to XRHandSubsystem (running=" + m_HandSubsystem.running + ").");
        }

        void Unsubscribe()
        {
            if (!m_Subscribed) return;
            if (m_HandSubsystem != null)
                m_HandSubsystem.updatedHands -= OnUpdatedHands;
            m_Subscribed = false;
        }

        void OnUpdatedHands(XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if (!m_Enabled) return;
            // Pinch is normally evaluated in Dynamic, not BeforeRender, to avoid double-fires.
            if (updateType != XRHandSubsystem.UpdateType.Dynamic) return;

            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
                CheckHand(subsystem.leftHand, Handedness.Left);
            else if (m_VerboseLogging && !m_LoggedNoHandTracking_Left)
            {
                Debug.Log("[HandPinchInput] Left hand joints not yet updated.");
                m_LoggedNoHandTracking_Left = true;
            }

            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                CheckHand(subsystem.rightHand, Handedness.Right);
            else if (m_VerboseLogging && !m_LoggedNoHandTracking_Right)
            {
                Debug.Log("[HandPinchInput] Right hand joints not yet updated.");
                m_LoggedNoHandTracking_Right = true;
            }
        }

        void CheckHand(XRHand hand, Handedness handedness)
        {
            if (!hand.isTracked) return;

            var thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
            var indexTip = hand.GetJoint(XRHandJointID.IndexTip);

            if (!thumbTip.TryGetPose(out Pose thumbPose) || !indexTip.TryGetPose(out Pose indexPose))
                return;

            float distance = Vector3.Distance(thumbPose.position, indexPose.position);
            bool isPinching = handedness == Handedness.Left ? m_LeftPinching : m_RightPinching;

            if (m_VerboseLogging && Time.time >= m_NextDistanceLogTime)
            {
                Debug.Log($"[HandPinchInput] {handedness} thumb-index distance: {distance:F3}m " +
                          $"(pinch<{m_PinchThreshold:F3}, release>{m_ReleaseThreshold:F3}, currentlyPinching={isPinching})");
                if (handedness == Handedness.Right)
                    m_NextDistanceLogTime = Time.time + m_DistanceLogIntervalSeconds;
            }

            if (!isPinching && distance < m_PinchThreshold)
            {
                if (handedness == Handedness.Left) m_LeftPinching = true;
                else m_RightPinching = true;

                Debug.Log($"[HandPinchInput] PINCH: {handedness} (distance {distance:F3}m)");

                ResponseReceived?.Invoke(new ResponseEvent
                {
                    rawSource = handedness == Handedness.Left ? "hand_left_pinch" : "hand_right_pinch",
                    command = handedness == Handedness.Left ? SemanticCommand.Hit : SemanticCommand.Miss,
                    confidence = 1f,
                    timestamp = Time.timeAsDouble,
                });
            }
            else if (isPinching && distance > m_ReleaseThreshold)
            {
                if (handedness == Handedness.Left) m_LeftPinching = false;
                else m_RightPinching = false;
            }
        }

        static bool TryGetRunningHandSubsystem(out XRHandSubsystem handSubsystem)
        {
            s_HandSubsystems.Clear();
            SubsystemManager.GetSubsystems(s_HandSubsystems);
            for (int i = 0; i < s_HandSubsystems.Count; i++)
            {
                if (s_HandSubsystems[i].running)
                {
                    handSubsystem = s_HandSubsystems[i];
                    return true;
                }
            }
            handSubsystem = default;
            return false;
        }

        void OnValidate()
        {
            if (m_PinchThreshold <= 0f) m_PinchThreshold = 0.005f;
            if (m_ReleaseThreshold <= m_PinchThreshold) m_ReleaseThreshold = m_PinchThreshold + 0.01f;
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy()
        {
            Unsubscribe();
            Disable();
        }
    }
}
