using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Positions the fixation crosshair from the PpsTaskAsset, and exposes
    /// Show/Hide so the panel sequence controls when it is visible.
    ///
    /// IMPORTANT: the crosshair must be parented to the SAME anchor as the
    /// looming lights (ChestAnchor). localPosition is relative to the parent, so
    /// parenting it to a rig-level object instead leaves it fixed in the room:
    /// wrong height for every participant, and it neither moves nor turns with
    /// them. This component warns if that wiring looks wrong.
    ///
    /// With the anchor at shoulder height, CrosshairHeight is the eye-to-shoulder
    /// drop — i.e. it puts the cross back at eye level.
    /// </summary>
    public class CrosshairController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PpsTaskAsset m_Asset;

        [Tooltip("The crosshair GameObject. It MUST be a child of the body anchor " +
                 "(ChestAnchor), not of a rig-level object, or it will not follow the participant.")]
        [SerializeField] GameObject m_Crosshair;

        [Tooltip("The body anchor the crosshair should hang from. Used only to verify the " +
                 "parenting is correct; leave empty to skip the check.")]
        [SerializeField] Transform m_ExpectedAnchor;

        [Header("Visibility")]
        [Tooltip("Hide the crosshair on Awake and let the panel sequence show it. " +
                 "The previous version called Show() here despite intending to hide.")]
        [SerializeField] bool m_StartHidden = true;

        void Awake()
        {
            VerifyParenting();
            ApplyPosition();

            if (m_StartHidden) Hide();
            else Show();
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

        void VerifyParenting()
        {
            if (m_Crosshair == null || m_ExpectedAnchor == null) return;

            Transform parent = m_Crosshair.transform.parent;
            bool underAnchor = parent != null && parent.IsChildOf(m_ExpectedAnchor);

            if (!underAnchor)
            {
                Debug.LogError(
                    "[Crosshair] WRONG PARENT: '" + m_Crosshair.name + "' is under '" +
                    (parent != null ? parent.name : "nothing") +
                    "' instead of '" + m_ExpectedAnchor.name + "'.\n" +
                    "Its height, position, and facing will not follow the participant. " +
                    "Reparent it under the anchor and zero its local transform.",
                    this
                );
            }
        }

        void ApplyPosition()
        {
            if (m_Asset == null || m_Crosshair == null) return;

            Vector3 local = new Vector3(0f, m_Asset.CrosshairHeight, m_Asset.CrosshairDistance);

            m_Crosshair.transform.localPosition = local;
            m_Crosshair.transform.localRotation = Quaternion.identity;

            Debug.Log(
                "[Crosshair] Placed at local " + local.ToString("F3") +
                " under '" + (m_Crosshair.transform.parent != null
                    ? m_Crosshair.transform.parent.name : "no parent") +
                "'. World Y = " + m_Crosshair.transform.position.y.ToString("F3") + " m.",
                this
            );
        }

        void OnValidate()
        {
            if (Application.isPlaying) return;
            ApplyPosition();
        }
    }
}