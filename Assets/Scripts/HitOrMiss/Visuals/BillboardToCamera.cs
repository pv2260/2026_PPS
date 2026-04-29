using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Keeps a transform (typically a crosshair quad) facing the main camera.
    /// Drop this on the crosshair child of the spawn origin so the participant
    /// always sees the sight head-on, regardless of head rotation.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class BillboardToCamera : MonoBehaviour
    {
        [Tooltip("If empty, uses Camera.main at runtime.")]
        [SerializeField] Transform m_Camera;

        [Tooltip("Lock vertical rotation so the crosshair stays upright.")]
        [SerializeField] bool m_LockUpright = true;

        void LateUpdate()
        {
            var cam = m_Camera != null ? m_Camera : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;

            Vector3 toCam = cam.position - transform.position;
            if (m_LockUpright) toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }
    }
}
