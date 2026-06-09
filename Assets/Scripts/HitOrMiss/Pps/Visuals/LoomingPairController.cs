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
