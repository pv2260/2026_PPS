using System;
using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Animates two LED-like transforms (left/right) from the far loom distance
    /// to the near loom distance, scaling them to produce the looming cue.
    ///
    /// Distances are controlled by PpsTaskAsset:
    ///     LoomStartDistance  = farthest SCORED point, D7
    ///     LoomEndDistance    = nearest SCORED point, D1
    ///     DistanceStageCount = number of equally spaced stages between them
    ///     WarmupDistanceMeters = where the LEDs first appear, farther than D7
    ///
    /// The warm-up phase glides the LEDs from WarmupDistanceMeters to D7 at the
    /// SAME velocity and along the SAME scale ramp as the scored loom, so that
    /// nothing perceptible happens at D7. D7 is not a visual event; it is only
    /// where the scoring window opens.
    /// </summary>
    public class LoomingPairController : MonoBehaviour
    {
        [SerializeField] private Transform m_LeftLed;
        [SerializeField] private Transform m_RightLed;
        [SerializeField] private DistanceLayout m_Layout;

        [Header("3D depth core")]
        [Tooltip("If true, spawns a small lit sphere child on each LED at Awake. The core gives the brain real stereoscopic + silhouette depth cues that the billboard glow shader cannot provide.")]
        [SerializeField] private bool m_SpawnDepthCore = true;

        [Tooltip("Physical diameter of the lit core sphere, in meters. Stays constant across the loom.")]
        [SerializeField, Min(0.005f)] private float m_CoreDiameterMeters = 0.10f;

        [Tooltip("Material applied to the core sphere. Recommended: URP/Lit or URP/Simple Lit, opaque.")]
        [SerializeField] private Material m_CoreMaterial;

        [Tooltip("Color used by the runtime fallback material if m_CoreMaterial is empty.")]
        [SerializeField] private Color m_CoreFallbackColor = new Color(1.0f, 0.55f, 0.15f, 1f);

        [Header("Fireball tail animation")]
        [SerializeField] private bool m_AnimateTail = true;

        [Tooltip("Tail length at D7, in meters. Keep small so the stimulus does not look close too early.")]
        [SerializeField] private float m_TailLengthAtD7 = 0.05f;

        [Tooltip("Tail length at D1, in meters.")]
        [SerializeField] private float m_TailLengthAtD1 = 0.80f;

        [SerializeField] private float m_HeadSizeAtD7 = 0.04f;
        [SerializeField] private float m_HeadSizeAtD1 = 0.10f;

        [SerializeField] private float m_IntensityAtD7 = 4f;
        [SerializeField] private float m_IntensityAtD1 = 18f;

        [SerializeField] private AnimationCurve m_TailGrowthCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly int TailLengthId = Shader.PropertyToID("_TailLength");
        private static readonly int HeadSizeId = Shader.PropertyToID("_HeadSize");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private MaterialPropertyBlock m_LeftBlock;
        private MaterialPropertyBlock m_RightBlock;
        private Renderer m_LeftRenderer;
        private Renderer m_RightRenderer;

        private Transform m_LeftCore;
        private Transform m_RightCore;

        static readonly DistanceStage[] k_OrderedStages =
        {
            DistanceStage.D7,
            DistanceStage.D6,
            DistanceStage.D5,
            DistanceStage.D4,
            DistanceStage.D3,
            DistanceStage.D2,
            DistanceStage.D1
        };

        public DistanceLayout Layout
        {
            get => m_Layout;
            set => m_Layout = value;
        }

        public DistanceStage CurrentStage { get; private set; } = DistanceStage.None;
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Time.timeAsDouble at which the LEDs became visible and began gliding.
        /// This is the true visual stimulus onset.
        /// </summary>
        public double LoomOnsetTime { get; private set; } = double.NaN;

        private void Awake()
        {
            // Find glow/comet renderers BEFORE adding the depth-core children.
            // This prevents GetComponentInChildren from accidentally grabbing
            // the depth-core sphere instead of the glow shader renderer.
            m_LeftRenderer = FindFireballRenderer(m_LeftLed);
            m_RightRenderer = FindFireballRenderer(m_RightLed);

            m_LeftBlock = new MaterialPropertyBlock();
            m_RightBlock = new MaterialPropertyBlock();

            if (m_SpawnDepthCore)
            {
                m_LeftCore = EnsureCoreChild(m_LeftLed, "DepthCore_Left");
                m_RightCore = EnsureCoreChild(m_RightLed, "DepthCore_Right");
            }

            if (m_LeftRenderer == null)
                Debug.LogWarning("[LoomingPairController] No fireball/glow renderer found on LeftLED.");

            if (m_RightRenderer == null)
                Debug.LogWarning("[LoomingPairController] No fireball/glow renderer found on RightLED.");
        }

        private Renderer FindFireballRenderer(Transform root)
        {
            if (root == null)
                return null;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            Renderer firstNonCore = null;

            foreach (Renderer r in renderers)
            {
                if (r == null)
                    continue;

                if (r.name.Contains("DepthCore"))
                    continue;

                if (firstNonCore == null)
                    firstNonCore = r;

                Material mat = r.sharedMaterial;

                if (mat == null)
                    continue;

                bool hasFireballProperties =
                    mat.HasProperty(TailLengthId) ||
                    mat.HasProperty(HeadSizeId) ||
                    mat.HasProperty(IntensityId);

                if (hasFireballProperties)
                    return r;
            }

            return firstNonCore;
        }

        /// <summary>
        /// Creates or finds a child sphere that gives the LED a solid 3D silhouette.
        /// </summary>
        private Transform EnsureCoreChild(Transform led, string childName)
        {
            if (led == null)
                return null;

            Transform existing = led.Find(childName);
            if (existing != null)
                return existing;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = childName;

            Collider col = core.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            core.transform.SetParent(led, worldPositionStays: false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            core.transform.localScale = Vector3.one;

            MeshRenderer renderer = core.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = ResolveCoreMaterial();

            return core.transform;
        }

        private Material ResolveCoreMaterial()
        {
            if (m_CoreMaterial != null)
                return m_CoreMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Universal Render Pipeline/Simple Lit") ??
                Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogWarning(
                    "[LoomingPairController] No lit shader available for the depth core. " +
                    "Assign a Material in the inspector."
                );
                return null;
            }

            Material mat = new Material(shader)
            {
                color = m_CoreFallbackColor
            };

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", m_CoreFallbackColor * 0.6f);
            }

            return mat;
        }

        private void UpdateFireballTail(float progress01)
        {
            if (!m_AnimateTail)
                return;

            float t = Mathf.Clamp01(progress01);
            t = m_TailGrowthCurve != null ? m_TailGrowthCurve.Evaluate(t) : t;

            float tailLength = Mathf.Lerp(m_TailLengthAtD7, m_TailLengthAtD1, t);
            float headSize = Mathf.Lerp(m_HeadSizeAtD7, m_HeadSizeAtD1, t);
            float intensity = Mathf.Lerp(m_IntensityAtD7, m_IntensityAtD1, t);

            ApplyFireballMaterial(m_LeftRenderer, m_LeftBlock, tailLength, headSize, intensity);
            ApplyFireballMaterial(m_RightRenderer, m_RightBlock, tailLength, headSize, intensity);
        }

        private void ApplyFireballMaterial(
            Renderer renderer,
            MaterialPropertyBlock block,
            float tailLength,
            float headSize,
            float intensity)
        {
            if (renderer == null || block == null)
                return;

            renderer.GetPropertyBlock(block);

            block.SetFloat(TailLengthId, tailLength);
            block.SetFloat(HeadSizeId, headSize);
            block.SetFloat(IntensityId, intensity);

            renderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Computes the child's localScale so its world-space diameter matches
        /// m_CoreDiameterMeters regardless of the parent LED's localScale.
        /// </summary>
        private Vector3 CompensateCoreScale(Vector3 parentScale)
        {
            float Inv(float s)
            {
                return Mathf.Abs(s) > 1e-4f
                    ? m_CoreDiameterMeters / s
                    : m_CoreDiameterMeters;
            }

            return new Vector3(
                Inv(parentScale.x),
                Inv(parentScale.y),
                Inv(parentScale.z)
            );
        }

        /// <summary>
        /// Run one looming pass.
        ///
        /// participantShoulderWidthMeters:
        ///     Used to compute the left/right separation at runtime.
        ///     Narrow = participant shoulder width.
        ///     Wide   = participant shoulder width + asset wide offset.
        ///
        /// onStageEnter fires each time the pair crosses into a new distance stage.
        /// It does NOT fire during the warm-up.
        /// </summary>
        public IEnumerator RunLoom(
            PpsTrialDefinition trial,
            PpsTaskAsset asset,
            float participantShoulderWidthMeters,
            Action<DistanceStage> onStageEnter = null)
        {
            if (asset == null)
            {
                Debug.LogError("[LoomingPairController] Cannot run loom: asset is null.");
                yield break;
            }

            if (m_LeftLed == null || m_RightLed == null || m_Layout == null)
            {
                Debug.LogError("[LoomingPairController] Missing references: LEDs or layout.");
                yield break;
            }

            LoomOnsetTime = double.NaN;

            // Make sure the layout uses the current asset values.
            m_Layout.ConfigureFromAsset(asset);

            float duration = asset.DurationFor(trial.speed);
            float separation = asset.SeparationFor(trial.width, participantShoulderWidthMeters);
            AnimationCurve curve = asset.MotionCurve;

            Vector3 start = m_Layout.StartCenter; // D7
            Vector3 end = m_Layout.EndCenter;     // D1

            Camera cam = Camera.main;

            if (cam != null)
            {
                Debug.Log(
                    $"[REAL VIEW DISTANCE CHECK] " +
                    $"cameraPosition={cam.transform.position} | " +
                    $"layoutPosition={m_Layout.transform.position} | " +
                    $"assetLoomStart={asset.LoomStartDistance:F3}m | " +
                    $"assetLoomEnd={asset.LoomEndDistance:F3}m | " +
                    $"D7_distance_from_camera={Vector3.Distance(cam.transform.position, start):F3}m | " +
                    $"D1_distance_from_camera={Vector3.Distance(cam.transform.position, end):F3}m | " +
                    $"layout_lossyScale={m_Layout.transform.lossyScale}"
                );
            }

            Debug.Log(
                $"[LOOM TRAVEL CHECK] " +
                $"travelDistance={Vector3.Distance(start, end):F3}m | " +
                $"duration={duration:F3}s | " +
                $"meanSpeed={Vector3.Distance(start, end) / Mathf.Max(0.0001f, duration):F3}m/s"
            );

            m_LeftLed.gameObject.SetActive(true);
            m_RightLed.gameObject.SetActive(true);

            // Start with a small/faint tail.
            UpdateFireballTail(0f);

            LoomOnsetTime = Time.timeAsDouble;
            IsRunning = true;

            // ------------------------------------------------------------------
            // Warm-up phase: WarmupDistance -> D7
            // ------------------------------------------------------------------

            float scoredTravel = asset.LoomStartDistance - asset.LoomEndDistance;
            float warmupDistance = asset.WarmupDistanceMeters;
            float extra = warmupDistance - asset.LoomStartDistance;

            float warmupDuration = asset.WarmupLeadSeconds(trial.speed);

            bool doWarmup =
                warmupDuration > 0f &&
                (start - end).sqrMagnitude > 0.0001f;

            if (!doWarmup && extra <= 0f)
            {
                Debug.LogWarning(
                    $"[LoomingPairController] Warm-up disabled: WarmupDistanceMeters " +
                    $"({warmupDistance:F2}m) must be GREATER than LoomStartDistance " +
                    $"({asset.LoomStartDistance:F2}m). The LEDs will pop into existence at D7."
                );
            }

            if (doWarmup)
            {
                Vector3 backDir = (start - end).normalized;
                Vector3 warmupOrigin = start + backDir * extra;

                Quaternion warmupRotation =
                    Quaternion.LookRotation((end - start).normalized, Vector3.up);

                Vector3 sideDir = Vector3.right;

                float warmupStartT = -extra / Mathf.Max(0.0001f, scoredTravel);

                float loomVelocity = scoredTravel / Mathf.Max(0.0001f, duration);

                Debug.Log(
                    $"[LOOM WARMUP] from {warmupOrigin} -> {start} | " +
                    $"distance={extra:F2}m | duration={warmupDuration:F2}s | " +
                    $"velocity={loomVelocity:F3}m/s | scaleT={warmupStartT:F3}"
                );

                float warmupElapsed = 0f;

                while (warmupElapsed < warmupDuration)
                {
                    warmupElapsed += Time.deltaTime;

                    float wt = Mathf.Clamp01(warmupElapsed / warmupDuration);

                    Vector3 warmupCenter = Vector3.Lerp(warmupOrigin, start, wt);

                    // Keep tail small/faint during warm-up.
                    UpdateFireballTail(0f);

                    Vector3 wScale = Vector3.one;

                    m_LeftLed.position = warmupCenter - sideDir * (separation * 0.5f);
                    m_RightLed.position = warmupCenter + sideDir * (separation * 0.5f);

                    m_LeftLed.rotation = warmupRotation;
                    m_RightLed.rotation = warmupRotation;

                    m_LeftLed.localScale = wScale;
                    m_RightLed.localScale = wScale;

                    if (m_LeftCore != null)
                        m_LeftCore.localScale = CompensateCoreScale(wScale);

                    if (m_RightCore != null)
                        m_RightCore.localScale = CompensateCoreScale(wScale);

                    yield return null;
                }
            }

            // ------------------------------------------------------------------
            // Scored loom: D7 -> D1
            // ------------------------------------------------------------------

            CurrentStage = DistanceStage.D7;
            onStageEnter?.Invoke(DistanceStage.D7);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float curved = curve != null ? Mathf.Clamp01(curve.Evaluate(t)) : t;

                // Fireball effect grows as the stimulus moves from D7 to D1.
                UpdateFireballTail(curved);

                Vector3 center = Vector3.Lerp(start, end, curved);
                // Apparent growth now comes from real distance/perspective + fireball shader.
                Vector3 scale = Vector3.one;

                Vector3 moveDirection = (end - start).normalized;

                if (moveDirection.sqrMagnitude < 0.0001f)
                    moveDirection = Vector3.forward;

                Quaternion beamRotation = Quaternion.LookRotation(moveDirection, Vector3.up);

                Vector3 sideDirection = Vector3.right;

                m_LeftLed.position = center - sideDirection * (separation * 0.5f);
                m_RightLed.position = center + sideDirection * (separation * 0.5f);

                m_LeftLed.rotation = beamRotation;
                m_RightLed.rotation = beamRotation;

                m_LeftLed.localScale = scale;
                m_RightLed.localScale = scale;

                if (m_LeftCore != null)
                    m_LeftCore.localScale = CompensateCoreScale(scale);

                if (m_RightCore != null)
                    m_RightCore.localScale = CompensateCoreScale(scale);

                DistanceStage newStage = StageAtProgress(curved, asset);

                if (newStage != CurrentStage)
                {
                    CurrentStage = newStage;
                    onStageEnter?.Invoke(newStage);

                    Debug.Log(
                        $"[LOOM STAGE ENTER] " +
                        $"stage={newStage} | " +
                        $"curvedProgress={curved:F3} | " +
                        $"elapsed={elapsed:F3}s"
                    );
                }

                yield return null;
            }

            // Ensure final near-state is reached visually before hiding.
            UpdateFireballTail(1f);

            m_LeftLed.gameObject.SetActive(false);
            m_RightLed.gameObject.SetActive(false);

            IsRunning = false;
            CurrentStage = DistanceStage.None;
        }

        /// <summary>
        /// Converts normalized progress into the current stage.
        /// </summary>
        private DistanceStage StageAtProgress(float progress, PpsTaskAsset asset)
        {
            progress = Mathf.Clamp01(progress);

            int stageCount = Mathf.Clamp(
                asset.DistanceStageCount,
                2,
                k_OrderedStages.Length
            );

            int intervalCount = stageCount - 1;

            int index = Mathf.FloorToInt(progress * intervalCount + 0.0001f);
            index = Mathf.Clamp(index, 0, intervalCount);

            return k_OrderedStages[index];
        }

        public void ForceHide()
        {
            if (m_LeftLed != null)
                m_LeftLed.gameObject.SetActive(false);

            if (m_RightLed != null)
                m_RightLed.gameObject.SetActive(false);

            IsRunning = false;
            CurrentStage = DistanceStage.None;
        }
    }
}