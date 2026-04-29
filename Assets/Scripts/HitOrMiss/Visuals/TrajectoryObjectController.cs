using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Animates a ball along a curved trajectory toward the player.
    /// All trials spawn at the same point in front of the player
    /// (player + forward × spawnDistance) and travel toward the player.
    ///
    /// The category is encoded in the lateral curve and end offset:
    ///   • Hit       — straight line, ends at the player
    ///   • NearHit   — slight outward curve, ends very close to the player
    ///   • NearMiss  — moderate outward curve, ends 10–25 cm to the side
    ///   • Miss      — pronounced outward curve, ends 30–45 cm to the side
    /// </summary>
    public class TrajectoryObjectController : MonoBehaviour
    {
        [Header("Shadow")]
        [SerializeField] GameObject m_ShadowPrefab;
        [Tooltip("World Y of the ground plane for shadow projection")]
        [SerializeField] float m_GroundY = 0f;

        Transform m_Shadow;
        TrialDefinition m_Trial;
        float m_StartTime;
        float m_Duration;
        bool m_Active;

        Vector3 m_StartPos;
        Vector3 m_EndPos;
        Vector3 m_ControlPoint;

        public string TrialId { get; private set; }
        public bool IsComplete { get; private set; }

        public void Initialize(TrialDefinition trial, Vector3 playerPosition, Vector3 playerForward)
        {
            m_Trial = trial;
            TrialId = trial.trialId;

            Vector3 forward = playerForward.sqrMagnitude > 0.0001f ? playerForward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

            // Single spawn point in front of the player. End at the player with a
            // signed lateral offset. The arc bows outward (in the direction of the
            // end offset) by curveMagnitude at the midpoint — Hit has zero curve.
            m_StartPos = playerPosition + forward * trial.spawnDistance;
            m_EndPos = playerPosition + right * trial.finalLateralOffset;

            float curveSign = Mathf.Approximately(trial.finalLateralOffset, 0f)
                ? 0f
                : Mathf.Sign(trial.finalLateralOffset);

            Vector3 midpoint = (m_StartPos + m_EndPos) * 0.5f;
            // Quadratic bezier midpoint sits at the average of (P0 + P1)/2 and the
            // control point — so we need 2× the desired apex offset on the control.
            m_ControlPoint = midpoint + right * (curveSign * trial.curveMagnitude * 2f);

            m_Duration = trial.Duration;

            float diameter = trial.ballDiameter > 0f ? trial.ballDiameter : 0.175f;
            transform.localScale = Vector3.one * diameter;
            transform.position = m_StartPos;

            CreateShadow(diameter);
            SetVisible(false);
            m_Active = false;
            IsComplete = false;
        }

        public void Activate(float engineTime)
        {
            m_StartTime = engineTime;
            m_Active = true;
            SetVisible(true);
        }

        void Update()
        {
            if (!m_Active || IsComplete) return;

            float elapsed = Time.time - m_StartTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(m_Duration, 0.0001f));

            float oneMinusT = 1f - t;
            Vector3 pos = oneMinusT * oneMinusT * m_StartPos
                        + 2f * oneMinusT * t * m_ControlPoint
                        + t * t * m_EndPos;

            transform.position = pos;
            UpdateShadow(pos);

            if (t >= 1f)
            {
                IsComplete = true;
                m_Active = false;
                SetVisible(false);
            }
        }

        void CreateShadow(float diameter)
        {
            if (m_ShadowPrefab != null)
            {
                var shadowGo = Instantiate(m_ShadowPrefab, transform.position, Quaternion.Euler(90f, 0f, 0f));
                shadowGo.transform.SetParent(transform.parent);
                m_Shadow = shadowGo.transform;
                m_Shadow.localScale = Vector3.one * diameter * 1.2f;
            }
            else
            {
                var shadowGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shadowGo.name = "BallShadow";

                var col = shadowGo.GetComponent<Collider>();
                if (col != null) Destroy(col);

                shadowGo.transform.localScale = new Vector3(diameter * 1.2f, 0.005f, diameter * 1.2f);

                var renderer = shadowGo.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = new Color(0f, 0f, 0f, 0.35f);
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 0);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = 3000;
                    renderer.material = mat;
                }

                shadowGo.transform.SetParent(transform.parent);
                m_Shadow = shadowGo.transform;
            }
        }

        void UpdateShadow(Vector3 ballPos)
        {
            if (m_Shadow == null) return;

            m_Shadow.position = new Vector3(ballPos.x, m_GroundY + 0.01f, ballPos.z);

            float height = Mathf.Max(ballPos.y - m_GroundY, 0.1f);
            float scaleFactor = Mathf.Clamp(1f / (height * 0.5f + 0.5f), 0.3f, 1.5f);
            float baseDiam = m_Trial.ballDiameter > 0 ? m_Trial.ballDiameter : 0.175f;
            m_Shadow.localScale = new Vector3(baseDiam * 1.2f * scaleFactor, 0.005f, baseDiam * 1.2f * scaleFactor);

            m_Shadow.gameObject.SetActive(m_Active && !IsComplete);
        }

        void SetVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = visible;
            if (m_Shadow != null) m_Shadow.gameObject.SetActive(visible);
        }

        public void Despawn()
        {
            m_Active = false;
            IsComplete = true;
            if (m_Shadow != null) Destroy(m_Shadow.gameObject);
            Destroy(gameObject);
        }
    }
}
