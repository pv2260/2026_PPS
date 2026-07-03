using UnityEngine;

namespace HitOrMiss.Pps
{
    public class PpsHeadTurnPauseButton : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[PpsHeadTurnPauseButton] Awake fired.", this);
        }
        
        [Header("References")]
        [SerializeField] private Transform xrCamera;
        [SerializeField] private GameObject pauseButtonRoot;
        [SerializeField] private GameObject fullPausePanel;

        [Header("Head Turn Detection")]
        [SerializeField] private float showAngleDegrees = 75f;
        [SerializeField] private float hideAngleDegrees = 55f;

        [Header("Button Placement")]
        [SerializeField] private float distanceFromHead = 1.0f;
        [SerializeField] private float verticalOffset = -0.1f;

        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private Vector3 referenceForward;
        private bool buttonVisible;

        private void Start()
        {
            if (xrCamera == null && Camera.main != null)
                xrCamera = Camera.main.transform;

            if (xrCamera == null)
            {
                Debug.LogError("[PpsHeadTurnPauseButton] No XR Camera assigned and no Camera.main found.");
                enabled = false;
                return;
            }

            if (pauseButtonRoot == null)
            {
                Debug.LogError("[PpsHeadTurnPauseButton] Pause Button Root is not assigned.");
                enabled = false;
                return;
            }

            referenceForward = Flatten(xrCamera.forward).normalized;

            SetButtonVisible(false);
        }

        private void LateUpdate()
        {
            if (xrCamera == null || pauseButtonRoot == null)
                return;

            if (fullPausePanel != null && fullPausePanel.activeSelf)
            {
                SetButtonVisible(false);
                return;
            }

            Vector3 headForward = Flatten(xrCamera.forward).normalized;

            if (headForward.sqrMagnitude < 0.001f)
                return;

            float signedAngle = Vector3.SignedAngle(referenceForward, headForward, Vector3.up);
            float absAngle = Mathf.Abs(signedAngle);

            if (logDebug && Time.frameCount % 60 == 0)
               

            if (!buttonVisible && absAngle >= showAngleDegrees)
            {
                if (logDebug)
                  

                SetButtonVisible(true);
            }
            else if (buttonVisible && absAngle <= hideAngleDegrees)
            {
                if (logDebug)
                  

                SetButtonVisible(false);
            }

            if (buttonVisible)
                PositionButton();
        }

        private void PositionButton()
        {
            Vector3 headPosition = xrCamera.position;
            Vector3 forward = Flatten(xrCamera.forward).normalized;

            if (forward.sqrMagnitude < 0.001f)
                forward = xrCamera.forward;

            pauseButtonRoot.transform.position =
                headPosition +
                forward * distanceFromHead +
                Vector3.up * verticalOffset;

            // Face the button toward the participant.
            Vector3 directionToHead = headPosition - pauseButtonRoot.transform.position;
            directionToHead.y = 0f;

            if (directionToHead.sqrMagnitude > 0.001f)
            {
                pauseButtonRoot.transform.rotation =
                    Quaternion.LookRotation(-directionToHead.normalized, Vector3.up);
            }
        }

        private void SetButtonVisible(bool visible)
        {
            buttonVisible = visible;

            if (pauseButtonRoot != null && pauseButtonRoot.activeSelf != visible)
                pauseButtonRoot.SetActive(visible);
        }

        private Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}