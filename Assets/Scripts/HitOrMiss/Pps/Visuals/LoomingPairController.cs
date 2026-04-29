using System;
using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Animates two LED-like transforms (left/right) from D4 → D1 along the
    /// <see cref="DistanceLayout"/>, scaling them to produce the looming cue.
    /// Reports the current <see cref="DistanceStage"/> via a callback so the
    /// task manager can fire a vibrotactile event at a trial-specified stage
    /// without hard-coded timing thresholds.
    /// </summary>
    public class LoomingPairController : MonoBehaviour
    {
        [SerializeField] Transform m_LeftLed;
        [SerializeField] Transform m_RightLed;
        [SerializeField] DistanceLayout m_Layout;

        public DistanceLayout Layout
        {
            get => m_Layout;
            set => m_Layout = value;
        }

        public DistanceStage CurrentStage { get; private set; } = DistanceStage.None;
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Run one looming pass. <paramref name="onStageEnter"/> fires each time the
        /// pair crosses into a new stage (D4→D3, D3→D2, D2→D1).
        /// </summary>
        public IEnumerator RunLoom(PpsTrialDefinition trial, PpsTaskAsset asset, Action<DistanceStage> onStageEnter = null)
        {
            if (asset == null) yield break;
            if (m_LeftLed == null || m_RightLed == null || m_Layout == null)
            {
                Debug.LogError("[LoomingPairController] Missing references (LEDs or layout).");
                yield break;
            }

            float duration = asset.DurationFor(trial.speed);
            float separation = asset.SeparationFor(trial.width);
            var curve = asset.MotionCurve;
            Vector3 start = m_Layout.StartCenter;
            Vector3 end = m_Layout.EndCenter;

            m_LeftLed.gameObject.SetActive(true);
            m_RightLed.gameObject.SetActive(true);
            IsRunning = true;
            CurrentStage = DistanceStage.D4;
            onStageEnter?.Invoke(DistanceStage.D4);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curved = curve != null ? curve.Evaluate(t) : t;

                Vector3 center = Vector3.Lerp(start, end, curved);
                Vector3 scale = Vector3.Lerp(asset.ScaleAtD4, asset.ScaleAtD1, curved);
                Vector3 right = transform.right;

                m_LeftLed.position = center - right * (separation * 0.5f);
                m_RightLed.position = center + right * (separation * 0.5f);
                m_LeftLed.localScale = scale;
                m_RightLed.localScale = scale;

                var newStage = m_Layout.StageAt(curved);
                if (newStage != CurrentStage)
                {
                    CurrentStage = newStage;
                    onStageEnter?.Invoke(newStage);
                }

                yield return null;
            }

            m_LeftLed.gameObject.SetActive(false);
            m_RightLed.gameObject.SetActive(false);
            IsRunning = false;
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
