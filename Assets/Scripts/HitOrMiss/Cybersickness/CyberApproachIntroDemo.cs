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

        [Header("Motion")]
        [SerializeField] float m_StartDistance = 3.0f;
        [SerializeField] float m_EndDistance = 0.7f;
        [SerializeField] float m_Duration = 3.0f;
        [SerializeField] float m_VerticalOffset = -0.1f;
        [SerializeField] float m_AnimalScale = 1.0f;

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

            Vector3 verticalOffset = Vector3.up * m_VerticalOffset;

            Vector3 startPosition =
                eyePosition +
                forward * m_StartDistance +
                verticalOffset;

            Vector3 endPosition =
                eyePosition +
                forward * m_EndDistance +
                verticalOffset;

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

                if (m_CurrentAnimal != null)
                {
                    m_CurrentAnimal.transform.position =
                        Vector3.Lerp(startPosition, endPosition, smoothT);

                    if (direction.sqrMagnitude > 0.0001f)
                        m_CurrentAnimal.transform.rotation =
                            Quaternion.LookRotation(direction, Vector3.up);
                }

                yield return null;
            }

            yield return new WaitForSeconds(0.4f);

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