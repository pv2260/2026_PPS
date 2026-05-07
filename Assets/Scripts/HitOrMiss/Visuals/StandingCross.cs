using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// AR positioning marker shown during the PositioningPanel phase.
    /// A simple cross laid flat on the floor at a configurable distance
    /// in front of the XR Origin — the participant stands on top of it
    /// before the experiment begins.
    ///
    /// Authoring options (pick one):
    ///   1. Drop a custom prefab into <see cref="m_CustomPrefab"/>.
    ///   2. Leave the slot empty; the component builds a default cross
    ///      from two thin Quads at scene start.
    ///
    /// Visibility is controlled by the controller — show during the
    /// PositioningPanel phase, hide once the participant has confirmed
    /// they are in position.
    /// </summary>
    public class StandingCross : MonoBehaviour
    {
        [Header("Anchoring")]
        [Tooltip("Reference transform the cross is positioned relative to. Typically the XR Origin.")]
        [SerializeField] Transform m_Anchor;
        [Tooltip("Forward distance from the anchor along its forward axis (meters).")]
        [SerializeField] float m_DistanceMeters = 1.5f;
        [Tooltip("Vertical offset from the anchor's floor plane.")]
        [SerializeField] float m_HeightMeters = 0f;

        [Header("Visual")]
        [Tooltip("Optional custom cross prefab. If unset, a default white cross is built at runtime.")]
        [SerializeField] GameObject m_CustomPrefab;
        [SerializeField] float m_ArmLengthMeters = 0.30f;
        [SerializeField] float m_ArmWidthMeters = 0.04f;
        [SerializeField] Color m_Color = Color.white;

        GameObject m_Visual;

        void Awake()
        {
            EnsureVisual();
            Hide();
        }

        public void Show()
        {
            EnsureVisual();
            PositionVisual();
            if (m_Visual != null) m_Visual.SetActive(true);
        }

        public void Hide()
        {
            if (m_Visual != null) m_Visual.SetActive(false);
        }

        void EnsureVisual()
        {
            if (m_Visual != null) return;

            if (m_CustomPrefab != null)
            {
                m_Visual = Instantiate(m_CustomPrefab, transform);
                m_Visual.name = m_CustomPrefab.name + "(StandingCross)";
                return;
            }

            m_Visual = BuildDefaultCross();
        }

        GameObject BuildDefaultCross()
        {
            var root = new GameObject("DefaultStandingCross");
            root.transform.SetParent(transform, false);

            var horizontal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            horizontal.name = "Horizontal";
            DestroyImmediate(horizontal.GetComponent<Collider>());
            horizontal.transform.SetParent(root.transform, false);
            horizontal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            horizontal.transform.localScale = new Vector3(m_ArmLengthMeters, m_ArmWidthMeters, 1f);

            var vertical = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vertical.name = "Vertical";
            DestroyImmediate(vertical.GetComponent<Collider>());
            vertical.transform.SetParent(root.transform, false);
            vertical.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vertical.transform.localScale = new Vector3(m_ArmWidthMeters, m_ArmLengthMeters, 1f);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                var mat = new Material(unlit) { color = m_Color };
                horizontal.GetComponent<Renderer>().sharedMaterial = mat;
                vertical.GetComponent<Renderer>().sharedMaterial = mat;
            }

            return root;
        }

        void PositionVisual()
        {
            if (m_Visual == null) return;
            Transform anchor = m_Anchor != null ? m_Anchor : transform;
            Vector3 forward = anchor.forward.sqrMagnitude > 0.0001f
                ? anchor.forward.normalized
                : Vector3.forward;
            Vector3 pos = anchor.position + forward * m_DistanceMeters + Vector3.up * m_HeightMeters;
            m_Visual.transform.position = pos;
            // Lay cross flat on the floor by default — ignore anchor pitch.
            m_Visual.transform.rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
        }
    }
}
