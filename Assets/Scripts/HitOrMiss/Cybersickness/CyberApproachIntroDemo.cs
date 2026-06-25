using System.Collections;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class CyberApproachIntroDemo : MonoBehaviour
    {
        [Header("Animal demo")]
        [SerializeField] GameObject m_AnimalPrefab;
        [SerializeField] Transform m_PlayerAnchor;
        [SerializeField] Transform m_SpawnParent;

        [Header("Approach path")]
        [SerializeField] float m_StartDistance = 3.0f;
        [SerializeField] float m_EndDistance = 0.7f;
        [SerializeField] float m_Duration = 3.0f;
        [SerializeField] float m_VerticalOffset = -0.1f;
        [SerializeField] float m_AnimalScale = 1.0f;

        [Header("Bounce")]
        [SerializeField] float m_BounceAmplitude = 0.12f;
        [SerializeField] float m_BounceFrequency = 2.2f;

        [Header("Side sway")]
        [SerializeField] float m_SwayAmplitude = 0.08f;
        [SerializeField] float m_SwayFrequency = 1.2f;

        [Header("Rotation polish")]
        [SerializeField] float m_WiggleYawAmplitude = 8f;
        [SerializeField] float m_WiggleYawFrequency = 2.5f;
        [SerializeField] float m_ModelYawOffset = 180f;

        GameObject m_CurrentAnimal;
        Coroutine m_DemoCoroutine;

        public bool Finished { get; private set; }

        void OnDisable()
        {
            ClearAnimal();
        }

        public void PlayDemo()
        {
            if (m_DemoCoroutine != null)
                StopCoroutine(m_DemoCoroutine);

            ClearAnimal();

            Finished = false;
            m_DemoCoroutine = StartCoroutine(RunDemo());
        }

        IEnumerator RunDemo()
        {
            if (m_AnimalPrefab == null)
            {
                Debug.LogError("[APPROACH INTRO] No animal prefab assigned.");
                Finished = true;
                yield break;
            }

            if (m_PlayerAnchor == null)
            {
                Debug.LogError("[APPROACH INTRO] No player anchor assigned.");
                Finished = true;
                yield break;
            }

            Vector3 eyePosition = m_PlayerAnchor.position;

            Vector3 forward = m_PlayerAnchor.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();

            Vector3 right = m_PlayerAnchor.right;
            right.y = 0f;

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            right.Normalize();

            Vector3 baseOffset = Vector3.up * m_VerticalOffset;

            Vector3 startPosition =
                eyePosition +
                forward * m_StartDistance +
                baseOffset;

            Vector3 endPosition =
                eyePosition +
                forward * m_EndDistance +
                baseOffset;

            m_CurrentAnimal = Instantiate(
                m_AnimalPrefab,
                startPosition,
                Quaternion.identity,
                m_SpawnParent
            );

            m_CurrentAnimal.transform.localScale = Vector3.one * m_AnimalScale;

            Vector3 direction = (endPosition - startPosition).normalized;

            float elapsed = 0f;

            while (elapsed < m_Duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / m_Duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 basePosition = Vector3.Lerp(startPosition, endPosition, smoothT);

                float bounce =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * m_BounceFrequency) * m_BounceAmplitude;

                float sway =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * m_SwayFrequency) * m_SwayAmplitude;

                Vector3 animatedPosition =
                    basePosition +
                    Vector3.up * bounce +
                    right * sway;

                if (m_CurrentAnimal != null)
                {
                    m_CurrentAnimal.transform.position = animatedPosition;

                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        Quaternion lookRotation =
                            Quaternion.LookRotation(direction, Vector3.up);

                        lookRotation *= Quaternion.Euler(0f, m_ModelYawOffset, 0f);

                        float wiggleYaw =
                            Mathf.Sin(elapsed * Mathf.PI * 2f * m_WiggleYawFrequency)
                            * m_WiggleYawAmplitude;

                        Quaternion wiggleRotation = Quaternion.Euler(0f, wiggleYaw, 0f);

                        m_CurrentAnimal.transform.rotation = lookRotation * wiggleRotation;
                    }
                }

                yield return null;
            }

            yield return new WaitForSeconds(0.35f);

            ClearAnimal();

            Finished = true;
            m_DemoCoroutine = null;
        }

        void ClearAnimal()
        {
            if (m_CurrentAnimal != null)
                Destroy(m_CurrentAnimal);

            m_CurrentAnimal = null;
        }
    }
}