using System;
using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    public class LoomingPairController : MonoBehaviour
    {
        [SerializeField] Transform m_LeftLed;
        [SerializeField] Transform m_RightLed;
        [SerializeField] DistanceLayout m_Layout;

        [Header("Debug")]
        [SerializeField] bool m_LogLoom = true;

        public DistanceLayout Layout
        {
            get => m_Layout;
            set => m_Layout = value;
        }

        public DistanceStage CurrentStage { get; private set; } = DistanceStage.None;
        public bool IsRunning { get; private set; }

        void Awake()
        {
            ForceHide();
        }

        public IEnumerator RunLoom(
    
            PpsTrialDefinition trial,
            PpsTaskAsset asset,
            Action<DistanceStage> onStageEnter = null)
        {
            Debug.LogError("[LoomingPairController] RUN LOOM CALLED");
            if (asset == null)
            {
                Debug.LogError("[LoomingPairController] Missing PpsTaskAsset.");
                yield break;
            }

            if (m_LeftLed == null || m_RightLed == null || m_Layout == null)
            {
                Debug.LogError("[LoomingPairController] Missing references: Left LED, Right LED, or Layout.");
                yield break;
            }

            float duration = asset.DurationFor(trial.speed);
            float separation = asset.SeparationFor(trial.width);
            var curve = asset.MotionCurve;

            Vector3 start = m_Layout.StartCenter;
            Vector3 end = m_Layout.EndCenter;
            Vector3 right = transform.right;

            if (m_LogLoom)
            {
                Debug.Log(
                    $"[LoomingPairController] RunLoom {trial.trialId} | " +
                    $"duration={duration}, separation={separation}, start={start}, end={end}"
                );
            }

            m_LeftLed.gameObject.SetActive(true);
            m_RightLed.gameObject.SetActive(true);

            IsRunning = true;
            CurrentStage = DistanceStage.D4;

            // Place immediately at D4 before first frame.
            Vector3 startScale = asset.ScaleAtD4;

            m_LeftLed.position = start - right * (separation * 0.5f);
            m_RightLed.position = start + right * (separation * 0.5f);
            m_LeftLed.localScale = startScale;
            m_RightLed.localScale = startScale;

            onStageEnter?.Invoke(DistanceStage.D4);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float curved = curve != null ? curve.Evaluate(t) : t;

                Vector3 center = Vector3.Lerp(start, end, curved);
                Vector3 scale = Vector3.Lerp(asset.ScaleAtD4, asset.ScaleAtD1, curved);

                m_LeftLed.position = center - right * (separation * 0.5f);
                m_RightLed.position = center + right * (separation * 0.5f);

                m_LeftLed.localScale = scale;
                m_RightLed.localScale = scale;

                DistanceStage newStage = m_Layout.StageAt(curved);

                if (newStage != CurrentStage)
                {
                    CurrentStage = newStage;

                    if (m_LogLoom)
                        Debug.Log($"[LoomingPairController] Stage entered: {newStage}");

                    onStageEnter?.Invoke(newStage);
                }

                yield return null;
            }

            ForceHide();
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