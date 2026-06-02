using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Body-centered layout of the four distance stages D4..D1.
    /// Place on an empty GameObject parented to a body anchor (see PpsBodyAnchor),
    /// then assign the PpsTaskAsset. Stages auto-configure on Awake.
    /// </summary>
    public class DistanceLayout : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Asset providing DistanceD4..D1 and LedHeight. Auto-applied on Awake.")]
        [SerializeField] PpsTaskAsset m_Asset;

        [Header("Stage transforms (auto-created if null)")]
        [SerializeField] Transform m_D4;
        [SerializeField] Transform m_D3;
        [SerializeField] Transform m_D2;
        [SerializeField] Transform m_D1;

        public Transform D4 => m_D4;
        public Transform D3 => m_D3;
        public Transform D2 => m_D2;
        public Transform D1 => m_D1;

        void Awake()
        {
            if (m_Asset != null)
                ConfigureFromAsset(m_Asset);

            DebugDistanceLayout();
        }

        /// <summary>
        /// Position D4..D1 as local children of this transform.
        /// Forward axis = local +Z, vertical = local +Y at LedHeight.
        /// </summary>
        public void ConfigureFromAsset(PpsTaskAsset asset)
        {
            if (asset == null) return;
            m_Asset = asset;

            EnsureChild(ref m_D4, "D4", asset.DistanceD4, asset.LedHeight);
            EnsureChild(ref m_D3, "D3", asset.DistanceD3, asset.LedHeight);
            EnsureChild(ref m_D2, "D2", asset.DistanceD2, asset.LedHeight);
            EnsureChild(ref m_D1, "D1", asset.DistanceD1, asset.LedHeight);
        }

        public DistanceStage StageAt(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.25f) return DistanceStage.D4;
            if (t < 0.50f) return DistanceStage.D3;
            if (t < 0.75f) return DistanceStage.D2;
            return DistanceStage.D1;
        }

        public Vector3 StartCenter => m_D4 != null
            ? m_D4.position
            : transform.TransformPoint(new Vector3(0f, 0f, 2f));

        public Vector3 EndCenter => m_D1 != null
            ? m_D1.position
            : transform.TransformPoint(new Vector3(0f, 0f, 0.6f));

        void EnsureChild(ref Transform slot, string childName, float forwardDistance, float ledHeight)
        {
            if (slot == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                slot = go.transform;
            }

            slot.localPosition = new Vector3(0f, ledHeight, forwardDistance);
            slot.localRotation = Quaternion.identity;
            slot.localScale = Vector3.one;
        }

        // Re-apply in the editor when the asset is changed or distances are edited
        void OnValidate()
        {
            if (!Application.isPlaying && m_Asset != null && m_D4 != null)
                ConfigureFromAsset(m_Asset);
        }



        [ContextMenu("Debug Distance Layout")]
        public void DebugDistanceLayout()
        {
            Debug.Log("========== DISTANCE LAYOUT DEBUG ==========");

            Debug.Log(
                $"[DistanceLayout] name={name} | " +
                $"localPosition={transform.localPosition} | " +
                $"worldPosition={transform.position} | " +
                $"localScale={transform.localScale} | " +
                $"lossyScale={transform.lossyScale} | " +
                $"rotation={transform.rotation.eulerAngles}"
            );

            DebugStage("D4", m_D4);
            DebugStage("D3", m_D3);
            DebugStage("D2", m_D2);
            DebugStage("D1", m_D1);

            Debug.Log("========== PARENT CHAIN ==========");
            Transform p = transform.parent;
            while (p != null)
            {
                Debug.Log(
                    $"parent={p.name} | " +
                    $"localPosition={p.localPosition} | " +
                    $"worldPosition={p.position} | " +
                    $"localScale={p.localScale} | " +
                    $"lossyScale={p.lossyScale} | " +
                    $"rotation={p.rotation.eulerAngles}"
                );

                p = p.parent;
            }
        }

        private void DebugStage(string label, Transform stage)
        {
            if (stage == null)
            {
                Debug.LogWarning($"[{label}] is null");
                return;
            }

            float worldDistanceFromLayout =
                Vector3.Distance(transform.position, stage.position);

            Debug.Log(
                $"[{label}] " +
                $"localPosition={stage.localPosition} | " +
                $"worldPosition={stage.position} | " +
                $"worldDistanceFromLayout={worldDistanceFromLayout:F3} | " +
                $"lossyScale={stage.lossyScale}"
            );
        }
    }
}