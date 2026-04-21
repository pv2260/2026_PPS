using System;
using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Placeholder <see cref="IVibrotactileOutput"/> used before real hardware is wired.
    /// Toggles a <see cref="VibrationMessageView"/> on screen for the pulse duration.
    /// Swap for a hardware driver by changing the <see cref="PpsTaskManager"/>'s output
    /// field — no trial-logic changes required.
    /// </summary>
    public class OnScreenVibrationOutput : MonoBehaviour, IVibrotactileOutput
    {
        [SerializeField] VibrationMessageView m_View;

        public string DeviceName => "OnScreenPlaceholder";
        public event Action PulseStarted;

        public VibrationMessageView View
        {
            get => m_View;
            set => m_View = value;
        }

        public void Fire(float intensity01, float durationMs)
        {
            StartCoroutine(FireRoutine(durationMs));
        }

        IEnumerator FireRoutine(float durationMs)
        {
            if (m_View != null) m_View.Show();
            PulseStarted?.Invoke();
            yield return new WaitForSeconds(durationMs / 1000f);
            if (m_View != null) m_View.Hide();
        }
    }
}
