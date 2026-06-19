using UnityEngine;

namespace HitOrMiss.Visuals
{
    /// <summary>
    /// Snaps this transform in front of <see cref="Camera.main"/> every time
    /// the GameObject is enabled. Use it on the root of world-space UI panels
    /// (welcome, instructions, positioning, break, end, etc.) so they always
    /// appear centered on the participant's gaze in VR/MR, no matter where
    /// they were placed in the scene or where the participant happens to be
    /// looking when the panel shows.
    ///
    /// Pairs naturally with the SessionFlowPanels show/hide flow — that
    /// component already SetActives the root, and OnEnable fires every time.
    ///
    /// To pin a panel to a more specific anchor (e.g. a body anchor, the
    /// stationary table, or a custom XR anchor), assign <see cref="m_Anchor"/>
    /// in the inspector. When null, the main camera is used.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnchorToCameraOnEnable : MonoBehaviour
    {
        [Tooltip("Anchor to follow. Leave empty to follow Camera.main automatically.")]
        [SerializeField] Transform m_Anchor;

        [Tooltip("Forward distance in meters from the anchor at which to place the panel. " +
                 "1.0-2.0 m is comfortable for most VR/MR UI.")]
        [SerializeField, Min(0.1f)] float m_Distance = 1.5f;

        [Tooltip("Vertical offset in meters relative to the anchor. " +
                 "Negative values place the panel below eye level (useful for instructions).")]
        [SerializeField] float m_VerticalOffset = 0f;

        [Tooltip("Lateral offset in meters (positive = right of the gaze line). " +
                 "Almost always 0; only set if you want a panel deliberately off-center.")]
        [SerializeField] float m_LateralOffset = 0f;

        [Tooltip("If true, the panel ignores the anchor's pitch so it always stands upright. " +
                 "If false, the panel tilts with the head — useful for floor instructions.")]
        [SerializeField] bool m_KeepUpright = true;

        [Tooltip("If true, also re-anchors every frame so the panel chases the gaze. " +
                 "Default false (snap on Enable only); turn on if the panel should track head movement.")]
        [SerializeField] bool m_RecenterEveryFrame = false;

        void OnEnable()
        {
            Recenter();
        }

        void LateUpdate()
        {
            if (m_RecenterEveryFrame) Recenter();
        }

        /// <summary>
        /// Snap the transform to the configured offset in front of the anchor.
        /// Safe to call manually from elsewhere if the show-flow does not
        /// disable/re-enable the GameObject.
        /// </summary>
        public void Recenter()
        {
            Transform anchor = m_Anchor != null ? m_Anchor : Camera.main != null ? Camera.main.transform : null;
            if (anchor == null) return;

            Vector3 forward = anchor.forward;
            if (m_KeepUpright)
            {
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
                forward.Normalize();
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 position =
                anchor.position
                + forward * m_Distance
                + Vector3.up * m_VerticalOffset
                + right * m_LateralOffset;

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
