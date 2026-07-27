using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Shows a world-space pause button when the participant turns their head far
    /// enough toward it, and hides it again when they turn back to the task.
    ///
    /// WHAT CHANGED AND WHY
    ///   The button used to be repositioned every frame from the head's CURRENT
    ///   forward, which meant it sat wherever the participant happened to be
    ///   looking and tracked every movement of the neck. It never held still, and
    ///   because it stayed centred in view there was nothing to turn TOWARD, so
    ///   the head-turn gesture and the button's location contradicted each other.
    ///
    ///   The button now sits at a FIXED BEARING from the reference forward, on the
    ///   participant's right by default. The pose is computed once at the moment
    ///   it becomes visible and is then left alone, so it is genuinely static
    ///   while on screen. Turning further, leaning, or postural sway do not move
    ///   it. Turning back past the hide angle removes it.
    ///
    ///   The show gate is now SIGNED. Only a turn toward the button's own side
    ///   counts, so turning away from it no longer summons a button behind the
    ///   participant's head.
    ///
    /// REFERENCE FORWARD
    ///   Captured at Start, which is whenever the scene loads and may be before
    ///   the participant is properly seated and facing the task. Call Recenter()
    ///   once they are in position, or wire it to the start of the session.
    /// </summary>
    public class PpsHeadTurnPauseButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform xrCamera;
        [SerializeField] private GameObject pauseButtonRoot;
        [SerializeField] private GameObject fullPausePanel;

        [Header("Head Turn Detection")]
        [Tooltip("Turn past this angle TOWARD the button's side to show it.")]
        [SerializeField] private float showAngleDegrees = 75f;

        [Tooltip("Turn back inside this angle to hide it. Must stay below the show angle, or the button will flicker on the boundary.")]
        [SerializeField] private float hideAngleDegrees = 55f;

        [Header("Button Placement")]
        [Tooltip("Bearing from the reference forward, in degrees. +90 puts the button squarely on the participant's right, -90 on their left. The show gate follows this sign automatically.")]
        [SerializeField] private float bearingDegrees = 90f;

        [SerializeField] private float distanceFromHead = 1.0f;
        [SerializeField] private float verticalOffset = -0.1f;

        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private Vector3 referenceForward;
        private bool buttonVisible;

        private void Awake()
        {
            if (logDebug)
                Debug.Log("[PpsHeadTurnPauseButton] Awake fired.", this);
        }

        private void Start()
        {
            if (xrCamera == null && Camera.main != null)
                xrCamera = Camera.main.transform;

            if (xrCamera == null)
            {
                Debug.LogError("[PpsHeadTurnPauseButton] No XR camera assigned and no Camera.main found.", this);
                enabled = false;
                return;
            }

            if (pauseButtonRoot == null)
            {
                Debug.LogError("[PpsHeadTurnPauseButton] No pause button root assigned.", this);
                enabled = false;
                return;
            }

            Recenter();
            SetButtonVisible(false);
        }

        /// <summary>
        /// Re-reads the participant's facing as the new task front. Everything
        /// else is measured from this, so call it once the participant is seated
        /// and looking at the task rather than relying on whatever direction the
        /// headset happened to point at scene load.
        /// </summary>
        public void Recenter()
        {
            if (xrCamera == null) return;

            Vector3 flat = Flatten(xrCamera.forward);

            if (flat.sqrMagnitude < 0.001f)
            {
                // Looking straight up or down leaves no horizontal component.
                // The head's up vector is horizontal in exactly that case.
                flat = Flatten(xrCamera.up);
            }

            if (flat.sqrMagnitude < 0.001f)
                flat = Vector3.forward;

            referenceForward = flat.normalized;

            if (logDebug)
                Debug.Log($"[PpsHeadTurnPauseButton] Reference forward set to {referenceForward}.", this);
        }

        private void LateUpdate()
        {
            if (xrCamera == null || pauseButtonRoot == null)
                return;

            // The full panel takes over the participant's attention, so the small
            // access button is redundant while it is open.
            if (fullPausePanel != null && fullPausePanel.activeSelf)
            {
                SetButtonVisible(false);
                return;
            }

            Vector3 headForward = Flatten(xrCamera.forward);

            if (headForward.sqrMagnitude < 0.001f)
                return;

            headForward.Normalize();

            // Positive means the participant has turned to their right. The
            // button's bearing carries the same sign convention, so multiplying
            // by it gives "how far have they turned TOWARD the button", with a
            // turn the other way coming out negative and never passing the gate.
            float signedAngle  = Vector3.SignedAngle(referenceForward, headForward, Vector3.up);
            float towardButton = signedAngle * Mathf.Sign(bearingDegrees);

            if (!buttonVisible && towardButton >= showAngleDegrees)
            {
                if (logDebug)
                    Debug.Log($"[PpsHeadTurnPauseButton] Showing button at {towardButton:F0} deg toward it.", this);

                SetButtonVisible(true);
            }
            else if (buttonVisible && towardButton <= hideAngleDegrees)
            {
                if (logDebug)
                    Debug.Log($"[PpsHeadTurnPauseButton] Hiding button at {towardButton:F0} deg toward it.", this);

                SetButtonVisible(false);
            }

            // Deliberately NOT repositioning here. The pose is set once on show
            // and held. Putting PositionButton() back in this method is what made
            // the button follow the head in the first place.
        }

        /// <summary>
        /// Places the button at its bearing from the reference forward, at the
        /// head's current height and horizontal position. Called only when the
        /// button appears, so the pose is fixed for as long as it is on screen.
        /// </summary>
        private void PositionButton()
        {
            Vector3 headPosition = xrCamera.position;

            Vector3 bearing = Quaternion.Euler(0f, bearingDegrees, 0f) * referenceForward;

            pauseButtonRoot.transform.position =
                headPosition +
                bearing * distanceFromHead +
                Vector3.up * verticalOffset;

            // A world-space Canvas is read from the side its forward points AWAY
            // from, so the button's forward runs outward along the bearing rather
            // than back at the participant.
            pauseButtonRoot.transform.rotation = Quaternion.LookRotation(bearing, Vector3.up);
        }

        private void SetButtonVisible(bool visible)
        {
            buttonVisible = visible;

            if (pauseButtonRoot == null || pauseButtonRoot.activeSelf == visible)
                return;

            // Pose first, then activate, so the button is never drawn for a frame
            // at wherever it was left the last time it was shown.
            if (visible)
                PositionButton();

            pauseButtonRoot.SetActive(visible);
        }

        private Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private void OnValidate()
        {
            // Hysteresis only works if the hide angle is genuinely below the show
            // angle. Equal values put the button on a knife edge and it flickers.
            if (hideAngleDegrees >= showAngleDegrees)
                hideAngleDegrees = Mathf.Max(0f, showAngleDegrees - 10f);
        }
    }
}