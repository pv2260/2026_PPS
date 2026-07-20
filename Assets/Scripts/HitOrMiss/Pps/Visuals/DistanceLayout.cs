using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Defines the local positions of distance stages D7 through D1.
    ///
    /// Each stage is positioned relative to this DistanceLayout transform:
    ///
    /// X = 0
    /// Y = PpsTaskAsset.LedHeight
    /// Z = PpsTaskAsset.DistanceForStage(stage)
    ///
    /// No additional positional offset is added by this script.
    ///
    /// Recommended hierarchy:
    ///
    /// XR Origin
    /// ├── Camera Offset
    /// │   └── Main Camera
    /// └── LoomingPair
    ///     ├── LeftLED
    ///     ├── RightLED
    ///     └── DistanceLayout
    ///         ├── D7
    ///         ├── D6
    ///         ├── D5
    ///         ├── D4
    ///         ├── D3
    ///         ├── D2
    ///         └── D1
    /// </summary>
    public class DistanceLayout : MonoBehaviour
    {
        [Header("Configuration")]

        [Tooltip(
            "Asset providing LoomStartDistance, LoomEndDistance, " +
            "DistanceStageCount and LedHeight."
        )]
        [SerializeField]
        private PpsTaskAsset m_Asset;

        [Header("Origin Debug References")]

        [Tooltip(
            "Assign XR Origin (XR Rig). " +
            "This is used only for debugging and measurement."
        )]
        [SerializeField]
        private Transform m_XrOrigin;

        [Tooltip(
            "Assign the tracked Main Camera. " +
            "This is used only for debugging and measurement."
        )]
        [SerializeField]
        private Transform m_MainCamera;

        [Header("Debug")]

        [Tooltip("Print a full distance report when entering Play Mode.")]
        [SerializeField]
        private bool m_DebugOnAwake = true;

        [Tooltip("Log every stage when it receives its asset position.")]
        [SerializeField]
        private bool m_LogStageConfiguration = true;

        [Header("Stage Transforms")]

        [Tooltip("These children are found or created automatically if missing.")]
        [SerializeField] private Transform m_D7;
        [SerializeField] private Transform m_D6;
        [SerializeField] private Transform m_D5;
        [SerializeField] private Transform m_D4;
        [SerializeField] private Transform m_D3;
        [SerializeField] private Transform m_D2;
        [SerializeField] private Transform m_D1;

        public Transform D7 => m_D7;
        public Transform D6 => m_D6;
        public Transform D5 => m_D5;
        public Transform D4 => m_D4;
        public Transform D3 => m_D3;
        public Transform D2 => m_D2;
        public Transform D1 => m_D1;

        public PpsTaskAsset Asset => m_Asset;

        /// <summary>
        /// World-space position of D7.
        /// </summary>
        public Vector3 StartCenter
        {
            get
            {
                if (m_D7 != null)
                    return m_D7.position;

                float height = m_Asset != null
                    ? m_Asset.LedHeight
                    : 0f;

                float distance = m_Asset != null
                    ? m_Asset.LoomStartDistance
                    : 3.5f;

                return transform.TransformPoint(
                    new Vector3(0f, height, distance)
                );
            }
        }

        /// <summary>
        /// World-space position of D1.
        /// </summary>
        public Vector3 EndCenter
        {
            get
            {
                if (m_D1 != null)
                    return m_D1.position;

                float height = m_Asset != null
                    ? m_Asset.LedHeight
                    : 0f;

                float distance = m_Asset != null
                    ? m_Asset.LoomEndDistance
                    : 0.15f;

                return transform.TransformPoint(
                    new Vector3(0f, height, distance)
                );
            }
        }

        private void Awake()
        {
            if (m_Asset == null)
            {
                Debug.LogError(
                    "[DistanceLayout] No PpsTaskAsset is assigned.",
                    this
                );

                return;
            }

            ConfigureFromAsset(m_Asset);

            if (m_DebugOnAwake)
            {
                DebugPpsOrigin();
                DebugDistanceLayout();
            }
        }

        /// <summary>
        /// Positions D7 through D1 using values from PpsTaskAsset.
        ///
        /// Exact calculation:
        ///
        /// stage.localPosition =
        ///     new Vector3(0, asset.LedHeight, stageDistance);
        ///
        /// No additional offset is added here.
        /// </summary>
        public void ConfigureFromAsset(PpsTaskAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError(
                    "[DistanceLayout] Cannot configure because asset is null.",
                    this
                );

                return;
            }

            m_Asset = asset;

            EnsureChild(
                ref m_D7,
                "D7",
                asset.DistanceForStage(DistanceStage.D7),
                asset.LedHeight
            );

            EnsureChild(
                ref m_D6,
                "D6",
                asset.DistanceForStage(DistanceStage.D6),
                asset.LedHeight
            );

            EnsureChild(
                ref m_D5,
                "D5",
                asset.DistanceForStage(DistanceStage.D5),
                asset.LedHeight
            );

            EnsureChild(
                ref m_D4,
                "D4",
                asset.DistanceForStage(DistanceStage.D4),
                asset.LedHeight
            );

            EnsureChild(
                ref m_D3,
                "D3",
                asset.DistanceForStage(DistanceStage.D3),
                asset.LedHeight
            );

            EnsureChild(
                ref m_D2,
                "D2",
                asset.DistanceForStage(DistanceStage.D2),
                asset.LedHeight
            );

            EnsureChild(
                ref m_D1,
                "D1",
                asset.DistanceForStage(DistanceStage.D1),
                asset.LedHeight
            );

            string configurationMessage =
                "[DistanceLayout] Configured from asset '" +
                asset.name +
                "'\n" +
                "D7 = " +
                asset.DistanceForStage(DistanceStage.D7).ToString("F4") +
                " m\n" +
                "D6 = " +
                asset.DistanceForStage(DistanceStage.D6).ToString("F4") +
                " m\n" +
                "D5 = " +
                asset.DistanceForStage(DistanceStage.D5).ToString("F4") +
                " m\n" +
                "D4 = " +
                asset.DistanceForStage(DistanceStage.D4).ToString("F4") +
                " m\n" +
                "D3 = " +
                asset.DistanceForStage(DistanceStage.D3).ToString("F4") +
                " m\n" +
                "D2 = " +
                asset.DistanceForStage(DistanceStage.D2).ToString("F4") +
                " m\n" +
                "D1 = " +
                asset.DistanceForStage(DistanceStage.D1).ToString("F4") +
                " m\n" +
                "LedHeight = " +
                asset.LedHeight.ToString("F4") +
                " m";

            Debug.Log(configurationMessage, this);
        }

        /// <summary>
        /// Finds or creates a stage child and gives it its exact local position.
        /// </summary>
        private void EnsureChild(
            ref Transform slot,
            string childName,
            float forwardDistance,
            float ledHeight
        )
        {
            if (slot == null)
            {
                Transform existingChild = transform.Find(childName);

                if (existingChild != null)
                {
                    slot = existingChild;
                }
                else
                {
                    GameObject childObject = new GameObject(childName);

                    childObject.transform.SetParent(
                        transform,
                        false
                    );

                    slot = childObject.transform;
                }
            }

            Vector3 expectedLocalPosition = new Vector3(
                0f,
                ledHeight,
                forwardDistance
            );

            // Exact position from PpsTaskAsset.
            // No extra offset is added.
            slot.localPosition = expectedLocalPosition;
            slot.localRotation = Quaternion.identity;
            slot.localScale = Vector3.one;

            float difference = Vector3.Distance(
                slot.localPosition,
                expectedLocalPosition
            );

            if (difference > 0.00001f)
            {
                string errorMessage =
                    "[DistanceLayout] " +
                    childName +
                    " position mismatch.\n" +
                    "Expected local position: " +
                    FormatVector(expectedLocalPosition) +
                    "\n" +
                    "Actual local position: " +
                    FormatVector(slot.localPosition) +
                    "\n" +
                    "Difference: " +
                    difference.ToString("F6") +
                    " m";

                Debug.LogError(errorMessage, slot);
                return;
            }

            if (m_LogStageConfiguration)
            {
                string confirmationMessage =
                    "[DistanceLayout] " +
                    childName +
                    " exact local position confirmed\n" +
                    "X = " +
                    slot.localPosition.x.ToString("F4") +
                    " m\n" +
                    "Y = " +
                    slot.localPosition.y.ToString("F4") +
                    " m\n" +
                    "Z = " +
                    slot.localPosition.z.ToString("F4") +
                    " m";

                Debug.Log(confirmationMessage, slot);
            }
        }

        /// <summary>
        /// Converts normalized looming progress into a distance-stage label.
        /// </summary>
        public DistanceStage StageAt(float progress)
        {
            progress = Mathf.Clamp01(progress);

            int stageCount = m_Asset != null
                ? Mathf.Clamp(m_Asset.DistanceStageCount, 2, 7)
                : 7;

            int intervalCount = stageCount - 1;

            int index = Mathf.FloorToInt(
                progress * intervalCount + 0.0001f
            );

            index = Mathf.Clamp(index, 0, intervalCount);

            switch (index)
            {
                case 0:
                    return DistanceStage.D7;

                case 1:
                    return DistanceStage.D6;

                case 2:
                    return DistanceStage.D5;

                case 3:
                    return DistanceStage.D4;

                case 4:
                    return DistanceStage.D3;

                case 5:
                    return DistanceStage.D2;

                default:
                    return DistanceStage.D1;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (m_Asset == null)
                return;

            ConfigureFromAsset(m_Asset);
        }

        [ContextMenu("Force Reconfigure From Asset")]
        public void ForceReconfigureFromAsset()
        {
            if (m_Asset == null)
            {
                Debug.LogError(
                    "[DistanceLayout] Cannot reconfigure because asset is null.",
                    this
                );

                return;
            }

            ConfigureFromAsset(m_Asset);
            DebugPpsOrigin();
            DebugDistanceLayout();
        }

        /// <summary>
        /// Logs the exact transform being used as the local zero.
        /// </summary>
        [ContextMenu("Debug PPS Origin")]
        public void DebugPpsOrigin()
        {
            Transform xrOrigin = m_XrOrigin;
            Transform mainCamera = GetMainCameraTransform();

            string layoutParentName = transform.parent != null
                ? transform.parent.name
                : "NONE";

            string xrOriginName = xrOrigin != null
                ? xrOrigin.name
                : "NULL";

            string xrOriginWorldPosition = xrOrigin != null
                ? FormatVector(xrOrigin.position)
                : "NULL";

            string xrOriginWorldRotation = xrOrigin != null
                ? FormatVector(xrOrigin.eulerAngles)
                : "NULL";

            string layoutRelativeToXr = xrOrigin != null
                ? FormatVector(
                    xrOrigin.InverseTransformPoint(transform.position)
                )
                : "NULL";

            string mainCameraName = mainCamera != null
                ? mainCamera.name
                : "NULL";

            string mainCameraWorldPosition = mainCamera != null
                ? FormatVector(mainCamera.position)
                : "NULL";

            string cameraRelativeToXr =
                mainCamera != null && xrOrigin != null
                    ? FormatVector(
                        xrOrigin.InverseTransformPoint(
                            mainCamera.position
                        )
                    )
                    : "NULL";

            string layoutRelativeToCamera = mainCamera != null
                ? FormatVector(
                    mainCamera.InverseTransformPoint(
                        transform.position
                    )
                )
                : "NULL";

            string message =
                "========== PPS ORIGIN DEBUG ==========\n" +
                "DistanceLayout object: " +
                name +
                "\n" +
                "DistanceLayout parent: " +
                layoutParentName +
                "\n\n" +
                "DistanceLayout local position: " +
                FormatVector(transform.localPosition) +
                "\n" +
                "DistanceLayout world position: " +
                FormatVector(transform.position) +
                "\n" +
                "DistanceLayout local rotation: " +
                FormatVector(transform.localEulerAngles) +
                "\n" +
                "DistanceLayout world rotation: " +
                FormatVector(transform.eulerAngles) +
                "\n" +
                "DistanceLayout forward: " +
                FormatVector(transform.forward) +
                "\n\n" +
                "XR Origin assigned: " +
                xrOriginName +
                "\n" +
                "XR Origin world position: " +
                xrOriginWorldPosition +
                "\n" +
                "XR Origin world rotation: " +
                xrOriginWorldRotation +
                "\n" +
                "DistanceLayout relative to XR Origin: " +
                layoutRelativeToXr +
                "\n\n" +
                "Main Camera assigned: " +
                mainCameraName +
                "\n" +
                "Main Camera world position: " +
                mainCameraWorldPosition +
                "\n" +
                "Main Camera relative to XR Origin: " +
                cameraRelativeToXr +
                "\n" +
                "DistanceLayout relative to Main Camera: " +
                layoutRelativeToCamera;

            Debug.Log(message, this);

            DebugStageOriginComparison(
                "D7",
                m_D7,
                xrOrigin,
                mainCamera
            );

            DebugStageOriginComparison(
                "D1",
                m_D1,
                xrOrigin,
                mainCamera
            );

            Debug.Log(
                "========== END PPS ORIGIN DEBUG ==========",
                this
            );
        }

        [ContextMenu("Debug Distance Layout")]
        public void DebugDistanceLayout()
        {
            Transform mainCamera = GetMainCameraTransform();

            string referenceName = mainCamera != null
                ? mainCamera.name
                : "NONE";

            string headerMessage =
                "========== DISTANCE LAYOUT DEBUG ==========\n" +
                "Layout name: " +
                name +
                "\n" +
                "Layout local position: " +
                FormatVector(transform.localPosition) +
                "\n" +
                "Layout world position: " +
                FormatVector(transform.position) +
                "\n" +
                "Layout local rotation: " +
                FormatVector(transform.localEulerAngles) +
                "\n" +
                "Layout world rotation: " +
                FormatVector(transform.eulerAngles) +
                "\n" +
                "Layout local scale: " +
                FormatVector(transform.localScale) +
                "\n" +
                "Layout lossy scale: " +
                FormatVector(transform.lossyScale) +
                "\n" +
                "Layout forward: " +
                FormatVector(transform.forward) +
                "\n" +
                "Distance reference: " +
                referenceName;

            Debug.Log(headerMessage, this);

            DebugStage(
                "D7",
                DistanceStage.D7,
                m_D7,
                mainCamera
            );

            DebugStage(
                "D6",
                DistanceStage.D6,
                m_D6,
                mainCamera
            );

            DebugStage(
                "D5",
                DistanceStage.D5,
                m_D5,
                mainCamera
            );

            DebugStage(
                "D4",
                DistanceStage.D4,
                m_D4,
                mainCamera
            );

            DebugStage(
                "D3",
                DistanceStage.D3,
                m_D3,
                mainCamera
            );

            DebugStage(
                "D2",
                DistanceStage.D2,
                m_D2,
                mainCamera
            );

            DebugStage(
                "D1",
                DistanceStage.D1,
                m_D1,
                mainCamera
            );

            DebugParentChain();

            Debug.Log(
                "========== END DISTANCE LAYOUT DEBUG ==========",
                this
            );
        }

        private void DebugStage(
            string label,
            DistanceStage distanceStage,
            Transform stage,
            Transform reference
        )
        {
            if (stage == null)
            {
                Debug.LogWarning(
                    "[DistanceLayout] " + label + " is null.",
                    this
                );

                return;
            }

            float assetDistance = m_Asset != null
                ? m_Asset.DistanceForStage(distanceStage)
                : float.NaN;

            float expectedHeight = m_Asset != null
                ? m_Asset.LedHeight
                : float.NaN;

            Vector3 expectedLocalPosition = new Vector3(
                0f,
                expectedHeight,
                assetDistance
            );

            Vector3 layoutToStage =
                stage.position - transform.position;

            float distanceAlongLayoutForward = Vector3.Dot(
                layoutToStage,
                transform.forward
            );

            float straightLineDistanceFromLayout =
                Vector3.Distance(
                    transform.position,
                    stage.position
                );

            float localDifference = Vector3.Distance(
                stage.localPosition,
                expectedLocalPosition
            );

            string message =
                "========== " +
                label +
                " ==========\n" +
                "Asset configured distance: " +
                FormatFloat(assetDistance) +
                " m\n" +
                "Asset configured height: " +
                FormatFloat(expectedHeight) +
                " m\n" +
                "Expected local position: " +
                FormatVector(expectedLocalPosition) +
                "\n" +
                "Actual local position: " +
                FormatVector(stage.localPosition) +
                "\n" +
                "Local X: " +
                stage.localPosition.x.ToString("F4") +
                " m\n" +
                "Local Y: " +
                stage.localPosition.y.ToString("F4") +
                " m\n" +
                "Local Z: " +
                stage.localPosition.z.ToString("F4") +
                " m\n" +
                "World position: " +
                FormatVector(stage.position) +
                "\n" +
                "World Z coordinate: " +
                stage.position.z.ToString("F4") +
                " m\n" +
                "Distance along layout forward: " +
                distanceAlongLayoutForward.ToString("F4") +
                " m\n" +
                "Straight-line distance from layout origin: " +
                straightLineDistanceFromLayout.ToString("F4") +
                " m\n" +
                "Local-position difference from expected: " +
                localDifference.ToString("F6") +
                " m";

            if (reference != null)
            {
                Vector3 relativeToReference =
                    reference.InverseTransformPoint(stage.position);

                Vector3 referenceToStage =
                    stage.position - reference.position;

                float distanceAlongReferenceForward = Vector3.Dot(
                    referenceToStage,
                    reference.forward
                );

                float straightLineDistanceFromReference =
                    Vector3.Distance(
                        reference.position,
                        stage.position
                    );

                message +=
                    "\nReference: " +
                    reference.name +
                    "\nReference world position: " +
                    FormatVector(reference.position) +
                    "\nStage relative to reference: " +
                    FormatVector(relativeToReference) +
                    "\nDistance along reference forward: " +
                    distanceAlongReferenceForward.ToString("F4") +
                    " m\n" +
                    "Straight-line distance from reference: " +
                    straightLineDistanceFromReference.ToString("F4") +
                    " m";
            }

            Debug.Log(message, stage);
        }

        private void DebugStageOriginComparison(
            string label,
            Transform stage,
            Transform xrOrigin,
            Transform mainCamera
        )
        {
            if (stage == null)
            {
                Debug.LogWarning(
                    "[DistanceLayout] " + label + " is null.",
                    this
                );

                return;
            }

            Vector3 fromLayout =
                stage.position - transform.position;

            float distanceAlongLayoutForward = Vector3.Dot(
                fromLayout,
                transform.forward
            );

            string message =
                "========== " +
                label +
                " ORIGIN CHECK ==========\n" +
                label +
                " local position: " +
                FormatVector(stage.localPosition) +
                "\n" +
                label +
                " world position: " +
                FormatVector(stage.position) +
                "\n" +
                label +
                " local Z: " +
                stage.localPosition.z.ToString("F4") +
                " m\n" +
                label +
                " distance along DistanceLayout forward: " +
                distanceAlongLayoutForward.ToString("F4") +
                " m";

            if (xrOrigin != null)
            {
                Vector3 relativeToXrOrigin =
                    xrOrigin.InverseTransformPoint(stage.position);

                float xrForwardDistance = Vector3.Dot(
                    stage.position - xrOrigin.position,
                    xrOrigin.forward
                );

                float straightLineDistanceFromXr =
                    Vector3.Distance(
                        xrOrigin.position,
                        stage.position
                    );

                message +=
                    "\n" +
                    label +
                    " relative to XR Origin: " +
                    FormatVector(relativeToXrOrigin) +
                    "\n" +
                    label +
                    " along XR Origin forward: " +
                    xrForwardDistance.ToString("F4") +
                    " m\n" +
                    label +
                    " straight-line distance from XR Origin: " +
                    straightLineDistanceFromXr.ToString("F4") +
                    " m";
            }

            if (mainCamera != null)
            {
                Vector3 relativeToCamera =
                    mainCamera.InverseTransformPoint(stage.position);

                float cameraForwardDistance = Vector3.Dot(
                    stage.position - mainCamera.position,
                    mainCamera.forward
                );

                float straightLineDistanceFromCamera =
                    Vector3.Distance(
                        mainCamera.position,
                        stage.position
                    );

                message +=
                    "\n" +
                    label +
                    " relative to Main Camera: " +
                    FormatVector(relativeToCamera) +
                    "\n" +
                    label +
                    " along camera forward: " +
                    cameraForwardDistance.ToString("F4") +
                    " m\n" +
                    label +
                    " straight-line distance from camera: " +
                    straightLineDistanceFromCamera.ToString("F4") +
                    " m";
            }

            Debug.Log(message, stage);
        }

        private Transform GetMainCameraTransform()
        {
            if (m_MainCamera != null)
                return m_MainCamera;

            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                return mainCamera.transform;

            return null;
        }

        private void DebugParentChain()
        {
            Debug.Log(
                "========== DISTANCE LAYOUT PARENT CHAIN ==========",
                this
            );

            Transform currentParent = transform.parent;

            if (currentParent == null)
            {
                Debug.Log(
                    "[DistanceLayout] No parent transform.",
                    this
                );

                return;
            }

            while (currentParent != null)
            {
                string parentMessage =
                    "Parent: " +
                    currentParent.name +
                    "\n" +
                    "Local position: " +
                    FormatVector(currentParent.localPosition) +
                    "\n" +
                    "World position: " +
                    FormatVector(currentParent.position) +
                    "\n" +
                    "Local rotation: " +
                    FormatVector(currentParent.localEulerAngles) +
                    "\n" +
                    "World rotation: " +
                    FormatVector(currentParent.eulerAngles) +
                    "\n" +
                    "Local scale: " +
                    FormatVector(currentParent.localScale) +
                    "\n" +
                    "Lossy scale: " +
                    FormatVector(currentParent.lossyScale);

                Debug.Log(parentMessage, currentParent);

                currentParent = currentParent.parent;
            }
        }

        private static string FormatVector(Vector3 value)
        {
            return
                "(" +
                value.x.ToString("F4") +
                ", " +
                value.y.ToString("F4") +
                ", " +
                value.z.ToString("F4") +
                ")";
        }

        private static string FormatFloat(float value)
        {
            if (float.IsNaN(value))
                return "N/A";

            return value.ToString("F4");
        }
    }
}