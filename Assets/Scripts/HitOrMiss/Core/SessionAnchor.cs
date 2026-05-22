using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Single source-of-truth transform that every "where things appear" system
    /// in a session references — panels, looming origin, practice origin,
    /// fixation crosses, distance layouts. The anchor's position and forward
    /// vector are snapshotted from the player's head at session start, so the
    /// participant only needs to orient themselves once and everything (UI,
    /// stimuli, fixation marks) appears from that direction afterwards.
    ///
    /// Wire usage (PpsAppController / HitOrMissAppController):
    ///   - Drop one SessionAnchor on an empty GameObject in the scene.
    ///   - On session start, call Calibrate(Camera.main.transform).
    ///   - Parent panels, DistanceLayout, TrajectoryTaskManager.SpawnOrigin,
    ///     the standing cross, the floor alignment cross, etc. as children
    ///     of this anchor (or set their world position to anchor.position).
    ///
    /// Once calibrated, the anchor does NOT follow the camera — it is a fixed
    /// world-space frame. This is intentional: panels should stay where the
    /// participant looked when they confirmed they were ready, not chase the
    /// head every frame (no LazyFollow drift).
    /// </summary>
    public class SessionAnchor : MonoBehaviour
    {
        [Tooltip("If true, calibration snaps the anchor to the camera's yaw only — pitch and roll are forced to identity so the anchor's forward stays horizontal (panels won't tilt with the head).")]
        [SerializeField] bool m_LevelYawOnly = true;

        [Tooltip("Optional vertical offset applied to the anchor's Y at calibration. Use this to lift the anchor off the floor (e.g. 0 for floor-aligned, eye height for chest panels).")]
        [SerializeField] float m_AnchorYOffset = 0f;

        public bool IsCalibrated { get; private set; }

        /// <summary>
        /// Snapshot the camera's current position + (optionally yaw-only)
        /// orientation into this anchor. Call once per session, after the
        /// participant has confirmed they're in position.
        /// </summary>
        public void Calibrate(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                Debug.LogWarning("[SessionAnchor] Calibrate called with null camera transform.");
                return;
            }

            Vector3 camPos = cameraTransform.position;
            Vector3 camForward = cameraTransform.forward;

            if (m_LevelYawOnly)
            {
                camForward.y = 0f;
                if (camForward.sqrMagnitude < 1e-4f)
                    camForward = Vector3.forward;
                else
                    camForward.Normalize();
            }

            transform.position = new Vector3(camPos.x, m_AnchorYOffset, camPos.z);
            transform.rotation = Quaternion.LookRotation(camForward, Vector3.up);
            IsCalibrated = true;

            Debug.Log($"[SessionAnchor] Calibrated. pos={transform.position} fwd={transform.forward}");
        }

        /// <summary>
        /// Resets calibration so the next Calibrate call writes fresh values.
        /// Useful when the session restarts or the participant moves.
        /// </summary>
        public void ResetCalibration()
        {
            IsCalibrated = false;
        }
    }
}
