using System.Collections;
using UnityEngine;

/// <summary>
/// Places the body anchor that every PPS stimulus is measured from.
///
/// The anchor is derived from the headset, so it follows the participant:
/// their height, where they stand, and which way they face. That is what makes
/// the layout per-subject without anyone typing a measurement.
///
/// The anchor sits at SHOULDER height by design. EyeToAnchorDropMeters is the
/// vertical distance from the eyes (the headset) down to the acromion, so with
/// PpsTaskAsset.LedHeight = 0 the looming lights sit exactly at shoulder level,
/// and PpsTaskAsset.CrosshairHeight = EyeToAnchorDropMeters puts the fixation
/// cross back at eye level.
///
/// Calibration retries until head tracking is live. The old version tried once
/// in Start and gave up silently, which left the anchor at its editor transform
/// of (0,0,0) — the floor — and made every stimulus appear at floor height.
/// </summary>
public class ChestAnchorCalibrator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform chestAnchor;
    [SerializeField] private Transform distanceLayout;

    [Header("Anchor placement")]
    [Tooltip("Vertical distance from the EYES (headset) down to the shoulder, in meters. " +
             "The anchor is placed this far below the headset, so it lands at shoulder height. " +
             "Adult acromion is roughly 0.22-0.28 m below eye level; 0.25 is a good default. " +
             "This is the only anthropometric assumption in the layout.")]
    [SerializeField] private float eyeToAnchorDropMeters = 0.25f;

    [Tooltip("Forward offset from the headset to the anchor, in meters. Keeps the anchor " +
             "roughly over the body rather than at the face.")]
    [SerializeField] private float anchorForwardOffsetMeters = 0.08f;

    [Header("Validation")]
    [Tooltip("Calibration is refused below this tracked head height. Guards against running " +
             "with an untracked headset reporting y near 0.")]
    [SerializeField] private float minimumTrackedHeadHeight = 0.8f;

    [Tooltip("How long to keep retrying before giving up and logging an error, in seconds.")]
    [SerializeField] private float trackingWaitTimeoutSeconds = 20f;

    public bool IsCalibrated { get; private set; }

    /// <summary>Eye-to-shoulder drop in use. Read this instead of hard-coding 0.25.</summary>
    public float EyeToAnchorDropMeters => eyeToAnchorDropMeters;

    private void Start()
    {
        StartCoroutine(CalibrateWhenTrackingReady());
    }

    /// <summary>
    /// Retries until the headset reports a plausible head height. Call
    /// CalibrateChestAnchor() directly to force a recalibration later, e.g. at
    /// session start once the participant is standing in position.
    /// </summary>
    private IEnumerator CalibrateWhenTrackingReady()
    {
        float deadline = Time.time + trackingWaitTimeoutSeconds;

        while (Time.time < deadline)
        {
            if (CalibrateChestAnchor())
                yield break;

            yield return new WaitForSeconds(0.25f);
        }

        Debug.LogError(
            "[Chest Calibration] FAILED after " +
            trackingWaitTimeoutSeconds.ToString("F0") +
            " s: head tracking never reached " +
            minimumTrackedHeadHeight.ToString("F2") +
            " m.\n" +
            "The anchor is still at its editor position, so the lights and crosshair " +
            "will be at the WRONG HEIGHT (likely floor level). Do not collect data " +
            "until this calibrates. Put the headset on and press Play again.",
            this
        );
    }

    /// <summary>
    /// Places the anchor. Returns true on success, false if tracking is not ready
    /// yet or a reference is missing.
    /// </summary>
    public bool CalibrateChestAnchor()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[Chest Calibration] Main Camera is not assigned.", this);
            return false;
        }

        if (xrOrigin == null)
        {
            Debug.LogError("[Chest Calibration] XR Origin is not assigned.", this);
            return false;
        }

        if (chestAnchor == null)
        {
            Debug.LogError("[Chest Calibration] Chest Anchor is not assigned.", this);
            return false;
        }

        Vector3 cameraRelativeToOrigin = xrOrigin.InverseTransformPoint(mainCamera.position);

        // Not an error while we are still waiting for tracking — the coroutine retries.
        if (cameraRelativeToOrigin.y < minimumTrackedHeadHeight)
            return false;

        Vector3 flatForward = Vector3.ProjectOnPlane(mainCamera.forward, Vector3.up);

        if (flatForward.sqrMagnitude < 0.001f)
            return false;

        flatForward.Normalize();

        Vector3 calibratedPosition =
            mainCamera.position
            - Vector3.up * eyeToAnchorDropMeters
            + flatForward * anchorForwardOffsetMeters;

        chestAnchor.SetPositionAndRotation(
            calibratedPosition,
            Quaternion.LookRotation(flatForward, Vector3.up)
        );

        IsCalibrated = true;

        float eyeHeight = cameraRelativeToOrigin.y;
        float anchorHeight = xrOrigin.InverseTransformPoint(chestAnchor.position).y;

        Debug.Log(
            "========== ANCHOR CALIBRATION COMPLETE ==========\n" +
            "Eye height above floor:      " + eyeHeight.ToString("F3") + " m\n" +
            "Eye-to-shoulder drop:        " + eyeToAnchorDropMeters.ToString("F3") + " m\n" +
            "Anchor (shoulder) height:    " + anchorHeight.ToString("F3") + " m\n" +
            "Anchor world position:       " + chestAnchor.position.ToString("F3") + "\n" +
            "DistanceLayout world pos:    " +
            (distanceLayout != null ? distanceLayout.position.ToString("F3") : "not assigned") + "\n" +
            "Lights sit at anchor height + PpsTaskAsset.LedHeight.\n" +
            "Crosshair sits at anchor height + PpsTaskAsset.CrosshairHeight.",
            this
        );

        return true;
    }
}