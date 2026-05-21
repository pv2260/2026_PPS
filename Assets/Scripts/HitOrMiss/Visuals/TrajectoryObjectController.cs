using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Animates a ball along a straight-line trajectory toward the player.
    /// All trials spawn at the same point in front of the player
    /// (player + forward × spawnDistance) and travel in a straight line to an
    /// end point laterally offset from the player. The category is encoded
    /// entirely in the lateral offset (no curvature):
    ///   • Hit       — ends at the player (zero offset)
    ///   • NearHit   — ends very close to the player
    ///   • NearMiss  — ends 10–25 cm to the side
    ///   • Miss      — passes 30–45 cm to the side
    ///
    /// When the ball reaches the end point it spawns a splat effect at the
    /// impact position (instead of disappearing 1 m away from the patient).
    /// A pinch response recolors the ball once per trial: left pinch → blue,
    /// right pinch → orange, regardless of correctness.
    /// </summary>
    public class TrajectoryObjectController : MonoBehaviour
    {
        [Header("Shadow")]
        [SerializeField] GameObject m_ShadowPrefab;
        [Tooltip("World Y of the ground plane for shadow projection")]
        [SerializeField] float m_GroundY = 0f;

        [Header("Pinch feedback (side panels)")]
        [Tooltip("Child GameObject shown when the participant gives a LEFT pinch (Hit). Should display YES on blue.")]
        [SerializeField] GameObject m_LeftPanel;
        [Tooltip("Child GameObject shown when the participant gives a RIGHT pinch (Miss). Should display NO on orange.")]
        [SerializeField] GameObject m_RightPanel;
        [Tooltip("Background color applied to the LEFT panel image on activation (also drives splat tint).")]
        [SerializeField] Color m_LeftPanelColor = new Color(0.20f, 0.45f, 1.00f, 1f);
        [Tooltip("Background color applied to the RIGHT panel image on activation (also drives splat tint).")]
        [SerializeField] Color m_RightPanelColor = new Color(1.00f, 0.55f, 0.10f, 1f);

        [Header("Hit-validation tolerance")]
        [Tooltip("Extra radius (meters) added to the ball when computing impact. The ball's effective collision sphere is its visual radius + this bonus.")]
        [SerializeField] float m_BallCollisionRadius = 0.05f;

        [Header("Miss overreach (passes past the player)")]
        [Tooltip("For Miss-class trials, extend the ball's travel by this many meters along the trajectory direction so it visibly passes past the participant rather than vanishing at the lateral end.")]
        [SerializeField] float m_MissOverreachMeters = 0.8f;

        [Header("Splat on impact")]
        [Tooltip("Prefab spawned at the end of the trajectory. If empty a default splat is built procedurally.")]
        [SerializeField] GameObject m_SplatPrefab;
        [Tooltip("Lifetime of the procedurally-built splat (seconds). Ignored when SplatPrefab is used.")]
        [SerializeField] float m_SplatLifetime = 1.2f;
        [Tooltip("Final size of the procedurally-built splat at full expansion (meters). Ignored when SplatPrefab is used.")]
        [SerializeField] float m_SplatPeakSize = 0.45f;
        [Tooltip("Distance from the player (meters) at which the ball collides and the splat fires. 0 = disabled — the ball runs all the way to its lateral end position.")]
        [SerializeField] float m_ImpactDistance = 0f;
        [Tooltip("Restrict splat to trials whose expected response is Hit (Hit and NearHit categories). Miss-class trials (NearMiss, ClearMiss) pass through without bursting.")]
        [SerializeField] bool m_SplatOnlyOnHitClass = true;

        Transform m_Shadow;
        TrialDefinition m_Trial;
        float m_StartTime;
        float m_Duration;
        bool m_Active;
        bool m_PinchColorApplied;
        Color m_PinchTint = Color.white;
        Renderer[] m_Renderers;
        MaterialPropertyBlock m_Mpb;

        Vector3 m_StartPos;
        Vector3 m_EndPos;
        Vector3 m_PlayerPos;

        // --- Custom Fields Added for Instruction Setup ---
        private UnityEngine.UI.Image m_LeftPanelImage;
        private UnityEngine.UI.Image m_RightPanelImage;
        private Color m_LightGrey = new Color(0.8f, 0.8f, 0.8f, 1.0f);

        public string TrialId { get; private set; }
        public bool IsComplete { get; private set; }

        public void Initialize(TrialDefinition trial, Vector3 playerPosition, Vector3 playerForward)
        {
            m_Trial = trial;
            TrialId = trial.trialId;

            Vector3 forward = playerForward.sqrMagnitude > 0.0001f ? playerForward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

            m_StartPos = playerPosition + forward * trial.spawnDistance;
            m_EndPos = playerPosition + right * trial.finalLateralOffset;
            m_PlayerPos = playerPosition;

            if (!trial.WillHit && m_MissOverreachMeters > 0f)
            {
                Vector3 trajDir = (m_EndPos - m_StartPos);
                float baseLen = trajDir.magnitude;
                if (baseLen > 0.0001f)
                {
                    trajDir /= baseLen;
                    m_EndPos += trajDir * m_MissOverreachMeters;
                    m_Duration = trial.Duration * ((baseLen + m_MissOverreachMeters) / baseLen);
                }
                else
                {
                    m_Duration = trial.Duration;
                }
            }
            else
            {
                m_Duration = trial.Duration;
            }

            Debug.Log($"[TrajectoryObjectController] Spawn trial={trial.trialId} cat={trial.category} " +
                      $"willHit={trial.WillHit} impactDistance={m_ImpactDistance:F3}m " +
                      $"ballCollisionRadius={m_BallCollisionRadius:F3}m " +
                      $"missOverreach={m_MissOverreachMeters:F2}m " +
                      $"playerPos={playerPosition} startPos={m_StartPos} endPos={m_EndPos} " +
                      $"startDistToPlayer={Vector3.Distance(m_StartPos, playerPosition):F2}m " +
                      $"endDistToPlayer={Vector3.Distance(m_EndPos, playerPosition):F2}m");

            float diameter = trial.ballDiameter > 0f ? trial.ballDiameter : 0.175f;
            transform.localScale = Vector3.one * diameter;
            transform.position = m_StartPos;

            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_Mpb = new MaterialPropertyBlock();
            m_PinchColorApplied = false;

            if (m_LeftPanel != null)  m_LeftPanel.SetActive(false);
            if (m_RightPanel != null) m_RightPanel.SetActive(false);

            // Cache and store Image components for grey/color initialization
            if (m_LeftPanel != null)
                m_LeftPanelImage = m_LeftPanel.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (m_RightPanel != null)
                m_RightPanelImage = m_RightPanel.GetComponentInChildren<UnityEngine.UI.Image>(true);

            ResetPanelsToDefault();

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

            if (m_LeftPanel != null)
            {
                m_LeftPanel.SetActive(true);
                SetPanelInactiveStyle(m_LeftPanel);
            }

            if (m_RightPanel != null)
            {
                m_RightPanel.SetActive(true);
                SetPanelInactiveStyle(m_RightPanel);
            }
        }

        void Update()
        {
            if (!m_Active || IsComplete) return;

            float elapsed = Time.time - m_StartTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(m_Duration, 0.0001f));

            Vector3 pos = Vector3.Lerp(m_StartPos, m_EndPos, t);

            transform.position = pos;
            UpdateShadow(pos);

            if (m_ImpactDistance > 0f)
            {
                float ballRadius = (m_Trial.ballDiameter > 0f ? m_Trial.ballDiameter : 0.175f) * 0.5f;
                float effectiveImpact = m_ImpactDistance + ballRadius + m_BallCollisionRadius;
                float distToPlayer = Vector3.Distance(pos, m_PlayerPos);
                if (distToPlayer <= effectiveImpact)
                {
                    IsComplete = true;
                    m_Active = false;
                    SetVisible(false);
                    SpawnSplat(pos);
                    return;
                }
            }

            if (t >= 1f)
            {
                Debug.Log($"[TrajectoryObjectController] Trajectory end: trial={TrialId} " +
                          $"impactDistance={m_ImpactDistance:F3}m " +
                          $"endDistToPlayer={Vector3.Distance(m_EndPos, m_PlayerPos):F3}m " +
                          $"(impact-distance check never fired). endPos={m_EndPos}");
                IsComplete = true;
                m_Active = false;
                SetVisible(false);
                SpawnSplat(m_EndPos);
            }
        }

        public void ApplyPinchFeedback(SemanticCommand command)
        {
            if (m_PinchColorApplied) return;

            GameObject panel;
            Color tint;

            if (command == SemanticCommand.Hit)
            {
                panel = m_LeftPanel;
                tint = m_LeftPanelColor;
            }
            else if (command == SemanticCommand.Miss)
            {
                panel = m_RightPanel;
                tint = m_RightPanelColor;
            }
            else
            {
                return;
            }

            if (panel != null)
            {
                panel.SetActive(true);
                SetPanelSelectedStyle(panel, tint);
            }

            m_PinchColorApplied = true;
            m_PinchTint = tint;
        }

        // --- Custom Logic Methods Added for Instruction Setup ---
        public void ResetPanelsToDefault()
        {
            if (m_LeftPanelImage != null) m_LeftPanelImage.color = m_LightGrey;
            if (m_RightPanelImage != null) m_RightPanelImage.color = m_LightGrey;
        }

        public void SetInstructionFeedback(bool isYesState)
        {
            if (isYesState)
            {
                if (m_LeftPanelImage != null) m_LeftPanelImage.color = m_LeftPanelColor;
                if (m_RightPanelImage != null) m_RightPanelImage.color = m_LightGrey;
            }
            else
            {
                if (m_LeftPanelImage != null) m_LeftPanelImage.color = m_LightGrey;
                if (m_RightPanelImage != null) m_RightPanelImage.color = m_RightPanelColor;
            }
        }

        //Style of panels:
        void SetPanelInactiveStyle(GameObject panel)
        {
            if (panel == null) return;

            var images = panel.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                images[i].color = new Color(1f, 1f, 1f, 0f);

                var outline = images[i].GetComponent<UnityEngine.UI.Outline>();
                if (outline == null)
                    outline = images[i].gameObject.AddComponent<UnityEngine.UI.Outline>();

                outline.effectColor = new Color(0.7f, 0.7f, 0.7f, 0.03f);
                outline.effectDistance = new Vector2(0.5f, -0.5f);
                outline.useGraphicAlpha = false;
            }

            var texts = panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                 texts[i].color = new Color(0.85f, 0.85f, 0.85f, 0.25f);
            }
        }

        void SetPanelSelectedStyle(GameObject panel, Color fillColor)
        {
            if (panel == null) return;

            var images = panel.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                images[i].color = fillColor;

                var outline = images[i].GetComponent<UnityEngine.UI.Outline>();
                if (outline != null)
                    outline.effectColor = fillColor;
            }

            var texts = panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                texts[i].color = Color.white;
            }
        }

        void SpawnSplat(Vector3 worldPos)
        {
            if (m_SplatOnlyOnHitClass && !m_Trial.WillHit)
                return;

            if (m_SplatPrefab != null)
            {
                var go = Instantiate(m_SplatPrefab, worldPos, Quaternion.identity);
                InitializeSplatInstance(go, m_PinchTint, m_PinchColorApplied,
                                        Time.time, m_SplatLifetime, m_SplatPeakSize);
                return;
            }
            BuildDefaultSplat(worldPos);
        }

        static void InitializeSplatInstance(GameObject splatRoot, Color tint, bool overrideTint,
                                            float startTime, float lifetime, float peakSize)
        {
            if (splatRoot == null) return;
            var renderers = splatRoot.GetComponentsInChildren<Renderer>(true);
            float blobSeed = UnityEngine.Random.value * 1000f;
            if (renderers.Length > 0)
            {
                var mpb = new MaterialPropertyBlock();
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    if (overrideTint)
                    {
                        mpb.SetColor("_Color", tint);
                        mpb.SetColor("_BaseColor", tint);
                    }
                    mpb.SetFloat("_StartTime", startTime);
                    mpb.SetFloat("_Lifetime",  lifetime);
                    mpb.SetFloat("_PeakSize",  peakSize);
                    mpb.SetFloat("_BlobSeed",  blobSeed);
                    r.SetPropertyBlock(mpb);
                }
            }

            var driver = splatRoot.GetComponent<SplatLifetime>();
            if (driver == null) driver = splatRoot.AddComponent<SplatLifetime>();
            driver.Init(lifetime, peakSize);
        }

        void BuildDefaultSplat(Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BallSplat";

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * 0.02f;

            Color tint = m_PinchColorApplied ? m_PinchTint : Color.white;

            var rend = go.GetComponent<Renderer>();
            var splatShader = Shader.Find("PPS/BallSplat");
            if (splatShader != null)
            {
                var mat = new Material(splatShader);
                mat.SetColor("_Color", tint);
                mat.SetFloat("_StartTime", Time.time);
                mat.SetFloat("_Lifetime", m_SplatLifetime);
                mat.SetFloat("_PeakSize", m_SplatPeakSize);
                mat.SetFloat("_BlobSeed", UnityEngine.Random.value * 1000f);
                rend.material = mat;
            }
            else
            {
                var fallback = Shader.Find("Universal Render Pipeline/Unlit");
                if (fallback != null)
                {
                    var mat = new Material(fallback) { color = tint };
                    rend.material = mat;
                }
            }

            var driver = go.AddComponent<SplatLifetime>();
            driver.Init(m_SplatLifetime, m_SplatPeakSize);
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

    public class SplatLifetime : MonoBehaviour
    {
        float m_Lifetime;
        float m_PeakSize;
        float m_StartTime;
        bool m_HasSplatShader;
        Renderer m_Renderer;

        public void Init(float lifetime, float peakSize)
        {
            m_Lifetime = Mathf.Max(0.05f, lifetime);
            m_PeakSize = Mathf.Max(0.001f, peakSize);
            m_StartTime = Time.time;
            m_Renderer = GetComponent<Renderer>();
            m_HasSplatShader =
                m_Renderer != null
                && m_Renderer.sharedMaterial != null
                && m_Renderer.sharedMaterial.shader != null
                && m_Renderer.sharedMaterial.shader.name == "PPS/BallSplat";
        }

        void Update()
        {
            float age = Time.time - m_StartTime;
            float t = Mathf.Clamp01(age / m_Lifetime);

            if (!m_HasSplatShader)
            {
                float scale = Mathf.SmoothStep(0.02f, m_PeakSize, Mathf.Min(1f, t * 3f));
                transform.localScale = Vector3.one * scale;
                if (m_Renderer != null && m_Renderer.material != null)
                {
                    Color c = m_Renderer.material.color;
                    c.a = 1f - t;
                    m_Renderer.material.color = c;
                }
            }

            if (age >= m_Lifetime) Destroy(gameObject);
        }
    }
}