using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace HitOrMiss
{
    /// <summary>
    /// Hand pinch input: left hand pinch = HIT, right hand pinch = MISS.
    /// Uses XR Hands subsystem's updatedHands event for reliable joint data.
    /// </summary>
    public class HandPinchInput : MonoBehaviour, IResponseInputSource
    {
        [Header("Pinch Settings")]
        [SerializeField] float m_PinchThreshold = 0.04f;   // Distance in meters to trigger pinch
        [SerializeField] float m_ReleaseThreshold = 0.06f;  // Distance to release pinch (hysteresis)

        public event Action<ResponseEvent> ResponseReceived;

        bool m_Enabled;
        XRHandSubsystem m_HandSubsystem;
        bool m_LeftPinching;
        bool m_RightPinching;
        bool m_Subscribed;

        static List<XRHandSubsystem> s_HandSubsystems;

        public void Enable()
        {
            m_Enabled = true;
            m_LeftPinching = false;
            m_RightPinching = false;
        }

        public void Disable()
        {
            m_Enabled = false;
            m_LeftPinching = false;
            m_RightPinching = false;
        }

        void Update()
        {
            if (m_HandSubsystem != null && m_HandSubsystem.running)
                return;

            // Keep trying to find and subscribe to the hand subsystem
            m_Subscribed = false;
            m_HandSubsystem = null;

            if (TryGetHandSubsystem(out m_HandSubsystem))
                Subscribe();
        }

        void Subscribe()
        {
            if (m_Subscribed || m_HandSubsystem == null)
                return;

            m_HandSubsystem.updatedHands += OnUpdatedHands;
            m_Subscribed = true;
            Debug.Log("[HandPinchInput] Subscribed to hand tracking subsystem.");
        }

        void Unsubscribe()
        {
            if (!m_Subscribed || m_HandSubsystem == null)
                return;

            m_HandSubsystem.updatedHands -= OnUpdatedHands;
            m_Subscribed = false;
        }

        void OnUpdatedHands(XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if (!m_Enabled) return;

            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
                CheckHand(subsystem.leftHand, Handedness.Left);

            if ((updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
                CheckHand(subsystem.rightHand, Handedness.Right);
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

            if (!isPinching && distance < m_PinchThreshold)
            {
                if (handedness == Handedness.Left)
                    m_LeftPinching = true;
                else
                    m_RightPinching = true;

                Debug.Log($"[HandPinchInput] Pinch detected: {handedness}, distance: {distance:F3}m");

                ResponseReceived?.Invoke(new ResponseEvent
                {
                    rawSource = handedness == Handedness.Left ? "hand_left_pinch" : "hand_right_pinch",
                    command = handedness == Handedness.Left ? SemanticCommand.Hit : SemanticCommand.Miss,
                    confidence = 1f,
                    timestamp = Time.timeAsDouble
                });
            }
            else if (isPinching && distance > m_ReleaseThreshold)
            {
                if (handedness == Handedness.Left)
                    m_LeftPinching = false;
                else
                    m_RightPinching = false;
            }
        }

        static bool TryGetHandSubsystem(out XRHandSubsystem handSubsystem)
        {
            s_HandSubsystems ??= new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(s_HandSubsystems);
            if (s_HandSubsystems.Count == 0)
            {
                handSubsystem = default;
                return false;
            }

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

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
            Disable();
        }
    }
}
