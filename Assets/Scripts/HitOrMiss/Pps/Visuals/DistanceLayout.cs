using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Scene-authored layout of the four distance stages along the floor.
    /// Holds transforms for D4..D1 and exposes helpers to convert a [0..1] loom
    /// progress value into the <see cref="DistanceStage"/> the pair is currently in.
    ///
    /// Place this component on an empty GameObject that is a child of the
    /// participant's body-centered reference (typically the XR Origin), so the
    /// layout stays registered with the participant in AR.
    /// </summary>
    public class DistanceLayout : MonoBehaviour
    {
        [SerializeField] Transform m_D4;
        [SerializeField] Transform m_D3;
        [SerializeField] Transform m_D2;
        [SerializeField] Transform m_D1;

        public Transform D4 => m_D4;
        public Transform D3 => m_D3;
        public Transform D2 => m_D2;
        public Transform D1 => m_D1;

        /// <summary>
        /// If no transforms are authored, generate them procedurally from the asset.
        /// Call once at startup after the participant is positioned.
        /// </summary>
        public void ConfigureFromAsset(PpsTaskAsset asset)
        {
            if (asset == null) return;

            EnsureChild(ref m_D4, "D4", asset.DistanceD4, asset.LedHeight);
            EnsureChild(ref m_D3, "D3", asset.DistanceD3, asset.LedHeight);
            EnsureChild(ref m_D2, "D2", asset.DistanceD2, asset.LedHeight);
            EnsureChild(ref m_D1, "D1", asset.DistanceD1, asset.LedHeight);
        }

        /// <summary>
        /// Map normalized loom progress t ∈ [0,1] (D4 → D1) onto the current stage.
        /// Uses fixed quartile thresholds; override if the asset defines non-uniform
        /// stage boundaries in the future.
        /// </summary>
        public DistanceStage StageAt(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.25f) return DistanceStage.D4;
            if (t < 0.50f) return DistanceStage.D3;
            if (t < 0.75f) return DistanceStage.D2;
            return DistanceStage.D1;
        }

        public Vector3 StartCenter => m_D4 != null ? m_D4.position : transform.position + transform.forward * 2f;
        public Vector3 EndCenter => m_D1 != null ? m_D1.position : transform.position + transform.forward * 0.6f;

        void EnsureChild(ref Transform slot, string childName, float forwardDistance, float height)
        {
            if (slot != null) return;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height, forwardDistance);
            slot = go.transform;
        }
    }
}
