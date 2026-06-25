using System.Collections;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CyberFishController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject m_FishPrefab;

        [Tooltip("Usually the XR camera / CenterEyeAnchor / Main Camera.")]
        [SerializeField] Transform m_PlayerAnchor;

        [Tooltip("Defines the stable forward direction for fish approach. Use SpawnOrigin.")]
        [SerializeField] Transform m_SpawnOrigin;

        [Tooltip("Small bubble effect shown when the fish touches the participant.")]
        [SerializeField] GameObject m_BubblePrefab;

        [Tooltip("Optional parent for spawned fish and bubbles. Leave empty while testing.")]
        [SerializeField] Transform m_SpawnParent;

        [Header("Fish visual scale")]
        [SerializeField] float m_FishScale = 0.55f;

        [Header("Trajectory tuning")]
        [Tooltip("Keeps the fish slightly below eye level so it stays visible.")]
        [SerializeField] float m_VerticalOffsetMeters = -0.20f;

        [Tooltip("Optional cap on speed so the fish is not too fast while testing.")]
        [SerializeField] float m_MaxVisibleSpeed = 0.50f;

        [Header("Bubble")]
        [SerializeField] float m_BubbleLifetime = 1.2f;
        [SerializeField] float m_BubbleUpSpeed = 0.25f;
        [SerializeField] float m_BubbleForwardOffset = 0.10f;
        [SerializeField] float m_BubbleVerticalOffset = 0.12f;
        [SerializeField] float m_BubbleScale = 0.18f;

        [Header("Swimming animation")]
        [Tooltip("Side-to-side body wiggle in degrees.")]
        [SerializeField] float m_SwimYawAmplitudeDeg = 8f;

        [Tooltip("How fast the fish wiggles.")]
        [SerializeField] float m_SwimYawFrequencyHz = 2.5f;

        [Tooltip("Small vertical bob while swimming.")]
        [SerializeField] float m_BobAmplitudeMeters = 0.05f;

        [SerializeField] float m_BobFrequencyHz = 1.5f;

        GameObject m_CurrentFish;
        GameObject m_CurrentBubble;

        bool m_Running;
        bool m_OutcomeReached;
        bool m_Finished;

        double m_OutcomeTime;

        public bool IsRunning => m_Running;
        public bool OutcomeReached => m_OutcomeReached;
        public bool Finished => m_Finished;
        public double OutcomeTime => m_OutcomeTime;

        public void StartFish(CyberBugTrialDefinition trial)
        {
            StopAndClear();

            m_Running = true;
            m_OutcomeReached = false;
            m_Finished = false;
            m_OutcomeTime = double.NaN;

            if (m_PlayerAnchor == null)
            {
                Debug.LogError("[CyberFishController] No PlayerAnchor assigned.");
                m_Running = false;
                m_Finished = true;
                return;
            }

            if (m_FishPrefab == null)
            {
                Debug.LogError("[CyberFishController] No FishPrefab assigned.");
                m_Running = false;
                m_Finished = true;
                return;
            }

            Vector3 eyePosition = m_PlayerAnchor.position;

            // Use the participant's head/camera direction.
            Transform directionAnchor = m_PlayerAnchor;

            Vector3 forward = directionAnchor.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();

            Vector3 right = directionAnchor.right;
            right.y = 0f;

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            right.Normalize();

            Vector3 verticalOffset = Vector3.up * m_VerticalOffsetMeters;

            // Fish starts in front of the participant.
            float startDistance = Mathf.Clamp(trial.startDistance, 2.0f, 4.0f);
            float contactDistance = Mathf.Clamp(trial.contactDistance, 0.35f, 1.0f);

            // Keep the miss offset visible but not huge.
            float missOffset = Mathf.Clamp(Mathf.Abs(trial.lateralOffset), 0.35f, 0.90f);

            Vector3 startPosition =
                eyePosition +
                forward * startDistance +
                verticalOffset;

            Vector3 endPosition;

            if (trial.willTouch)
            {
                // YES trial: fish comes toward the participant.
                endPosition =
                    eyePosition +
                    forward * contactDistance +
                    verticalOffset;
            }
            else
            {
                // NO trial: fish comes toward the participant but passes to the side.
                endPosition =
                    eyePosition +
                    forward * contactDistance +
                    right * missOffset +
                    verticalOffset;
            }

            Debug.LogError(
                $"[FISH DEBUG] trial={trial.trialId} willTouch={trial.willTouch} willSplash={trial.willSplash} " +
                $"speed={trial.speed} start={startPosition} end={endPosition} " +
                $"eye={eyePosition} forward={forward} right={right}"
            );

            m_CurrentFish = Instantiate(
                m_FishPrefab,
                startPosition,
                Quaternion.identity,
                m_SpawnParent
            );

            m_CurrentFish.transform.localScale = Vector3.one * m_FishScale;

            Debug.LogError($"[FISH DEBUG] Spawned fish active={m_CurrentFish.activeSelf} position={m_CurrentFish.transform.position} scale={m_CurrentFish.transform.localScale}");

            StartCoroutine(SwimFish(trial, startPosition, endPosition, forward));
        }

        IEnumerator SwimFish(
            CyberBugTrialDefinition trial,
            Vector3 startPosition,
            Vector3 endPosition,
            Vector3 forward)
        {
            float distance = Vector3.Distance(startPosition, endPosition);

            // Clamp speed while testing so the fish is slow enough to perceive.
            float visibleSpeed = Mathf.Min(trial.speed, m_MaxVisibleSpeed);
            visibleSpeed = Mathf.Max(0.01f, visibleSpeed);

            float duration = distance / visibleSpeed;
            float elapsed = 0f;

            Vector3 direction = (endPosition - startPosition).normalized;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth motion: slow start, clear approach, slow end.
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 basePosition = Vector3.Lerp(startPosition, endPosition, smoothT);

                float bob =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * m_BobFrequencyHz)
                    * m_BobAmplitudeMeters;

                Vector3 animatedPosition = basePosition + Vector3.up * bob;

                if (m_CurrentFish != null)
                {
                    m_CurrentFish.transform.position = animatedPosition;

                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);

                        float swimYaw =
                            Mathf.Sin(elapsed * Mathf.PI * 2f * m_SwimYawFrequencyHz)
                            * m_SwimYawAmplitudeDeg;

                        Quaternion swimRotation = Quaternion.Euler(0f, swimYaw, 0f);

                        m_CurrentFish.transform.rotation = lookRotation * swimRotation;
                    }
                }

                yield return null;
            }

            if (m_CurrentFish != null)
                m_CurrentFish.transform.position = endPosition;

            m_OutcomeReached = true;
            m_OutcomeTime = Time.timeAsDouble;

            if (trial.willSplash)
                yield return ShowBubble(endPosition, forward);

            if (m_CurrentFish != null)
                Destroy(m_CurrentFish);

            m_CurrentFish = null;

            m_Running = false;
            m_Finished = true;
        }
        
        IEnumerator ShowBubble(Vector3 fishEndPosition, Vector3 forward)
        {
            if (m_BubblePrefab == null)
            {
                Debug.LogWarning("[CyberFishController] No BubblePrefab assigned.");
                yield break;
            }

            int bubbleCount = 8;

            GameObject[] bubbles = new GameObject[bubbleCount];
            Vector3[] startPositions = new Vector3[bubbleCount];
            Vector3[] startScales = new Vector3[bubbleCount];
            Vector3[] endScales = new Vector3[bubbleCount];
            float[] riseSpeeds = new float[bubbleCount];

            Vector3 baseBubblePosition =
                fishEndPosition +
                forward * m_BubbleForwardOffset +
                Vector3.up * m_BubbleVerticalOffset;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            for (int i = 0; i < bubbleCount; i++)
            {
                float sideJitter = Random.Range(-0.18f, 0.18f);
                float upJitter = Random.Range(-0.05f, 0.15f);
                float forwardJitter = Random.Range(-0.08f, 0.08f);

                Vector3 bubblePosition =
                    baseBubblePosition +
                    right * sideJitter +
                    Vector3.up * upJitter +
                    forward * forwardJitter;

                bubbles[i] = Instantiate(
                    m_BubblePrefab,
                    bubblePosition,
                    Quaternion.identity,
                    m_SpawnParent
                );

                float randomScale = Random.Range(0.6f, 1.3f) * m_BubbleScale;

                bubbles[i].transform.localScale = Vector3.one * randomScale;

                if (m_PlayerAnchor != null)
                    bubbles[i].transform.LookAt(m_PlayerAnchor.position);

                startPositions[i] = bubblePosition;
                startScales[i] = bubbles[i].transform.localScale;
                endScales[i] = startScales[i] * Random.Range(1.3f, 2.0f);
                riseSpeeds[i] = Random.Range(m_BubbleUpSpeed * 0.7f, m_BubbleUpSpeed * 1.5f);
            }

            float elapsed = 0f;

            while (elapsed < m_BubbleLifetime)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / m_BubbleLifetime);

                for (int i = 0; i < bubbleCount; i++)
                {
                    if (bubbles[i] == null)
                        continue;

                    bubbles[i].transform.position =
                        startPositions[i] +
                        Vector3.up * riseSpeeds[i] * elapsed;

                    bubbles[i].transform.localScale =
                        Vector3.Lerp(startScales[i], endScales[i], t);
                }

                yield return null;
            }

            for (int i = 0; i < bubbleCount; i++)
            {
                if (bubbles[i] != null)
                    Destroy(bubbles[i]);
            }

            m_CurrentBubble = null;
        }

        public void StopAndClear()
        {
            StopAllCoroutines();

            if (m_CurrentFish != null)
                Destroy(m_CurrentFish);

            if (m_CurrentBubble != null)
                Destroy(m_CurrentBubble);

            m_CurrentFish = null;
            m_CurrentBubble = null;

            m_Running = false;
            m_OutcomeReached = false;
            m_Finished = true;
            m_OutcomeTime = double.NaN;
        }
    }
}