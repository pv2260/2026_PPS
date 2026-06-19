using UnityEngine;
using UnityEngine.XR;

namespace HitOrMiss.Pps
{
    public class PPSControllerHaptics : MonoBehaviour
    {
        [Header("UI Button Haptic")]
        [SerializeField, Range(0f, 1f)]
        private float uiAmplitude = 0.2f;

        [SerializeField]
        private float uiDuration = 0.05f;

        [Header("Task Response Haptic")]
        [SerializeField, Range(0f, 1f)]
        private float responseAmplitude = 0.25f;

        [SerializeField]
        private float responseDuration = 0.06f;

        [Header("UI Hover Haptic")]
        [SerializeField, Range(0f, 1f)]
        private float hoverAmplitude = 0.08f;

        [SerializeField]
        private float hoverDuration = 0.025f;

        public void PlayUIHaptic()
        {
            PulseController(XRNode.LeftHand, uiAmplitude, uiDuration);
            PulseController(XRNode.RightHand, uiAmplitude, uiDuration);
        }

        public void PlayHoverHaptic()
        {
            PulseController(XRNode.LeftHand, hoverAmplitude, hoverDuration);
            PulseController(XRNode.RightHand, hoverAmplitude, hoverDuration);
        }

        public void PlayLeftResponseHaptic()
        {
            PulseController(XRNode.LeftHand, responseAmplitude, responseDuration);
        }

        public void PlayRightResponseHaptic()
        {
            PulseController(XRNode.RightHand, responseAmplitude, responseDuration);
        }

        private void PulseController(XRNode hand, float amplitude, float duration)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(hand);

            if (!device.isValid)
                return;

            if (device.TryGetHapticCapabilities(out HapticCapabilities capabilities) &&
                capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }
}