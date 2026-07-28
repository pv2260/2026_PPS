using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace HitOrMiss
{
    /// <summary>
    /// Hand pinch input via XR Hands.
    ///
    /// RIGHT pinch = HIT ("yes, it will hit me").
    /// LEFT pinch = MISS ("no, it will miss me").
    ///
    /// This mapping matches ControllerButtonInput so responses remain
    /// consistent whether participants use controllers or their hands.
    /// </summary>
    public class HandPinchInput : MonoBehaviour, IResponseInputSource
    {
        [Header("Pinch thresholds (thumb-tip ↔ index-tip distance, meters)")]
        [Tooltip("Distance at which a pinch is detected.")]
        [SerializeField]
        float m_PinchThreshold = 0.025f;

        [Tooltip("Distance at which a pinch is released. Must be greater than PinchThreshold.")]
        [SerializeField]
        float m_ReleaseThreshold = 0.045f;

        public event Action<ResponseEvent> ResponseReceived;

        bool m_Enabled;
        bool m_Subscribed;
        bool m_LeftPinching;
        bool m_RightPinching;

        XRHandSubsystem m_HandSubsystem;

        static readonly List<XRHandSubsystem> s_HandSubsystems = new();

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
            // Nothing to do while subscribed to a running subsystem.
            if (m_Subscribed &&
                m_HandSubsystem != null &&
                m_HandSubsystem.running)
            {
                return;
            }

            // The previous subsystem stopped, so clean up its subscription.
            if (m_Subscribed)
            {
                Unsubscribe();
            }

            // Keep looking for a running hand-tracking subsystem.
            if (TryGetRunningHandSubsystem(out XRHandSubsystem subsystem))
            {
                m_HandSubsystem = subsystem;
                Subscribe();
            }
        }

        void Subscribe()
        {
            if (m_Subscribed || m_HandSubsystem == null)
            {
                return;
            }

            m_HandSubsystem.updatedHands += OnUpdatedHands;
            m_Subscribed = true;
        }

        void Unsubscribe()
        {
            if (!m_Subscribed)
            {
                return;
            }

            if (m_HandSubsystem != null)
            {
                m_HandSubsystem.updatedHands -= OnUpdatedHands;
            }

            m_Subscribed = false;
            m_HandSubsystem = null;
        }

        void OnUpdatedHands(
            XRHandSubsystem subsystem,
            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
            XRHandSubsystem.UpdateType updateType)
        {
            if (!m_Enabled)
            {
                return;
            }

            // Evaluate during Dynamic only to avoid duplicate responses
            // from Dynamic and BeforeRender updates.
            if (updateType != XRHandSubsystem.UpdateType.Dynamic)
            {
                return;
            }

            if ((updateSuccessFlags &
                 XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0)
            {
                CheckHand(subsystem.leftHand, Handedness.Left);
            }

            if ((updateSuccessFlags &
                 XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0)
            {
                CheckHand(subsystem.rightHand, Handedness.Right);
            }
        }

        void CheckHand(XRHand hand, Handedness handedness)
        {
            if (!hand.isTracked)
            {
                return;
            }

            XRHandJoint thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);
            XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);

            if (!thumbTip.TryGetPose(out Pose thumbPose) ||
                !indexTip.TryGetPose(out Pose indexPose))
            {
                return;
            }

            float distance = Vector3.Distance(
                thumbPose.position,
                indexPose.position);

            bool isLeft = handedness == Handedness.Left;
            bool isPinching = isLeft
                ? m_LeftPinching
                : m_RightPinching;

            if (!isPinching && distance < m_PinchThreshold)
            {
                if (isLeft)
                {
                    m_LeftPinching = true;
                }
                else
                {
                    m_RightPinching = true;
                }

                ResponseReceived?.Invoke(new ResponseEvent
                {
                    rawSource = isLeft
                        ? "hand_left_pinch"
                        : "hand_right_pinch",

                    command = isLeft
                        ? SemanticCommand.Miss
                        : SemanticCommand.Hit,

                    confidence = 1f,
                    timestamp = Time.timeAsDouble
                });
            }
            else if (isPinching && distance > m_ReleaseThreshold)
            {
                if (isLeft)
                {
                    m_LeftPinching = false;
                }
                else
                {
                    m_RightPinching = false;
                }
            }
        }

        static bool TryGetRunningHandSubsystem(
            out XRHandSubsystem handSubsystem)
        {
            s_HandSubsystems.Clear();
            SubsystemManager.GetSubsystems(s_HandSubsystems);

            for (int i = 0; i < s_HandSubsystems.Count; i++)
            {
                XRHandSubsystem subsystem = s_HandSubsystems[i];

                if (subsystem != null && subsystem.running)
                {
                    handSubsystem = subsystem;
                    return true;
                }
            }

            handSubsystem = null;
            return false;
        }

        void OnValidate()
        {
            if (m_PinchThreshold <= 0f)
            {
                m_PinchThreshold = 0.005f;
            }

            if (m_ReleaseThreshold <= m_PinchThreshold)
            {
                m_ReleaseThreshold = m_PinchThreshold + 0.01f;
            }
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