using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Moves a looming sphere along a curved trajectory toward the player.
    /// The ball starts at spawnDistance, travels at constant speed, curves laterally,
    /// and vanishes at vanishDistance. A ground shadow blob follows underneath.
    /// </summary>
    public class TrajectoryObjectController : MonoBehaviour
    {
        [Header("Shadow")]
        [SerializeField] GameObject m_ShadowPrefab;
        [Tooltip("Height of the ground plane for shadow projection (world Y)")]
        [SerializeField] float m_GroundY = 0f;

        Transform m_Shadow;
        TrialDefinition m_Trial;
        Vector3 m_PlayerPosition;
        Vector3 m_PlayerForward;
        Vector3 m_PlayerRight;
        float m_StartTime;
        float m_Duration;
        bool m_Active;

        // Precomputed trajectory points
        Vector3 m_StartPos;
        Vector3 m_EndPos;
        Vector3 m_ControlPoint; // quadratic bezier control for the curve

        public string TrialId { get; private set; }
        public bool IsComplete { get; private set; }

        public void Initialize(TrialDefinition trial, Vector3 playerPosition, Vector3 playerForward)
        {
            m_Trial = trial;
            TrialId = trial.trialId;
            m_PlayerPosition = playerPosition;
            m_PlayerForward = playerForward.normalized;
            m_PlayerRight = Vector3.Cross(Vector3.up, m_PlayerForward).normalized;

            m_Duration = trial.Duration;

            // Compute start position: offset by approach angle
            float angleRad = trial.approachAngleDeg * Mathf.Deg2Rad;
            Vector3 approachDir = (m_PlayerForward * Mathf.Cos(angleRad) + m_PlayerRight * Mathf.Sin(angleRad)).normalized;
            m_StartPos = m_PlayerPosition + approachDir * trial.spawnDistance;

            // End position: at vanish distance, with final lateral offset
            m_EndPos = m_PlayerPosition
                + m_PlayerForward * trial.vanishDistance
                + m_PlayerRight * trial.finalLateralOffset;

            // Control point: midpoint shifted by curvature
            Vector3 midpoint = (m_StartPos + m_EndPos) * 0.5f;
            m_ControlPoint = midpoint + m_PlayerRight * (trial.curveDirection * trial.curvatureMagnitude);

            // Set initial position and scale
            float diameter = trial.ballDiameter > 0f ? trial.ballDiameter : 0.175f;
            transform.localScale = Vector3.one * diameter;
            transform.position = m_StartPos;

            // Create shadow
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
            if (!m_Active || IsComplete)
                return;

            float elapsed = Time.time - m_StartTime;
            float t = Mathf.Clamp01(elapsed / m_Duration);

            // Quadratic bezier: B(t) = (1-t)^2 * P0 + 2(1-t)t * CP + t^2 * P1
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
                // Fallback: create a simple dark disc as shadow
                var shadowGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shadowGo.name = "BallShadow";

                // Remove collider
                var col = shadowGo.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Make it a flat dark disc
                shadowGo.transform.localScale = new Vector3(diameter * 1.2f, 0.005f, diameter * 1.2f);

                var renderer = shadowGo.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = new Color(0f, 0f, 0f, 0.35f);
                    // Enable transparency
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0);   // Alpha
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

            // Project ball position onto ground plane
            m_Shadow.position = new Vector3(ballPos.x, m_GroundY + 0.01f, ballPos.z);

            // Scale shadow based on height (closer to ground = sharper/larger)
            float height = Mathf.Max(ballPos.y - m_GroundY, 0.1f);
            float scaleFactor = Mathf.Clamp(1f / (height * 0.5f + 0.5f), 0.3f, 1.5f);
            float baseDiam = m_Trial.ballDiameter > 0 ? m_Trial.ballDiameter : 0.175f;
            m_Shadow.localScale = new Vector3(
                baseDiam * 1.2f * scaleFactor,
                0.005f,
                baseDiam * 1.2f * scaleFactor
            );

            m_Shadow.gameObject.SetActive(m_Active && !IsComplete);
        }

        void SetVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                r.enabled = visible;

            if (m_Shadow != null)
                m_Shadow.gameObject.SetActive(visible);
        }

        public void Despawn()
        {
            m_Active = false;
            IsComplete = true;
            if (m_Shadow != null)
                Destroy(m_Shadow.gameObject);
            Destroy(gameObject);
        }
    }
}
