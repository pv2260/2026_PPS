using UnityEngine;
using UnityEngine.XR;

namespace HitOrMiss.Cybersickness
{
    public class CyberTriggerInputRouter : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] CybersicknessAppController m_AppController;
        [SerializeField] CybersicknessTaskManager m_TaskManager;

        [Header("Threshold")]
        [SerializeField] float m_PressThreshold = 0.5f;

        bool m_LeftWasPressed;
        bool m_RightWasPressed;

        InputDevice m_LeftDevice;
        InputDevice m_RightDevice;

        void Update()
        {
            UpdateDevicesIfNeeded();

            CheckLeftTrigger();
            CheckRightTrigger();
        }

        void UpdateDevicesIfNeeded()
        {
            if (!m_LeftDevice.isValid)
            {
                m_LeftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

                if (m_LeftDevice.isValid)
                    Debug.LogError($"[XR INPUT] Left controller found: {m_LeftDevice.name}");
            }

            if (!m_RightDevice.isValid)
            {
                m_RightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

                if (m_RightDevice.isValid)
                    Debug.LogError($"[XR INPUT] Right controller found: {m_RightDevice.name}");
            }
        }

        void CheckLeftTrigger()
        {
            if (!m_LeftDevice.isValid)
                return;

            if (!m_LeftDevice.TryGetFeatureValue(CommonUsages.trigger, out float value))
                return;

            bool pressedNow = value >= m_PressThreshold;

            if (pressedNow && !m_LeftWasPressed)
            {
                Debug.LogError($"[XR INPUT] LEFT real trigger pressed. value={value}");

                // Used during trigger demo / practice popups.
                m_AppController?.PracticePressYes();

                // Used during actual fish trials.
                m_TaskManager?.PressYes();
            }

            m_LeftWasPressed = pressedNow;
        }

        void CheckRightTrigger()
        {
            if (!m_RightDevice.isValid)
                return;

            if (!m_RightDevice.TryGetFeatureValue(CommonUsages.trigger, out float value))
                return;

            bool pressedNow = value >= m_PressThreshold;

            if (pressedNow && !m_RightWasPressed)
            {
                Debug.LogError($"[XR INPUT] RIGHT real trigger pressed. value={value}");

                // Used during trigger demo / practice popups.
                m_AppController?.PracticePressNo();

                // Used during actual fish trials.
                m_TaskManager?.PressNo();
            }

            m_RightWasPressed = pressedNow;
        }
    }
}