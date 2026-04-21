using System;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Abstraction for vibrotactile stimulus delivery. Lets the task manager
    /// stay decoupled from the specific hardware (or on-screen placeholder)
    /// used to deliver the pulse.
    /// </summary>
    public interface IVibrotactileOutput
    {
        /// <summary>Display name used in logs (e.g. "OnScreenPlaceholder", "ArduinoERM", "QuestHaptic").</summary>
        string DeviceName { get; }

        /// <summary>
        /// Fire a single pulse. Implementations should be non-blocking — the call
        /// returns immediately and the pulse plays out over <paramref name="durationMs"/>.
        /// </summary>
        void Fire(float intensity01, float durationMs);

        /// <summary>Fires at the moment the pulse physically starts. Used to log <see cref="PpsTrialResult.vibrationFiredTime"/>.</summary>
        event Action PulseStarted;
    }
}
