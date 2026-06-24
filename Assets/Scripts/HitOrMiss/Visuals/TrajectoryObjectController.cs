using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Animates a ball along a straight-line trajectory toward the player.
    /// All trials spawn at the same point in front of the player
    /// (player + forward × spawnDistance) and travel in a straight line toward
    /// an end point laterally offset from the player. The category is encoded
    /// entirely in the lateral offset (no curvature):
    ///   • Hit       — ends at the player (zero offset)
    ///   • NearHit   — ends very close to the player
    ///   • NearMiss  — ends 10–25 cm to the side
    ///   • Miss      — passes 30–45 cm to the side
    ///
    /// Every ball — hit or miss — overshoots the participant by
    /// m_OverreachMeters along the trajectory direction so it visibly travels
    /// past them. The ball despawns immediately when it crosses the plane
    /// behind the participant; the LEFT/RIGHT response panels are torn down
    /// at the same instant so they don't linger.
    ///
    /// A pinch response recolors the matching response panel once per trial:
    /// left pinch → blue, right pinch → orange, regardless of correctness.
    /// </summary>
    public class TrajectoryObjectController : MonoBehaviour
    {
        //[Header("Shadow")]
        //[SerializeField] GameObject m_ShadowPrefab;
        //[Tooltip("World Y of the ground plane for shadow projection")]
        //[SerializeField] float m_GroundY = 0f;

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

        [Header("Overreach (every ball passes past the participant)")]
        [Tooltip("Distance in meters the ball continues past the participant plane (perpendicular to player's forward axis at the player's position). Applies to BOTH hit and miss trials so participants see the ball travel past them.")]
        [SerializeField] float m_OverreachMeters = 2.0f;

        [Header("Splat on impact")]
        [Tooltip("Prefab spawned at the moment of body-plane impact (hit-class trials only). If empty a default splat is built procedurally.")]
        [SerializeField] GameObject m_SplatPrefab;
        [Tooltip("Lifetime of the procedurally-built splat (seconds). Ignored when SplatPrefab is used.")]
        [SerializeField] float m_SplatLifetime = 1.2f;
        [Tooltip("Final size of the procedurally-built splat at full expansion (meters). Ignored when SplatPrefab is used.")]
        [SerializeField] float m_SplatPeakSize = 0.45f;
        [Tooltip("Distance from the player (meters) at which a hit-class ball collides and the splat fires. 0 = disabled — splat fires when the ball crosses the player plane.")]
        [SerializeField] float m_ImpactDistance = 0f;
        [Tooltip("Restrict splat to trials whose expected response is Hit (Hit and NearHit categories). Miss-class trials (NearMiss, ClearMiss) pass through without bursting.")]
        [SerializeField] bool m_SplatOnlyOnHitClass = true;

        //Transform m_Shadow;
        TrialDefinition m_Trial;
        float m_StartTime;
        float m_Duration;
        bool m_Active;
        bool m_PinchColorApplied;
        bool m_SplatFired;
        bool m_PassedPlayerPlane;
        Color m_PinchTint = Color.white;
        Renderer[] m_Renderers;
        MaterialPropertyBlock m_Mpb;

        Vector3 m_StartPos;
        Vector3 m_BodyImpactPos;     // where the ball crosses the player plane (splat origin)
        Vector3 m_EndPos;            // overshoot end point — m_OverreachMeters past the impact
        Vector3 m_PlayerPos;
        Vector3 m_PlayerForward;     // player's forward axis, used for plane-crossing tests

        UnityEngine.UI.Image m_LeftPanelImage;
        UnityEngine.UI.Image m_RightPanelImage;
        Color m_LightGrey = new Color(0.8f, 0.8f, 0.8f, 1.0f);

        public string TrialId { get; private set; }
        public bool IsComplete { get; private set; }

        public void Initialize(TrialDefinition trial, Vector3 playerPosition, Vector3 playerForward)
        {
            m_Trial = trial;
            TrialId = trial.trialId;

            Vector3 forward = playerForward.sqrMagnitude > 0.0001f ? playerForward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

            m_PlayerPos     = playerPosition;
            m_PlayerForward = forward;

            // Start point and the point where the ball nominally reaches the
            // participant's lateral plane.
            m_StartPos       = playerPosition + forward * trial.spawnDistance;
            //m_BodyImpactPos  = playerPosition + right   * trial.finalLateralOffset;
            m_BodyImpactPos = playerPosition
                + right * trial.finalLateralOffset
                + Vector3.up * (m_StartPos.y - playerPosition.y);

            // Every ball overshoots the impact point by m_OverreachMeters in
            // the trajectory direction. This is the actual endpoint of motion.
            Vector3 trajDir = (m_BodyImpactPos - m_StartPos);
            float baseLen = trajDir.magnitude;
            if (baseLen > 0.0001f && m_OverreachMeters > 0f)
            {
                trajDir /= baseLen;
                m_EndPos = m_BodyImpactPos + trajDir * m_OverreachMeters;
                // Stretch duration proportionally so the ball doesn't speed up.
                m_Duration = trial.Duration * ((baseLen + m_OverreachMeters) / baseLen);
            }
            else
            {
                m_EndPos = m_BodyImpactPos;
                m_Duration = trial.Duration;
            }

            Debug.Log($"[TrajectoryObjectController] Spawn trial={trial.trialId} cat={trial.category} " +
                      $"willHit={trial.WillHit} impactDistance={m_ImpactDistance:F3}m " +
                      $"ballCollisionRadius={m_BallCollisionRadius:F3}m " +
                      $"overreach={m_OverreachMeters:F2}m " +
                      $"playerPos={playerPosition} startPos={m_StartPos} bodyImpactPos={m_BodyImpactPos} endPos={m_EndPos} " +
                      $"startDistToPlayer={Vector3.Distance(m_StartPos, playerPosition):F2}m " +
                      $"impactDistToPlayer={Vector3.Distance(m_BodyImpactPos, playerPosition):F2}m");

            float diameter = trial.ballDiameter > 0f ? trial.ballDiameter : 0.175f;
            transform.localScale = Vector3.one * diameter;
            transform.position = m_StartPos;

            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_Mpb = new MaterialPropertyBlock();
            m_PinchColorApplied = false;
            m_SplatFired = false;
            m_PassedPlayerPlane = false;

            if (m_LeftPanel != null)  m_LeftPanel.SetActive(false);
            if (m_RightPanel != null) m_RightPanel.SetActive(false);

            if (m_LeftPanel != null)
                m_LeftPanelImage = m_LeftPanel.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (m_RightPanel != null)
                m_RightPanelImage = m_RightPanel.GetComponentInChildren<UnityEngine.UI.Image>(true);

            ResetPanelsToDefault();

            //CreateShadow(diameter);
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
            //UpdateShadow(pos);

            // Detect when the ball crosses the player plane (the plane through
            // the player's position perpendicular to the player's forward axis).
            // For a ball travelling along player.forward inward, this is the
            // moment the ball passes the participant.
            //   - Before the plane: dot > 0  (ball still in front)
            //   - On the plane:     dot ≈ 0
            //   - Past the plane:   dot < 0  (ball is behind the participant)
            float planeSign = Vector3.Dot(pos - m_PlayerPos, m_PlayerForward);

            // Hit-class splat: fires the first time we reach (or cross) the
            // configured impact distance in front of the player. For
            // m_ImpactDistance == 0 the splat fires when the ball crosses
            // the player plane. Hit-class balls STOP at the splash — they
            // don't continue past the participant (the splat itself is the
            // visual end of the trial).
            if (!m_SplatFired && m_Trial.WillHit)
            {
                bool reachedImpact;
                if (m_ImpactDistance > 0f)
                {
                    float ballRadius = (m_Trial.ballDiameter > 0f ? m_Trial.ballDiameter : 0.175f) * 0.5f;
                    float effectiveImpact = m_ImpactDistance + ballRadius + m_BallCollisionRadius;
                    reachedImpact = Vector3.Distance(pos, m_PlayerPos) <= effectiveImpact;
                }
                else
                {
                    reachedImpact = planeSign <= 0f;
                }
                if (reachedImpact)
                {
                    // Choose splat origin based on the impact mode:
                    //   ImpactDistance > 0: spawn at the ball's current world
                    //     position — that's where the actual collision was
                    //     detected, so the splat sits in front of the player
                    //     at the configured distance (e.g. 2 m forward).
                    //   ImpactDistance == 0: legacy body-plane behaviour —
                    //     spawn at m_BodyImpactPos which is in the player's
                    //     own plane (Z = 0 relative to player).
                    Vector3 splatOrigin = m_ImpactDistance > 0f ? pos : m_BodyImpactPos;
                    SpawnSplat(splatOrigin);
                    m_SplatFired = true;
                    // Hit-class: the splash IS the end of the ball's journey.
                    // Despawn now so it doesn't continue past the participant.
                    Debug.Log($"[TrajectoryObjectController] Hit-class splat — despawning ball at impact. trial={TrialId} " +
                              $"splatPos={splatOrigin} (distToPlayer={Vector3.Distance(splatOrigin, m_PlayerPos):F2}m)");
                    IsComplete = true;
                    m_Active = false;
                    Despawn();
                    return;
                }
            }

            // The instant the ball passes behind the participant plane, kill
            // the LEFT/RIGHT panels and the shadow so nothing lingers in the
            // peripheral view. The ball itself keeps travelling its remaining
            // overshoot so it visibly leaves the scene.
            if (!m_PassedPlayerPlane && planeSign < 0f)
            {
                m_PassedPlayerPlane = true;
                if (m_LeftPanel  != null) m_LeftPanel.SetActive(false);
                if (m_RightPanel != null) m_RightPanel.SetActive(false);
                //if (m_Shadow != null) m_Shadow.gameObject.SetActive(false);
            }

            // Reached the overshoot endpoint — clean up entirely. Despawn the
            // ball GameObject so the TaskManager doesn't need to wait through
            // its grace period to remove a stationary invisible ball.
            if (t >= 1f)
            {
                Debug.Log($"[TrajectoryObjectController] Trajectory end: trial={TrialId} " +
                          $"endDistToPlayer={Vector3.Distance(m_EndPos, m_PlayerPos):F3}m. " +
                          $"splatFired={m_SplatFired}. Despawning.");
                IsComplete = true;
                m_Active = false;
                Despawn();
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

        public void ResetPanelsToDefault()
        {
            if (m_LeftPanelImage  != null) m_LeftPanelImage.color  = m_LightGrey;
            if (m_RightPanelImage != null) m_RightPanelImage.color = m_LightGrey;
        }

        public void SetInstructionFeedback(bool isYesState)
        {
            if (isYesState)
            {
                if (m_LeftPanelImage  != null) m_LeftPanelImage.color  = m_LeftPanelColor;
                if (m_RightPanelImage != null) m_RightPanelImage.color = m_LightGrey;
            }
            else
            {
                if (m_LeftPanelImage  != null) m_LeftPanelImage.color  = m_LightGrey;
                if (m_RightPanelImage != null) m_RightPanelImage.color = m_RightPanelColor;
            }
        }

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
/* 
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
        } */

       // void UpdateShadow(Vector3 ballPos)
       // {
        //    if (m_Shadow == null) return;
            // Once the ball has crossed the player plane the shadow has been
            // hidden by the plane-crossing branch above; bail early so we
            // don't re-show it via scale updates.
        //    if (m_PassedPlayerPlane) return;

         //   m_Shadow.position = new Vector3(ballPos.x, m_GroundY + 0.01f, ballPos.z);

        //    float height = Mathf.Max(ballPos.y - m_GroundY, 0.1f);
        //    float scaleFactor = Mathf.Clamp(1f / (height * 0.5f + 0.5f), 0.3f, 1.5f);
        //    float baseDiam = m_Trial.ballDiameter > 0 ? m_Trial.ballDiameter : 0.175f;
        //    m_Shadow.localScale = new Vector3(baseDiam * 1.2f * scaleFactor, 0.005f, baseDiam * 1.2f * scaleFactor);

       //     m_Shadow.gameObject.SetActive(m_Active && !IsComplete);
       // }

        void SetVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = visible;
            //if (m_Shadow != null) m_Shadow.gameObject.SetActive(visible);
        }

        public void Despawn()
        {
            m_Active = false;
            IsComplete = true;
            if (m_LeftPanel  != null) m_LeftPanel.SetActive(false);
            if (m_RightPanel != null) m_RightPanel.SetActive(false);
            //if (m_Shadow != null) Destroy(m_Shadow.gameObject);
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