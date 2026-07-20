using UnityEngine;

public class ChestAnchorCalibrator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform chestAnchor;
    [SerializeField] private Transform distanceLayout;

    [Header("Headset-to-chest offsets")]
    [SerializeField] private float chestDownOffset = 0.55f;
    [SerializeField] private float chestForwardOffset = 0.05f;

    [Header("Validation")]
    [SerializeField] private float minimumTrackedHeadHeight = 0.8f;


    public bool IsCalibrated { get; private set; }

    private void Start()
    {
        Debug.Log("[Chest Calibration] Start called.", this);
        CalibrateChestAnchor();
    }
    
    public void CalibrateChestAnchor()
    {
        Debug.Log("[Chest Calibration] Calibration requested.", this);

        if (mainCamera == null)
        {
            Debug.LogError(
                "[Chest Calibration] Main Camera is not assigned.",
                this
            );
            return;
        }

        if (xrOrigin == null)
        {
            Debug.LogError(
                "[Chest Calibration] XR Origin is not assigned.",
                this
            );
            return;
        }

        if (chestAnchor == null)
        {
            Debug.LogError(
                "[Chest Calibration] Chest Anchor is not assigned.",
                this
            );
            return;
        }

        Vector3 cameraRelativeToOrigin =
            xrOrigin.InverseTransformPoint(mainCamera.position);

        Debug.Log(
            "[Chest Calibration] Camera relative to XR Origin: " +
            cameraRelativeToOrigin.ToString("F4"),
            this
        );

        if (cameraRelativeToOrigin.y < minimumTrackedHeadHeight)
        {
            Debug.LogWarning(
                "[Chest Calibration] Cancelled because tracking is not ready.\n" +
                "Tracked head height: " +
                cameraRelativeToOrigin.y.ToString("F4") +
                " m\nMinimum required height: " +
                minimumTrackedHeadHeight.ToString("F4") +
                " m",
                this
            );
            return;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(
            mainCamera.forward,
            Vector3.up
        );

        if (flatForward.sqrMagnitude < 0.001f)
        {
            Debug.LogError(
                "[Chest Calibration] Camera forward direction is invalid.",
                this
            );
            return;
        }

        flatForward.Normalize();

        Vector3 calibratedPosition =
            mainCamera.position
            - Vector3.up * chestDownOffset
            + flatForward * chestForwardOffset;

        chestAnchor.SetPositionAndRotation(
            calibratedPosition,
            Quaternion.LookRotation(flatForward, Vector3.up)
        );

        IsCalibrated = true;

        string layoutWorldPosition = distanceLayout != null
            ? distanceLayout.position.ToString("F4")
            : "DistanceLayout not assigned";

        Debug.Log(
            "========== CHEST CALIBRATION COMPLETE ==========\n" +
            "Camera world position: " +
            mainCamera.position.ToString("F4") + "\n" +
            "ChestAnchor world position: " +
            chestAnchor.position.ToString("F4") + "\n" +
            "Chest relative to camera: " +
            mainCamera
                .InverseTransformPoint(chestAnchor.position)
                .ToString("F4") + "\n" +
            "DistanceLayout world position: " +
            layoutWorldPosition,
            this
        );
    }
}