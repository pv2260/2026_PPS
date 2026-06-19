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
    ///     LoomStartDistance = farthest point, D7
    ///     LoomEndDistance   = nearest point, D1
    ///     DistanceStageCount = number of equally spaced stages between them
    ///
    /// The controller reports stage entry using equally spaced progress values,
    /// so the task manager can fire vibrotactile events at D7..D1 without
    /// manually defining every intermediate distance.
    /// </summary>
    public class LoomingPairController : MonoBehaviour
    {
        [SerializeField] Transform m_LeftLed;
        [SerializeField] Transform m_RightLed;
        [SerializeField] DistanceLayout m_Layout;

        [Header("3D depth core")]
        [Tooltip("If true, spawns a small lit sphere child on each LED at Awake. The core gives the brain real stereoscopic + silhouette depth cues that the billboard glow shader can't provide. Highly recommended for distance perception.")]
        [SerializeField] bool m_SpawnDepthCore = true;

        [Tooltip("Physical diameter of the lit core sphere, in meters. Stays constant across the loom; Unity's perspective camera produces the apparent growth as the LED approaches.")]
        [SerializeField, Min(0.005f)] float m_CoreDiameterMeters = 0.10f;

        [Tooltip("Material applied to the core sphere. Recommended: URP/Lit or URP/Simple Lit, opaque. If empty, a URP/Lit fallback is created at runtime so the sphere is visible.")]
        [SerializeField] Material m_CoreMaterial;

        [Tooltip("Color used by the runtime fallback material (only if m_CoreMaterial is empty). Pick something warm/bright so the core reads against a dark passthrough scene.")]
        [SerializeField] Color m_CoreFallbackColor = new Color(1.0f, 0.55f, 0.15f, 1f);

        Transform m_LeftCore;
        Transform m_RightCore;

        public AnimationCurve speedCurve;

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

        void Awake()
        {
            if (m_SpawnDepthCore)
            {
                m_LeftCore  = EnsureCoreChild(m_LeftLed,  "DepthCore_Left");
                m_RightCore = EnsureCoreChild(m_RightLed, "DepthCore_Right");
            }
        }

        /// <summary>
        /// Creates (or finds) a child sphere that gives the LED a solid 3D
        /// silhouette, lit by the scene. The glow billboard on the parent LED
        /// stays — the core is an additional cue, not a replacement.
        /// </summary>
        Transform EnsureCoreChild(Transform led, string childName)
        {
            if (led == null) return null;

            Transform existing = led.Find(childName);
            if (existing != null) return existing;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = childName;

            // Drop the collider Unity attaches by default; PPS doesn't need physics on the LEDs.
            Collider col = core.GetComponent<Collider>();
            if (col != null) Destroy(col);

            core.transform.SetParent(led, worldPositionStays: false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            core.transform.localScale    = Vector3.one;

            var renderer = core.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = ResolveCoreMaterial();

            return core.transform;
        }

        Material ResolveCoreMaterial()
        {
            if (m_CoreMaterial != null) return m_CoreMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Universal Render Pipeline/Simple Lit") ??
                Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogWarning("[LoomingPairController] No lit shader available for the depth core. " +
                                 "The core will render in magenta; assign a Material in the inspector.");
                return null;
            }

            var mat = new Material(shader) { color = m_CoreFallbackColor };
            // Add a touch of emission so the sphere stays visible even if the
            // scene has no directional light. Subtle on purpose: we still want
            // the shading gradient to read.
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", m_CoreFallbackColor * 0.6f);
            }
            return mat;
        }

        /// <summary>
        /// Computes the child's localScale so its world-space diameter matches
        /// m_CoreDiameterMeters regardless of the parent LED's localScale
        /// (which is driven by the glow shader's size, not by physical units).
        /// </summary>
        Vector3 CompensateCoreScale(Vector3 parentScale)
        {
            float Inv(float s) => Mathf.Abs(s) > 1e-4f ? m_CoreDiameterMeters / s : m_CoreDiameterMeters;
            return new Vector3(Inv(parentScale.x), Inv(parentScale.y), Inv(parentScale.z));
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

            // Make sure the layout uses the current asset values:
            // LoomStartDistance, LoomEndDistance, and the computed intermediate stages.
            m_Layout.ConfigureFromAsset(asset);

            float duration = asset.DurationFor(trial.speed);

            // Participant-specific separation:
            // Narrow = shoulder width
            // Wide   = shoulder width + wide offset
            float separation = asset.SeparationFor(trial.width, participantShoulderWidthMeters);

            AnimationCurve curve = asset.MotionCurve;

            Vector3 start = m_Layout.StartCenter; // D7 / far start
            Vector3 end = m_Layout.EndCenter;     // D1 / near end

            Camera cam = Camera.main;

            if (cam != null)
            {
                Debug.Log(
                    $"[REAL VIEW DISTANCE CHECK] " +
                    $"cameraPosition={cam.transform.position} | " +
                    $"layoutPosition={m_Layout.transform.position} | " +
                    $"D7_world={start} | " +
                    $"D1_world={end} | " +
                    $"D7_distance_from_camera={Vector3.Distance(cam.transform.position, start):F3}m | " +
                    $"D1_distance_from_camera={Vector3.Distance(cam.transform.position, end):F3}m | " +
                    $"layout_lossyScale={m_Layout.transform.lossyScale}"
                );
            }

            Debug.Log(
                $"[LOOM TRAVEL CHECK] " +
                $"start={start} | " +
                $"end={end} | " +
                $"travelDistance={Vector3.Distance(start, end):F3}m | " +
                $"duration={duration:F3}s | " +
                $"speed={Vector3.Distance(start, end) / duration:F3}m/s"
            );


            m_LeftLed.gameObject.SetActive(true);
            m_RightLed.gameObject.SetActive(true);

            IsRunning = true;

            // ---- Warm-up phase ----
            // Before the timed loom (D7 -> D1) starts, glide the LEDs from a
            // farther pre-position toward D7. Gives the brain time to perceive
            // the lights before the stage-scoring window begins, so the very
            // first vibration trigger at D7 doesn't catch the participant
            // staring at an empty void. No onStageEnter callback fires during
            // this phase — it is purely visual lead-in, not a measured stage.
            float warmupDuration = asset.WarmupDurationSeconds;
            float warmupDistance = asset.WarmupDistanceMeters;
            bool doWarmup =
                warmupDuration > 0f
                && warmupDistance > asset.LoomStartDistance
                && (start - end).sqrMagnitude > 0.0001f;

            if (doWarmup)
            {
                // Direction from D1 (near) back toward D7 (far), normalized.
                Vector3 backDir = (start - end).normalized;
                float extra = warmupDistance - asset.LoomStartDistance;
                Vector3 warmupOrigin = start + backDir * extra;

                Quaternion warmupRotation =
                    Quaternion.LookRotation((end - start).normalized, Vector3.up);
                Vector3 warmupScale = asset.ScaleAtD7;
                Vector3 sideDir = Vector3.right;

                Debug.Log(
                    $"[LOOM WARMUP] from {warmupOrigin} -> {start} | " +
                    $"distance={extra:F2}m | duration={warmupDuration:F2}s"
                );

                float warmupElapsed = 0f;
                while (warmupElapsed < warmupDuration)
                {
                    warmupElapsed += Time.deltaTime;
                    float wt = Mathf.Clamp01(warmupElapsed / warmupDuration);
                    Vector3 warmupCenter = Vector3.Lerp(warmupOrigin, start, wt);

                    m_LeftLed.position  = warmupCenter - sideDir * (separation * 0.5f);
                    m_RightLed.position = warmupCenter + sideDir * (separation * 0.5f);
                    m_LeftLed.rotation  = warmupRotation;
                    m_RightLed.rotation = warmupRotation;
                    m_LeftLed.localScale  = warmupScale;
                    m_RightLed.localScale = warmupScale;

                    if (m_LeftCore  != null) m_LeftCore.localScale  = CompensateCoreScale(warmupScale);
                    if (m_RightCore != null) m_RightCore.localScale = CompensateCoreScale(warmupScale);

                    yield return null;
                }
            }

            CurrentStage = DistanceStage.D7;
            onStageEnter?.Invoke(DistanceStage.D7);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float curved = curve != null ? Mathf.Clamp01(curve.Evaluate(t)) : t;

                Vector3 center = Vector3.Lerp(start, end, curved);
                Vector3 scale = Vector3.Lerp(asset.ScaleAtD7, asset.ScaleAtD1, curved);

                if (elapsed < Time.deltaTime * 2f)
                {
                    Debug.Log(
                        $"[LOOM PPS] " +
                        $"speed={trial.speed} | " +
                        $"width={trial.width} | " +
                        $"participantShoulder={participantShoulderWidthMeters:F3}m | " +
                        $"separation={separation:F3}m | " +
                        $"D7Scale={asset.ScaleAtD7} | " +
                        $"D1Scale={asset.ScaleAtD1} | " +
                        $"currentScale={scale}"
                    );
                }

                Vector3 moveDirection = (end - start).normalized;

                if (moveDirection.sqrMagnitude < 0.0001f)
                {
                    moveDirection = Vector3.forward;
                }

                Quaternion beamRotation = Quaternion.LookRotation(moveDirection, Vector3.up);

                // Left/right axis.
                // For now this is world right.
                // Later, if the participant turns, this should come from the body/XR anchor.
                Vector3 sideDirection = Vector3.right;

                m_LeftLed.position = center - sideDirection * (separation * 0.5f);
                m_RightLed.position = center + sideDirection * (separation * 0.5f);

                m_LeftLed.rotation = beamRotation;
                m_RightLed.rotation = beamRotation;

                m_LeftLed.localScale = scale;
                m_RightLed.localScale = scale;

                // Keep the depth core at a constant world-space diameter even
                // though the parent LED's scale changes during the loom. This
                // is what gives the brain a real-distance cue: the solid lit
                // sphere grows naturally via perspective, not via artificial
                // scaling.
                if (m_LeftCore != null)  m_LeftCore.localScale  = CompensateCoreScale(scale);
                if (m_RightCore != null) m_RightCore.localScale = CompensateCoreScale(scale);

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

            m_LeftLed.gameObject.SetActive(false);
            m_RightLed.gameObject.SetActive(false);

            IsRunning = false;
            CurrentStage = DistanceStage.None;
        }

        /// <summary>
        /// Converts normalized curved progress into the current stage.
        /// 
        /// With 7 stages:
        ///     D7 = progress 0.000
        ///     D6 = progress 0.167
        ///     D5 = progress 0.333
        ///     D4 = progress 0.500
        ///     D3 = progress 0.667
        ///     D2 = progress 0.833
        ///     D1 = progress 1.000
        ///
        /// This means stages are defined by equal progress intervals between
        /// the start and end distances.
        /// </summary>
        DistanceStage StageAtProgress(float progress, PpsTaskAsset asset)
        {
            progress = Mathf.Clamp01(progress);

            int stageCount = Mathf.Clamp(asset.DistanceStageCount, 2, k_OrderedStages.Length);
            int intervalCount = stageCount - 1;

            int index = Mathf.FloorToInt(progress * intervalCount + 0.0001f);
            index = Mathf.Clamp(index, 0, intervalCount);

            return k_OrderedStages[index];
        }

        public void ForceHide()
        {
            if (m_LeftLed != null) m_LeftLed.gameObject.SetActive(false);
            if (m_RightLed != null) m_RightLed.gameObject.SetActive(false);

            IsRunning = false;
            CurrentStage = DistanceStage.None;
        }
    }
}
