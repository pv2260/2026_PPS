using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Body-centered layout of the distance stages D7..D1.
    ///
    /// The user only enters:
    ///     - LoomStartDistance = D7 / farthest point
    ///     - LoomEndDistance   = D1 / nearest point
    ///     - DistanceStageCount
    ///
    /// Intermediate positions are computed automatically by PpsTaskAsset.
    ///
    /// Place this on an empty GameObject parented to a body/XR anchor.
    /// Stages auto-configure on Awake and can be reconfigured from the context menu.
    /// </summary>
    public class DistanceLayout : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Asset providing LoomStartDistance, LoomEndDistance, DistanceStageCount, and LedHeight.")]
        [SerializeField] PpsTaskAsset m_Asset;

        [Header("Stage transforms (auto-created if null)")]
        [SerializeField] Transform m_D7;
        [SerializeField] Transform m_D6;
        [SerializeField] Transform m_D5;
        [SerializeField] Transform m_D4;
        [SerializeField] Transform m_D3;
        [SerializeField] Transform m_D2;
        [SerializeField] Transform m_D1;

        public Transform D7 => m_D7;
        public Transform D6 => m_D6;
        public Transform D5 => m_D5;
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
        /// Position D7..D1 as local children of this transform.
        ///
        /// Forward axis = local +Z.
        /// Vertical axis = local +Y at LedHeight.
        ///
        /// D7 is the farthest/start position.
        /// D1 is the nearest/end position.
        ///
        /// Intermediate stage distances are computed automatically from
        /// LoomStartDistance, LoomEndDistance, and DistanceStageCount.
        /// </summary>
        public void ConfigureFromAsset(PpsTaskAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("[DistanceLayout] Cannot configure: asset is null.");
                return;
            }

            m_Asset = asset;

            EnsureChild(ref m_D7, "D7", asset.DistanceForStage(DistanceStage.D7), asset.LedHeight);
            EnsureChild(ref m_D6, "D6", asset.DistanceForStage(DistanceStage.D6), asset.LedHeight);
            EnsureChild(ref m_D5, "D5", asset.DistanceForStage(DistanceStage.D5), asset.LedHeight);
            EnsureChild(ref m_D4, "D4", asset.DistanceForStage(DistanceStage.D4), asset.LedHeight);
            EnsureChild(ref m_D3, "D3", asset.DistanceForStage(DistanceStage.D3), asset.LedHeight);
            EnsureChild(ref m_D2, "D2", asset.DistanceForStage(DistanceStage.D2), asset.LedHeight);
            EnsureChild(ref m_D1, "D1", asset.DistanceForStage(DistanceStage.D1), asset.LedHeight);

            Debug.Log(
                $"[DistanceLayout] Configured from asset '{asset.name}' | " +
                $"start(D7)={asset.LoomStartDistance:F3}m | " +
                $"end(D1)={asset.LoomEndDistance:F3}m | " +
                $"stageCount={asset.DistanceStageCount} | " +
                $"D7={asset.DistanceForStage(DistanceStage.D7):F3} | " +
                $"D6={asset.DistanceForStage(DistanceStage.D6):F3} | " +
                $"D5={asset.DistanceForStage(DistanceStage.D5):F3} | " +
                $"D4={asset.DistanceForStage(DistanceStage.D4):F3} | " +
                $"D3={asset.DistanceForStage(DistanceStage.D3):F3} | " +
                $"D2={asset.DistanceForStage(DistanceStage.D2):F3} | " +
                $"D1={asset.DistanceForStage(DistanceStage.D1):F3}"
            );
        }

        /// <summary>
        /// Converts normalized loom progress into a distance stage.
        ///
        /// This mirrors PpsTaskAsset.ProgressForStage().
        ///
        /// With 7 stages:
        ///     D7 = 0.000
        ///     D6 = 0.167
        ///     D5 = 0.333
        ///     D4 = 0.500
        ///     D3 = 0.667
        ///     D2 = 0.833
        ///     D1 = 1.000
        /// </summary>
        public DistanceStage StageAt(float progress)
        {
            progress = Mathf.Clamp01(progress);

            int stageCount = m_Asset != null
                ? Mathf.Clamp(m_Asset.DistanceStageCount, 2, 7)
                : 7;

            int intervalCount = stageCount - 1;

            int index = Mathf.FloorToInt(progress * intervalCount + 0.0001f);
            index = Mathf.Clamp(index, 0, intervalCount);

            return index switch
            {
                0 => DistanceStage.D7,
                1 => DistanceStage.D6,
                2 => DistanceStage.D5,
                3 => DistanceStage.D4,
                4 => DistanceStage.D3,
                5 => DistanceStage.D2,
                _ => DistanceStage.D1,
            };
        }

        public Vector3 StartCenter => m_D7 != null
            ? m_D7.position
            : transform.TransformPoint(new Vector3(0f, 0f, 2.4f));

        public Vector3 EndCenter => m_D1 != null
            ? m_D1.position
            : transform.TransformPoint(new Vector3(0f, 0f, 0.6f));

        void EnsureChild(ref Transform slot, string childName, float forwardDistance, float ledHeight)
        {
            if (slot == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                slot = go.transform;
            }

            slot.localPosition = new Vector3(0f, ledHeight, forwardDistance);
            slot.localRotation = Quaternion.identity;
            slot.localScale = Vector3.one;
        }

        void OnValidate()
        {
            if (!Application.isPlaying && m_Asset != null)
                ConfigureFromAsset(m_Asset);
        }

        [ContextMenu("Force Reconfigure From Asset")]
        public void ForceReconfigureFromAsset()
        {
            if (m_Asset == null)
            {
                Debug.LogError("[DistanceLayout] Cannot reconfigure: m_Asset is null.");
                return;
            }

            ConfigureFromAsset(m_Asset);
            DebugDistanceLayout();
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

            DebugStage("D7", m_D7);
            DebugStage("D6", m_D6);
            DebugStage("D5", m_D5);
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

        void DebugStage(string label, Transform stage)
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