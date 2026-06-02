using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Runs PPS trials and records trial-level data.
    ///
    /// Current responsibilities:
    /// - Run individual trials
    /// - Control looming visual stimulus
    /// - Trigger vibrotactile stimulation
    /// - Capture participant responses
    /// - Emit EEG/event markers
    /// - Write CSV output
    ///
    /// Note:
    /// In the refactored architecture, higher-level experiment flow
    /// should move out of this class into PPSAppController.
    /// </summary>
    public class PpsTaskManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PpsTaskAsset m_TaskAsset;

        [Header("Scene references")]
        [SerializeField] private LoomingPairController m_Loom;
        [SerializeField] private DistanceLayout m_Layout;

        [Header("Participant geometry")]
        [Tooltip("Participant shoulder width in meters. Narrow PPS width uses this value. Wide PPS width adds the asset's wide offset.")]
        [SerializeField] private float m_ParticipantShoulderWidthMeters = 0.42f;

        [Header("Response Feedback Audio")]
        [SerializeField] private AudioSource m_ResponseAudioSource;
        [SerializeField] private AudioClip m_ResponseRegisteredClip;
        [SerializeField, Range(0f, 1f)] private float m_ResponseRegisteredVolume = 0.35f;

        public float ParticipantShoulderWidthMeters
        {
            get => m_ParticipantShoulderWidthMeters;
            set => m_ParticipantShoulderWidthMeters = Mathf.Max(0.01f, value);
        }

        /// <summary>
        /// Use this if the clinician GUI stores shoulder width in centimeters.
        /// Example: 42 cm becomes 0.42 m.
        /// </summary>
        public void SetParticipantShoulderWidthCm(float shoulderWidthCm)
        {
            if (shoulderWidthCm > 0f)
                m_ParticipantShoulderWidthMeters = shoulderWidthCm / 100f;
        }

        [Header("Practice Feedback")]
        [SerializeField] private SessionFlowPanels m_Ui;

        [SerializeField] private PpsFeedback m_Feedback;

        // Tracks whether the currently running trial is a practice trial.
        // Used only to decide whether feedback should be shown.
        private bool m_CurrentTrialIsPractice;

        // True after the vibration command has been sent.
        // Used to classify responses as valid vibration responses or false alarms.
        private bool m_VibrationHasFired;

        [Header("Output")]
        [SerializeField] private MonoBehaviour m_VibrotactileOutputBehaviour;

        [Header("Logging")]
        [SerializeField] private EegMarkerEmitter m_MarkerEmitter;

        [Header("Response Input")]
        [Tooltip("Drag the GameObject with an IResponseInputSource component (e.g. XrTriggerResponseInputSource).")]
        [SerializeField] private MonoBehaviour m_InputSourceBehaviour;

        // Runtime interface references.
        // These allow the task manager to work with different vibration and input implementations.
        private IVibrotactileOutput m_Output;
        private IResponseInputSource m_InputSource;

        // Response-capture state for the active trial.
        private bool m_CaptureResponses;
        private double m_VibrationFiredTime;
        private double m_FirstResponseTime;
        private bool m_Responded;

        // Random number generator used for inter-trial intervals.
        private System.Random m_ItiRng;

        // CSV logging state.
        private StreamWriter m_CsvWriter;
        private string m_CsvPath;

        // External systems can subscribe to these events to react to trial start/end.
        public event Action<PpsTrialDefinition> TrialStarted;
        public event Action<PpsTrialResult> TrialCompleted;

        // ---- Block tracking (consumed by HitMissNetworkServer status feed) ----
        // PpsTaskManager doesn't own block scheduling — PPSAppController does —
        // so each RunTrials call is treated as one logical "block" of trials.
        // These counters are updated as trials advance and reset at RunTrials
        // entry so /api/status reflects whichever phase is currently active.

        int m_CurrentBlockIndex;
        int m_TrialsCompletedInBlock;
        int m_TotalTrialsInBlock;

        /// <summary>0-based index of the block currently being run.</summary>
        public int CurrentBlockIndex => m_CurrentBlockIndex;

        /// <summary>Number of trials finished so far within the current RunTrials call.</summary>
        public int TrialsCompletedInBlock => m_TrialsCompletedInBlock;

        /// <summary>Total trials in the current RunTrials call.</summary>
        public int TotalTrialsInBlock => m_TotalTrialsInBlock;

        public PpsTaskAsset TaskAsset
        {
            get => m_TaskAsset;
            set => m_TaskAsset = value;
        }
        private void Awake()
        {
            m_Output = m_VibrotactileOutputBehaviour as IVibrotactileOutput;

            if (m_VibrotactileOutputBehaviour != null && m_Output == null)
                Debug.LogError($"[PpsTaskManager] {m_VibrotactileOutputBehaviour.name} does not implement IVibrotactileOutput.");

            if (m_Output != null)
                m_Output.PulseStarted += OnPulseStarted;

            // NEW: auto-wire input source from inspector reference
            if (m_InputSourceBehaviour != null)
            {
                var source = m_InputSourceBehaviour as IResponseInputSource;
                if (source != null)
                {
                    SetInputSource(source);
                    Debug.Log($"[PpsTaskManager] Input source wired: {m_InputSourceBehaviour.name}");
                }
                else
                {
                    Debug.LogError($"[PpsTaskManager] {m_InputSourceBehaviour.name} does not implement IResponseInputSource.");
                }
            }
            else
            {
                Debug.LogWarning("[PpsTaskManager] No input source assigned. Trial responses will not be captured.");
            }
        }

        private void OnDestroy()
        {
            // Always unsubscribe from events to avoid callbacks after this object is destroyed.
            if (m_Output != null)
                m_Output.PulseStarted -= OnPulseStarted;

            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;

            EndLogging();
        }

        /// <summary>
        /// Initializes runtime dependencies before trials are run.
        /// </summary>
        public void Initialize()
        {
            // Provide the looming controller with the spatial distance layout.         
            if (m_Loom != null && m_Layout != null)
                m_Loom.Layout = m_Layout;

            // Create a reproducible ITI random generator when a seed is provided.
            // The offset keeps this RNG stream separate from other task RNGs.
            m_ItiRng = m_TaskAsset != null && m_TaskAsset.RngSeed.HasValue
                ? new System.Random(m_TaskAsset.RngSeed.Value + 9973)
                : new System.Random();
        }

        /// <summary>
        /// Assigns the active response input source.
        /// This can be keyboard now, and later XR controller, hand tracking, etc.
        /// </summary>
        public void SetInputSource(IResponseInputSource source)
        {
            // Unsubscribe from the previous input source before replacing it.
            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;

            m_InputSource = source;

            // Subscribe to the new input source.
            if (m_InputSource != null)
                m_InputSource.ResponseReceived += OnResponseReceived;
        }

        private void PlayResponseRegisteredSound()
        {
            if (m_ResponseAudioSource == null || m_ResponseRegisteredClip == null)
                return;

            m_ResponseAudioSource.PlayOneShot(
                m_ResponseRegisteredClip,
                m_ResponseRegisteredVolume
            );
        }

        /// <summary>
        /// Starts CSV logging and enables participant input.
        /// </summary>
        public void BeginLogging(string subjectId)
        {
            if (m_TaskAsset == null)
            {
                Debug.LogError("[PpsTaskManager] Cannot begin logging. No PpsTaskAsset assigned.");
                return;
            }

            // Use a fallback subject ID if none was provided.
            if (string.IsNullOrWhiteSpace(subjectId))
                subjectId = "P000";

            // Create a persistent Logs folder.
            var dir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(dir);

            // Create a unique CSV file for this session.
            var sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            m_CsvPath = Path.Combine(dir, $"{subjectId}_{sessionId}_{m_TaskAsset.TaskName}.csv");

            m_CsvWriter = new StreamWriter(m_CsvPath, false, Encoding.UTF8);
            m_CsvWriter.WriteLine(PpsTrialResult.CsvHeader);
            m_CsvWriter.Flush();

            // Emit session-start marker and enable input capture.
            // FLORE TRIGGERS MODIFS
            //m_MarkerEmitter?.Emit("pps_session_start", extra: subjectId);
            m_MarkerEmitter?.Emit("pps_session_start");
            m_InputSource?.Enable();

            Debug.Log($"[PpsTaskManager] CSV: {m_CsvPath}");
        }

        /// <summary>
        /// Ends logging, disables input, and closes the CSV file.
        /// </summary>
        public void EndLogging()
        {
            m_MarkerEmitter?.Emit("pps_session_end");
            m_InputSource?.Disable();

            m_CsvWriter?.Dispose();
            m_CsvWriter = null;
        }

        // FLORE TRIGGER
        public void SetMarkerEmitter(EegMarkerEmitter emitter)
        {
            m_MarkerEmitter = emitter;
        }
        //

        /// <summary>
        /// Runs a sequence of trials with an inter-trial interval after each trial.
        ///
        /// In the future, PPSAppController should call this or RunOneTrial
        /// as part of the higher-level experiment flow.
        /// </summary>
        public IEnumerator RunTrials(PpsTrialDefinition[] trials)
            => RunTrials(trials, blockIndex: -1);

        /// <summary>
        /// Runs a sequence of trials and exposes per-block progress through
        /// <see cref="CurrentBlockIndex"/>, <see cref="TrialsCompletedInBlock"/>,
        /// and <see cref="TotalTrialsInBlock"/>. Use blockIndex = -1 for
        /// practice or non-block trial lists.
        /// </summary>
        public IEnumerator RunTrials(PpsTrialDefinition[] trials, int blockIndex)
        {
            if (m_TaskAsset == null)
            {
                Debug.LogError("[PpsTaskManager] Cannot run trials. No PpsTaskAsset assigned.");
                yield break;
            }

            if (m_Loom == null || m_Layout == null)
            {
                Debug.LogError("[PpsTaskManager] Cannot run trials. Missing Loom or Layout reference.");
                yield break;
            }

            Initialize();

            if (trials == null)
                yield break;

            // Reset block-progress counters so external clients (e.g. the
            // network server's /api/status endpoint) see fresh totals.
            m_CurrentBlockIndex      = blockIndex;
            m_TrialsCompletedInBlock = 0;
            m_TotalTrialsInBlock     = trials.Length;

            foreach (var trial in trials)
            {
                yield return RunOneTrial(trial);
                m_TrialsCompletedInBlock++;

                // Wait a randomized inter-trial interval before the next trial.
                float iti = NextItiSeconds();
                if (iti > 0f)
                    yield return new WaitForSeconds(iti);
            }
        }

        /// <summary>
        /// Returns a randomized inter-trial interval using the configured min/max values.
        /// </summary>
        private float NextItiSeconds()
        {
            float min = m_TaskAsset.ItiMinSeconds;
            float max = m_TaskAsset.ItiMaxSeconds;

            // If no valid range is configured, use the minimum value.
            if (max <= min)
                return min;

            double u = (m_ItiRng ?? new System.Random()).NextDouble();
            return (float)(min + u * (max - min));
        }

        /// <summary>
        /// Runs one PPS trial from start to finish.
        ///
        /// This method handles:
        /// - resetting trial state
        /// - starting visual looming when needed
        /// - firing vibration at the configured distance stage
        /// - collecting responses
        /// - computing reaction time
        /// - logging the result
        /// </summary>
        private IEnumerator RunOneTrial(PpsTrialDefinition trial)
        {
            // Create an empty result object and fill it during the trial.
            var result = PpsTrialResult.Empty(trial);
            result.vibrationDeviceName = m_Output != null ? m_Output.DeviceName : "None";

            m_CurrentTrialIsPractice = trial.isPractice;
            Debug.Log(
                $"[PPS TRIAL START] " +
                $"id={trial.trialId} | " +
                $"modality={trial.modality} | " +
                $"speed={trial.speed} | " +
                $"width={trial.width} | " +
                $"participantShoulderWidthMeters={m_ParticipantShoulderWidthMeters:F3} | " +
                $"computedSeparation={m_TaskAsset.SeparationFor(trial.width, m_ParticipantShoulderWidthMeters):F3} | " +
                $"vibrationStage={trial.vibrationStage} | " +
                $"requiresResponse={trial.RequiresResponse}"
            );
            TrialStarted?.Invoke(trial);


            // Reset all trial-specific response and timing state.
            m_CurrentTrialIsPractice = trial.isPractice;
            m_VibrationHasFired = false;
            m_CaptureResponses = true;
            m_VibrationFiredTime = double.NaN;
            m_FirstResponseTime = double.NaN;
            m_Responded = false;

            // Stores the time at which the looming stimulus reaches each distance stage.
            // Index corresponds to DistanceStage enum values.
            double[] crossings =
            {
                double.NaN,
                double.NaN,
                double.NaN,
                double.NaN,
                double.NaN
            };

            if (trial.modality == PpsModality.TactileOnly)
            {
                // In tactile-only trials, no visual stimulus is shown.
                // The vibration is fired at the same time it would have fired
                // if a looming stimulus had moved to the configured stage.
                float waitToFire = m_TaskAsset.TimeToReachStage(trial.speed, trial.vibrationStage);

                if (waitToFire > 0f)
                    yield return new WaitForSeconds(waitToFire);

                FireVibration(trial, trial.vibrationStage);

                // Keep the trial duration comparable to visual/visuotactile trials.
                float total = m_TaskAsset.DurationFor(trial.speed);
                float remaining = Mathf.Max(0f, total - waitToFire);

                if (remaining > 0f)
                    yield return new WaitForSeconds(remaining);
            }
            else
            {
                // Visual-only and visuotactile trials both run the looming stimulus.
                result.loomOnsetTime = Time.timeAsDouble;
                // FLORE TRIGGERS
                // m_MarkerEmitter?.Emit("pps_loom_onset", trial.trialId);
                // Emit trial-start marker for EEG/event synchronization.
                int triggerCode = TriggerEncoder.EncodeTask1(
                    ToTask1TrialType(trial.modality),
                    ToTactilePosition(trial.modality, trial.vibrationStage),
                    ToTask1Speed(trial.speed),
                    ToTask1Width(trial.width)
                );
                m_MarkerEmitter?.Emit("pps_loom_onset",  extra: triggerCode.ToString());

                bool vibFired = false;

                // Only Both trials should fire vibration during looming.
                bool fireOnStageMatch = trial.modality == PpsModality.Both;
                
                yield return m_Loom.RunLoom(trial, m_TaskAsset, m_ParticipantShoulderWidthMeters, stage =>
                {
                    double now = Time.timeAsDouble;
                    int idx = (int)stage;

                    // Record the first time this stage is entered.
                    if (idx >= 0 && idx < crossings.Length && double.IsNaN(crossings[idx]))
                        crossings[idx] = now;

                    Debug.Log(
                        $"[PPS STAGE] trial={trial.trialId} | " +
                        $"stage={stage} | " +
                        $"time={now:F3}"
                    );

                    // Emit stage-entry marker for EEG/event synchronization.
                    // FLORE TRIGGERS
                    // m_MarkerEmitter?.Emit("pps_stage_enter", trial.trialId, extra: stage.ToString());

                    // In visuotactile trials, fire vibration when the configured stage is reached.
                    if (fireOnStageMatch && !vibFired && stage == trial.vibrationStage)
                    {
                        vibFired = true;
                        FireVibration(trial, stage);
                    }
                });
            }

            // Store stage-crossing times in the result.
            result.crossingD4Time = crossings[(int)DistanceStage.D4];
            result.crossingD3Time = crossings[(int)DistanceStage.D3];
            result.crossingD2Time = crossings[(int)DistanceStage.D2];
            result.crossingD1Time = crossings[(int)DistanceStage.D1];

            // If the trial required a response but none occurred yet,
            // keep listening briefly during the response grace period.
            if (trial.RequiresResponse && !m_Responded)
            {
                float grace = m_TaskAsset.ResponseGracePeriodSeconds;
                float elapsed = 0f;

                while (elapsed < grace && !m_Responded)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!m_Responded)
                {
                    Debug.Log($"[PPS NO RESPONSE] trial={trial.trialId} | showing reminder");
                    m_CaptureResponses = true;
                    m_Feedback?.ShowNoResponseMessage();

                    while (!m_Responded)
                        yield return null;

                    Debug.Log($"[PPS NO RESPONSE] trial={trial.trialId} | response received, hiding reminder");
                    m_Feedback?.HideNoResponseMessage();
                }
            }

                        // Finalize response timing and reaction-time data.
            result.vibrationFiredTime = m_VibrationFiredTime;
            result.responseTime = m_FirstResponseTime;
            result.responded = m_Responded;

            result.reactionTimeMs =
                m_Responded && !double.IsNaN(m_VibrationFiredTime)
                    ? (float)((m_FirstResponseTime - m_VibrationFiredTime) * 1000.0)
                    : float.NaN;

            // Practice feedback — runs AFTER the no-response wait,
            // so a late response after the reminder still gets green/red.
            if (trial.isPractice && m_Feedback != null)
            {
                bool vibrationTrial = trial.RequiresResponse;

                bool respondedAfterVibration =
                    m_Responded &&
                    !double.IsNaN(m_VibrationFiredTime) &&
                    m_FirstResponseTime >= m_VibrationFiredTime;

                bool respondedBeforeVibration =
                    m_Responded &&
                    (
                        double.IsNaN(m_VibrationFiredTime) ||
                        m_FirstResponseTime < m_VibrationFiredTime
                    );

                bool hit  = vibrationTrial && respondedAfterVibration;
                bool miss = vibrationTrial && !m_Responded;  // will always be false here now
                bool falseAlarm = (!vibrationTrial && m_Responded) ||
                                (vibrationTrial && respondedBeforeVibration);

                if (hit)
                    m_Feedback.FlashGreen();
                else if (miss || falseAlarm)
                    m_Feedback.FlashRed();
            }
            // Finalize response timing and reaction-time data.
            result.vibrationFiredTime = m_VibrationFiredTime;
            result.responseTime = m_FirstResponseTime;
            result.responded = m_Responded;

            result.reactionTimeMs =
                m_Responded && !double.IsNaN(m_VibrationFiredTime)
                    ? (float)((m_FirstResponseTime - m_VibrationFiredTime) * 1000.0)
                    : float.NaN;
            // Practice correctness feedback.
            // Hit: vibration trial + response after vibration.
            // Miss: vibration trial + no response.
            // False alarm: no-vibration trial + response.
            // Correct rejection: no-vibration trial + no response.
            if (trial.isPractice && m_Feedback != null)
            {
                bool vibrationTrial = trial.RequiresResponse;

                bool respondedAfterVibration =
                    m_Responded &&
                    !double.IsNaN(m_VibrationFiredTime) &&
                    m_FirstResponseTime >= m_VibrationFiredTime;

                bool respondedBeforeVibration =
                    m_Responded &&
                    (
                        double.IsNaN(m_VibrationFiredTime) ||
                        m_FirstResponseTime < m_VibrationFiredTime
                    );

                bool hit =
                    vibrationTrial &&
                    respondedAfterVibration;

                bool miss =
                    vibrationTrial &&
                    !m_Responded;

                bool falseAlarm =
                    (!vibrationTrial && m_Responded) ||
                    (vibrationTrial && respondedBeforeVibration);

                if (hit)
                {
                    m_Feedback.FlashGreen();
                }
                else if (miss || falseAlarm)
                {
                    m_Feedback.FlashRed();
                }
            }





            Debug.Log(
                $"[PPS TRIAL END] " +
                $"id={trial.trialId} | " +
                $"modality={trial.modality} | " +
                $"speed={trial.speed} | " +
                $"width={trial.width} | " +
                $"vibrationStage={trial.vibrationStage} | " +
                $"responded={result.responded} | " +
                $"vibTime={FormatTime(result.vibrationFiredTime)} | " +
                $"responseTime={FormatTime(result.responseTime)} | " +
                $"RTms={FormatRt(result.reactionTimeMs)}"
            );

            // Emit trial-end marker.
            m_MarkerEmitter?.Emit("pps_trial_end", extra: m_Responded ? "62" : "63");

            // Stop accepting responses after the trial is finished.
            m_CaptureResponses = false;
            m_CurrentTrialIsPractice = false;
            m_VibrationHasFired = false;

            // Save and broadcast the completed result.
            WriteCsvRow(result);
            TrialCompleted?.Invoke(result);
        }

        /// <summary>
        /// Sends the vibration command to the vibrotactile output device.
        ///
        /// The actual confirmed onset time is recorded in OnPulseStarted,
        /// because the device may not start vibrating at the exact frame this method is called.
        /// </summary>
        private void FireVibration(PpsTrialDefinition trial, DistanceStage stage)
        {
            m_VibrationHasFired = true;

            Debug.Log("========== VIBRATION SENT ==========");

            // FUTURE ARDUINO SERIAL TRIGGER
            // serialPort.WriteLine("VIB_ON");

            m_Output?.Fire(
                m_TaskAsset.VibrationIntensity,
                m_TaskAsset.VibrationDurationMs
            );

            Debug.Log(
                $"[PPS VIBRATION] trial={trial.trialId} | modality={trial.modality} | stage={stage} | time={Time.timeAsDouble:F3}"
            );

            // FLORE TRIGGERS
            // m_MarkerEmitter?.Emit("pps_vib_fired", trial.trialId, extra: stage.ToString());
            m_MarkerEmitter?.Emit("pps_vib_fired");
        }

        /// <summary>
        /// Called by the vibration output device when the pulse actually starts.
        /// This timestamp is used as the reaction-time reference.
        /// </summary>
        private void OnPulseStarted()
        {
            m_VibrationFiredTime = Time.timeAsDouble;

            Debug.Log(
                $"[PPS VIBRATION STARTED] " +
                $"confirmedTime={m_VibrationFiredTime:F3}"
            );
        }

        /// <summary>
        /// Called when the participant responds.
        ///
        /// The task manager decides whether the response was:
        /// - a valid response to a vibration
        /// - an early response / false alarm
        ///
        /// Currently this may come from the keyboard,
        /// but the same interface can later support XR controller or hand input.
        /// </summary>
        private void OnResponseReceived(ResponseEvent ev)
        {
            // Sound fires on every single press — practice, main task, false alarms, all of it.
            m_Feedback?.OnResponseSubmitted();

            if (!m_CaptureResponses || m_Responded)
                return;

            m_Responded = true;
            m_FirstResponseTime = ev.timestamp;

            m_Feedback?.OnResponseSubmitted();

            if (m_VibrationHasFired)
                Debug.Log("[PPS RESPONSE] Felt vibration response accepted.");
            else
                Debug.Log("[PPS RESPONSE] Response before vibration / false alarm.");

            m_MarkerEmitter?.Emit("pps_response");
        }



                /// <summary>
        /// Writes one trial result to the CSV file.
        /// </summary>
        private void WriteCsvRow(PpsTrialResult result)
        {
            if (m_CsvWriter == null)
                return;

            m_CsvWriter.WriteLine(result.ToCsvRow());
            m_CsvWriter.Flush();
        }

        /// <summary>
        /// Formats a time value for readable debug output.
        /// </summary>
        private static string FormatTime(double value)
        {
            return double.IsNaN(value) ? "NA" : value.ToString("F3");
        }

        /// <summary>
        /// Formats a reaction time value for readable debug output.
        /// </summary>
        private static string FormatRt(float value)
        {
            return float.IsNaN(value) ? "NA" : value.ToString("F1");
        }
        
        private static TriggerEncoder.Task1TrialType ToTask1TrialType(PpsModality modality)
        {
            switch (modality)
            {
                case PpsModality.VisualOnly:
                    return TriggerEncoder.Task1TrialType.VisualOnly;

                case PpsModality.TactileOnly:
                    return TriggerEncoder.Task1TrialType.VibrotactileOnly;

                case PpsModality.Both:
                    return TriggerEncoder.Task1TrialType.VisualAndVibrotactile;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(modality),
                        modality,
                        "Unknown PPS modality."
                    );
            }
        }

        private static TriggerEncoder.TactilePosition ToTactilePosition(
            PpsModality modality,
            DistanceStage stage
        )
        {
            // Visual-only trials should not have a tactile position.
            if (modality == PpsModality.VisualOnly)
                return TriggerEncoder.TactilePosition.None;

            switch (stage)
            {
                case DistanceStage.D1:
                    return TriggerEncoder.TactilePosition.D1;

                case DistanceStage.D2:
                    return TriggerEncoder.TactilePosition.D2;

                case DistanceStage.D3:
                    return TriggerEncoder.TactilePosition.D3;

                case DistanceStage.D4:
                    return TriggerEncoder.TactilePosition.D4;

                default:
                    return TriggerEncoder.TactilePosition.None;
            }
        }

        private static TriggerEncoder.Task1Speed ToTask1Speed(PpsSpeed speed)
        {
            switch (speed)
            {
                case PpsSpeed.Slow:
                    return TriggerEncoder.Task1Speed.Slow;

                case PpsSpeed.Fast:
                    return TriggerEncoder.Task1Speed.Fast;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(speed),
                        speed,
                        "Unknown PPS speed."
                    );
            }
        }

        private static TriggerEncoder.Task1Width ToTask1Width(PpsWidth width)
        {
            switch (width)
            {
                case PpsWidth.Narrow:
                    return TriggerEncoder.Task1Width.Narrow;

                case PpsWidth.Wide:
                    return TriggerEncoder.Task1Width.Wide;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(width),
                        width,
                        "Unknown PPS width."
                    );
            }
        }
    }
}