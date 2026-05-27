using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Positions a pre-placed crosshair according to the PpsTaskAsset, and
    /// exposes simple Show/Hide methods so the panel sequence can control
    /// when the fixation cross is visible.
    /// </summary>
    public class CrosshairController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PpsTaskAsset m_Asset;

        [Tooltip("The crosshair GameObject already placed in the scene " +
                 "(e.g. ARObjects_Task1 → CrosshairPrefab).")]
        [SerializeField] GameObject m_Crosshair;

        void Awake()
        {
            ApplyPosition();
            Show(); // start hidden; PositioningPanel turns it on
        }

        public void Show()
        {
            if (m_Crosshair == null) return;
            ApplyPosition();
            m_Crosshair.SetActive(true);
        }

        public void Hide()
        {
            if (m_Crosshair != null)
                m_Crosshair.SetActive(false);
        }

        void ApplyPosition()
        {
            if (m_Asset == null || m_Crosshair == null) return;
            Debug.Log($"[Crosshair] Placing at local ({0}, {m_Asset.CrosshairHeight}, {m_Asset.CrosshairDistance})");

            // Position relative to whatever the crosshair's parent is
            // (ARObjects_Task1, in your case). Use local-space so it tracks
            // the parent if you ever move the rig.
            m_Crosshair.transform.localPosition = new Vector3(
                0f,
                m_Asset.CrosshairHeight,
                m_Asset.CrosshairDistance
            );
            m_Crosshair.transform.localRotation = Quaternion.identity;
        }

        // Re-apply in editor when the asset changes
        void OnValidate()
        {
            if (Application.isPlaying) return;
            ApplyPosition();
        }
    }
}