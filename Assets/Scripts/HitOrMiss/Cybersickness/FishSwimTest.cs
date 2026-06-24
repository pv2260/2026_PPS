using System.Collections;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public class FishSwimTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject m_FishPrefab;

        [Tooltip("Usually XR Origin / Main Camera / CenterEyeAnchor.")]
        [SerializeField] Transform m_PlayerAnchor;

        [Header("Movement")]
        [SerializeField] float m_StartDistance = 3f;
        [SerializeField] float m_EndDistance = 0.4f;
        [SerializeField] float m_Speed = 1.0f;

        [Header("Animation")]
        [SerializeField] float m_BobAmplitude = 0.04f;
        [SerializeField] float m_BobFrequency = 1.5f;
        [SerializeField] float m_WiggleAmplitudeDeg = 8f;
        [SerializeField] float m_WiggleFrequency = 3f;

        GameObject m_CurrentFish;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                SpawnAndSwimFish();
            }
        }

        public void SpawnAndSwimFish()
        {
            if (m_FishPrefab == null)
            {
                Debug.LogError("[FishSwimTest] No fish prefab assigned.");
                return;
            }

            if (m_PlayerAnchor == null)
            {
                Debug.LogError("[FishSwimTest] No player anchor assigned.");
                return;
            }

            if (m_CurrentFish != null)
                Destroy(m_CurrentFish);

            Vector3 forward = m_PlayerAnchor.forward.normalized;

            Vector3 startPos = m_PlayerAnchor.position + forward * m_StartDistance;
            Vector3 endPos = m_PlayerAnchor.position + forward * m_EndDistance;

            m_CurrentFish = Instantiate(m_FishPrefab, startPos, Quaternion.identity);

            StartCoroutine(Swim(startPos, endPos));
        }

        IEnumerator Swim(Vector3 startPos, Vector3 endPos)
        {
            float distance = Vector3.Distance(startPos, endPos);
            float duration = distance / Mathf.Max(0.01f, m_Speed);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 basePos = Vector3.Lerp(startPos, endPos, t);

                float bob =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * m_BobFrequency)
                    * m_BobAmplitude;

                m_CurrentFish.transform.position = basePos + Vector3.up * bob;

                Vector3 direction = (endPos - startPos).normalized;
                Quaternion look = Quaternion.LookRotation(direction, Vector3.up);

                float wiggle =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * m_WiggleFrequency)
                    * m_WiggleAmplitudeDeg;

                m_CurrentFish.transform.rotation = look * Quaternion.Euler(0f, wiggle, 0f);

                yield return null;
            }

            Destroy(m_CurrentFish);
            m_CurrentFish = null;
        }
    }
}