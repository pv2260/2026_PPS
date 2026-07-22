using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Runs PPS trials and emits trial-level events.
    ///
    /// TRIAL STRUCTURE (all trial types)
    ///
    ///   ITI (jittered 1.2-1.8 s, runs in RunTrials, outside the trial)
    ///   |
    ///   trial_start_ms ---- LEDs appear (VT/V) or silent wait begins (T)
    ///   |
    ///   warm-up lead ------ LEDs glide WarmupDistance -> D7 at the loom's own
    ///   |                   velocity. T trials wait the same interval blind.
    ///   |
    ///   d7_onset_ms ------- scoring window opens. Nothing perceptible happens.
    ///   |
    ///   scored loom ------- D7 -> D1. Vibration fires at the assigned stage.
    ///   |
    ///   RT window --------- opens at trial start, HARD-CLOSES at
    ///   |                   vibration + ResponseWindowSeconds. Presses after
    ///   |                   this are never logged as a reaction time.
    ///   |
    ///   advance gate ------ only if the trial was a miss (or GateEveryTrial).
    ///                       The press here advances the trial and is NOT an RT.
    ///
    /// There is no EEG padding. Fast and slow trials have different lengths;
    /// EEG epochs lock to the vibration and loom-onset markers, not to trial
    /// boundaries, and the jittered ITI breaks between-trial anticipation.
    ///
    /// Persistence is handled by <see cref="HitOrMiss.TaskLogger"/>, wired by
    /// PpsAppController via TrialCompleted. This class owns no files.
    /// </summary>
    public class PpsTaskManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PpsTaskAsset m_TaskAsset;

        [Header("Scene references")]
        [SerializeField] private LoomingPairController m_Loom;
        [SerializeField] private DistanceLayout m_Layout;

        [Header("ITI Crosshair")]
        [SerializeField] private Transform m_CrosshairRoot;
        [SerializeField] private Material m_CrosshairDefaultMaterial;
        [SerializeField] private Material m_CrosshairItiMaterial;

        private Renderer[] m_CrosshairRenderers;

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
        private bool m_CurrentTrialIsPractice;

        // True after the vibration command has been sent.
        private bool m_VibrationHasFired;

        [Header("Output")]
        [SerializeField] private MonoBehaviour m_VibrotactileOutputBehaviour;

        [Header("Logging")]
        [SerializeField] private EegMarkerEmitter m_MarkerEmitter;

        [Header("Response Input")]
        [Tooltip("Drag the GameObject with an IResponseInputSource component (e.g. XrTriggerResponseInputSource).")]
        [SerializeField] private MonoBehaviour m_InputSourceBehaviour;

        private IVibrotactileOutput m_Output;
        private IResponseInputSource m_InputSource;

        // ---- Response-capture state for the active trial ----
        //
        // m_CaptureResponses: the RT window. Opens at trial start (so anticipations
        //   and false alarms are still caught) and hard-closes in Update() either on
        //   the first response or at vibration + ResponseWindowSeconds.
        //
        // m_AwaitingAdvance / m_AdvanceRequested: the pacing gate. Opens only AFTER
        //   the RT window is closed. A press here advances the trial and is NEVER
        //   logged as a reaction time. This separation is the whole point: the old
        //   code had a single gate plus an unbounded `while (!m_Responded) yield`,
        //   so a "get me out of here" press 71 seconds after the vibration was
        //   recorded as a detection RT.
        private bool m_CaptureResponses;
        private bool m_AwaitingAdvance;
        private bool m_AdvanceRequested;
        private double m_VibrationFiredTime;
        private double m_FirstResponseTime;
        private bool m_Responded;

        private System.Random m_ItiRng;

        public event Action<PpsTrialDefinition> TrialStarted;
        public event Action<PpsTrialResult> TrialCompleted;

        // ---- Pause/Resume ----
        bool m_Paused;
        bool m_AbortCurrentRunRequested;

        public bool IsPaused => m_Paused;

        public event Action BlockPaused;
        public event Action BlockResumed;

        public void PauseBlock()
        {
            if (m_Paused) return;
            m_Paused = true;
            m_InputSource?.Disable();
            m_MarkerEmitter?.Emit("pps_block_paused");
            BlockPaused?.Invoke();
            Debug.Log("[PpsTaskManager] Paused.");
        }

        public void RequestAbortCurrentRun()
        {
            m_AbortCurrentRunRequested = true;
            Debug.Log("[PpsTaskManager] Abort current RunTrials requested.");
        }

        public void ResumeBlock()
        {
            if (!m_Paused) return;
            m_Paused = false;
            m_InputSource?.Enable();
            m_MarkerEmitter?.Emit("pps_block_resumed");
            BlockResumed?.Invoke();
            Debug.Log("[PpsTaskManager] Resumed.");
        }

        // ---- Block tracking ----
        int m_CurrentBlockIndex;
        int m_TrialsCompletedInBlock;
        int m_TotalTrialsInBlock;

        public int CurrentBlockIndex => m_CurrentBlockIndex;
        public int TrialsCompletedInBlock => m_TrialsCompletedInBlock;
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

            if (m_CrosshairRoot != null)
            {
                m_CrosshairRenderers = m_CrosshairRoot.GetComponentsInChildren<Renderer>(true);
                Debug.Log($"[PpsTaskManager] Found {m_CrosshairRenderers.Length} crosshair renderers.");
            }
            else
            {
                Debug.LogWarning("[PpsTaskManager] No crosshair root assigned.");
            }
        }

        /// <summary>
        /// Hard-closes the RT window. This MUST run per-frame rather than
        /// sequentially after RunLoom: on an early stage such as D7 the vibration
        /// fires roughly 1 s into a 3.4 s loom, so waiting for the loom to return
        /// would leave the window open for another 2.5 s of uninterpretable presses.
        /// That is what let a 4-second press become a "reaction time" in the pilot.
        /// </summary>
        private void Update()
        {
            if (!m_CaptureResponses || m_TaskAsset == null)
                return;

            // Close as soon as the first response lands.
            if (m_Responded)
            {
                m_CaptureResponses = false;
                return;
            }

            // Otherwise close ResponseWindowSeconds after the vibration.
            // Visual-only trials never fire, so their window is closed explicitly
            // by RunOneTrial at the end of the loom instead.
            if (!double.IsNaN(m_VibrationFiredTime)
                && Time.timeAsDouble >= m_VibrationFiredTime + m_TaskAsset.ResponseWindowSeconds)
            {
                m_CaptureResponses = false;
            }
        }

        private void OnDestroy()
        {
            if (m_Output != null)
                m_Output.PulseStarted -= OnPulseStarted;

            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;
        }

        public void Initialize()
        {
            if (m_Loom != null && m_Layout != null)
                m_Loom.Layout = m_Layout;

            m_ItiRng = m_TaskAsset != null && m_TaskAsset.RngSeed.HasValue
                ? new System.Random(m_TaskAsset.RngSeed.Value + 9973)
                : new System.Random();
        }

        public void SetInputSource(IResponseInputSource source)
        {
            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;

            m_InputSource = source;

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

        public void BeginSession()
        {
            m_MarkerEmitter?.Emit("pps_session_start");
            m_InputSource?.Enable();
        }

        public void EndSession()
        {
            m_MarkerEmitter?.Emit("pps_session_end");
            m_InputSource?.Disable();
        }

        public void SetMarkerEmitter(EegMarkerEmitter emitter)
        {
            m_MarkerEmitter = emitter;
        }

        private void SetCrosshairMaterial(Material material)
        {
            if (material == null)
                return;

            if (m_CrosshairRenderers == null || m_CrosshairRenderers.Length == 0)
                return;

            foreach (var r in m_CrosshairRenderers)
            {
                if (r != null)
                    r.material = material;
            }
        }

        public IEnumerator RunTrials(PpsTrialDefinition[] trials)
            => RunTrials(trials, blockIndex: -1);

        /// <summary>
        /// Assigned by the app controller: coroutine that shows the
        /// attention-check panel and completes when the participant responds.
        /// Invoked every <see cref="PpsTaskAsset.AttentionCheckEveryNTrials"/>
        /// completed MAIN-block trials — never during practice (blockIndex -1)
        /// and never after a block's final trial.
        /// </summary>
        public System.Func<IEnumerator> AttentionCheckRoutine;

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

            m_AbortCurrentRunRequested = false;

            if (trials == null)
                yield break;

            m_CurrentBlockIndex      = blockIndex;
            m_TrialsCompletedInBlock = 0;
            m_TotalTrialsInBlock     = trials.Length;

            foreach (var trial in trials)
            {
                // ------------------------------------------------------------
                // Jittered ITI before every trial.
                //
                // The jitter is what breaks between-trial temporal anticipation,
                // and it is why the EEG padding is no longer needed: with a
                // variable gap the participant cannot learn when the next loom
                // begins, regardless of whether the previous trial was fast or
                // slow. Configure ItiMin/ItiMax on the asset (1.2 to 1.8 s).
                // ------------------------------------------------------------

                float iti = NextItiSeconds();
                float elapsedIti = 0f;

                SetCrosshairMaterial(m_CrosshairItiMaterial);

                while (elapsedIti < iti)
                {
                    if (m_AbortCurrentRunRequested)
                    {
                        SetCrosshairMaterial(m_CrosshairDefaultMaterial);
                        yield break;
                    }

                    while (m_Paused)
                    {
                        if (m_AbortCurrentRunRequested)
                        {
                            SetCrosshairMaterial(m_CrosshairDefaultMaterial);
                            yield break;
                        }

                        yield return null;
                    }

                    elapsedIti += Time.deltaTime;
                    yield return null;
                }

                SetCrosshairMaterial(m_CrosshairDefaultMaterial);

                // Trial starts only AFTER the ITI has completed.
                while (m_Paused)
                {
                    if (m_AbortCurrentRunRequested)
                        yield break;

                    yield return null;
                }

                if (m_AbortCurrentRunRequested)
                    yield break;

                yield return RunOneTrial(trial);

                if (m_AbortCurrentRunRequested)
                    yield break;

                m_TrialsCompletedInBlock++;

                // ------------------------------------------------------------
                // Periodic attention check. Main blocks only (practice passes
                // blockIndex -1); skipped after the block's final trial. The
                // next iteration's jittered ITI follows the check, so the gap
                // before the next loom stays protocol-normal. Markers bracket
                // the panel so response latency is recoverable from the EEG
                // marker stream.
                // ------------------------------------------------------------
                int checkEvery = m_TaskAsset != null ? m_TaskAsset.AttentionCheckEveryNTrials : 0;
                if (checkEvery > 0
                    && blockIndex >= 0
                    && AttentionCheckRoutine != null
                    && m_TrialsCompletedInBlock % checkEvery == 0
                    && m_TrialsCompletedInBlock < m_TotalTrialsInBlock)
                {
                    m_MarkerEmitter?.Emit("pps_attention_check_shown");
                    yield return AttentionCheckRoutine();
                    m_MarkerEmitter?.Emit("pps_attention_check_done");

                    if (m_AbortCurrentRunRequested)
                        yield break;
                }
            }
        }

        /// <summary>
        /// Draws the inter-trial interval uniformly from [ItiMin, ItiMax].
        /// </summary>
        private float NextItiSeconds()
        {
            float min = m_TaskAsset.ItiMinSeconds;
            float max = m_TaskAsset.ItiMaxSeconds;

            if (max <= min)
                return min;

            double u = (m_ItiRng ?? new System.Random()).NextDouble();
            return (float)(min + u * (max - min));
        }

        /// <summary>
        /// Runs one PPS trial from start to finish.
        /// </summary>
        private IEnumerator RunOneTrial(PpsTrialDefinition trial)
        {
            var result = PpsTrialResult.Empty(trial);

            result.trialStartTime = Time.timeAsDouble;
            result.vibrationDeviceName = m_Output != null ? m_Output.DeviceName : "None";
            result.loomStartMeters = m_TaskAsset.LoomStartDistance;
            result.loomEndMeters   = m_TaskAsset.LoomEndDistance;
            result.vibrationDistanceMeters = trial.vibrationStage == DistanceStage.None
                ? float.NaN
                : m_TaskAsset.DistanceForStage(trial.vibrationStage);

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
            m_VibrationHasFired  = false;
            m_CaptureResponses   = true;
            m_AwaitingAdvance    = false;
            m_AdvanceRequested   = false;
            m_VibrationFiredTime = double.NaN;
            m_FirstResponseTime  = double.NaN;
            m_Responded          = false;

            // The warm-up glides at the loom's own velocity, so its length depends
            // on the trial's speed. Tactile-only trials must wait the same interval
            // blind, or their vibration lands earlier on the temporal hazard curve
            // than the matched visuotactile vibration.
            float warmupLead = m_TaskAsset.WarmupLeadSeconds(trial.speed);

            int stageCount = Enum.GetValues(typeof(DistanceStage)).Length;
            double[] crossings = new double[stageCount];

            for (int i = 0; i < crossings.Length; i++)
                crossings[i] = double.NaN;

            int triggerCode = TriggerEncoder.EncodeTask1(
                trial.vibrationStage.ToString(),
                trial.modality switch
                {
                    PpsModality.VisualOnly  => TriggerEncoder.Task1TrialType.VisualOnly,
                    PpsModality.TactileOnly => TriggerEncoder.Task1TrialType.VibrotactileOnly,
                    PpsModality.Both        => TriggerEncoder.Task1TrialType.Both,
                    _                       => TriggerEncoder.Task1TrialType.VisualOnly
                },
                trial.width switch
                {
                    PpsWidth.Narrow => TriggerEncoder.Task1Width.Narrow,
                    PpsWidth.Wide   => TriggerEncoder.Task1Width.Wide,
                    _               => TriggerEncoder.Task1Width.Narrow
                },
                trial.speed switch
                {
                    PpsSpeed.Slow => TriggerEncoder.Task1Speed.Slow,
                    PpsSpeed.Fast => TriggerEncoder.Task1Speed.Fast,
                    _             => TriggerEncoder.Task1Speed.Slow
                }
            );

            m_MarkerEmitter?.Emit(
                "pps_trial_start",
                trial.trialId,
                trial.modality.ToString(),
                extra: triggerCode.ToString()
            );

            if (trial.modality == PpsModality.TactileOnly)
            {
                // -----------------------------------------------------------------
                // Tactile-only: a silent clone of the visuotactile timeline.
                //
                // Nothing is drawn, but the vibration must land at exactly the same
                // elapsed time as it would on the matched VT trial. Two bugs without
                // the warmupLead term:
                //   1. T fires warmupLead earlier than VT at every stage, so the
                //      baseline is sampled at the wrong point on the hazard curve.
                //   2. At D7 (progress 0/6) TimeToReachStage returns exactly 0, so
                //      WaitForSeconds(0) yields immediately and the vibration lands
                //      on the same frame as trial start, before the participant is
                //      oriented. Pilot subject franc: 12/12 T-at-D7 trials unusable,
                //      median RT 4636 ms.
                // -----------------------------------------------------------------

                float waitToFire =
                    warmupLead + m_TaskAsset.TimeToReachStage(trial.speed, trial.vibrationStage);

                if (waitToFire > 0f)
                    yield return new WaitForSeconds(waitToFire);

                FireVibration(trial, trial.vibrationStage);

                // Run out the remainder of the matched loom window.
                float total = warmupLead + m_TaskAsset.DurationFor(trial.speed);
                float remaining = Mathf.Max(0f, total - waitToFire);

                if (remaining > 0f)
                    yield return new WaitForSeconds(remaining);
            }
            else
            {
                // Visual-only and visuotactile trials both run the looming stimulus.
                bool vibFired = false;
                bool fireOnStageMatch = trial.modality == PpsModality.Both;

                yield return m_Loom.RunLoom(trial, m_TaskAsset, m_ParticipantShoulderWidthMeters, stage =>
                {
                    double now = Time.timeAsDouble;
                    int idx = (int)stage;

                    if (idx >= 0 && idx < crossings.Length && double.IsNaN(crossings[idx]))
                        crossings[idx] = now;

                    Debug.Log(
                        $"[PPS STAGE] trial={trial.trialId} | " +
                        $"stage={stage} | " +
                        $"time={now:F3}"
                    );

                    if (fireOnStageMatch && !vibFired && stage == trial.vibrationStage)
                    {
                        vibFired = true;
                        FireVibration(trial, stage);
                    }
                });

                // loomOnsetTime is stamped inside RunLoom on the frame the LEDs
                // actually became visible. It used to be stamped here in the manager
                // before RunLoom was even called, which is why it came out
                // byte-identical to trial_start_ms in the pilot.
                result.loomOnsetTime = m_Loom.LoomOnsetTime;

                // The D7 crossing is where the scored window opened. It sits one
                // warm-up lead after loomOnsetTime. Nothing perceptible happens to
                // the participant at D7; it is an analysis bookmark.
                int d7Index = (int)DistanceStage.D7;
                result.d7OnsetTime = (d7Index >= 0 && d7Index < crossings.Length)
                    ? crossings[d7Index]
                    : double.NaN;
            }

            // -----------------------------------------------------------------
            // Close the response window.
            //
            // Update() closes it on the first response, or ResponseWindowSeconds
            // after the vibration, whichever comes first. For late stages such as
            // D1 the vibration fires on the loom's final frame, so the loom ends
            // BEFORE the window does; hold the trial open until it closes so a D1
            // trial gets the same window as a D7 trial.
            //
            // A fast responder exits immediately, so this does not slow the pace.
            // Visual-only trials never fire, so the loop is skipped and the window
            // shuts at the end of the loom: any press during a V trial is a false
            // alarm, which is exactly what it should be.
            // -----------------------------------------------------------------
            while (m_CaptureResponses && !double.IsNaN(m_VibrationFiredTime))
                yield return null;

            m_CaptureResponses = false;

            // Store stage-crossing times in the result.
            result.crossingD7Time = crossings[(int)DistanceStage.D7];
            result.crossingD6Time = crossings[(int)DistanceStage.D6];
            result.crossingD5Time = crossings[(int)DistanceStage.D5];
            result.crossingD4Time = crossings[(int)DistanceStage.D4];
            result.crossingD3Time = crossings[(int)DistanceStage.D3];
            result.crossingD2Time = crossings[(int)DistanceStage.D2];
            result.crossingD1Time = crossings[(int)DistanceStage.D1];

            // Finalize response timing. A miss stays a miss: responded = false,
            // reactionTimeMs = NaN. Nothing that happens in the advance gate below
            // can change these.
            result.vibrationFiredTime = m_VibrationFiredTime;
            result.responseTime       = m_FirstResponseTime;
            result.responded          = m_Responded;

            result.reactionTimeMs =
                m_Responded && !double.IsNaN(m_VibrationFiredTime)
                    ? (float)((m_FirstResponseTime - m_VibrationFiredTime) * 1000.0)
                    : float.NaN;

            // -----------------------------------------------------------------
            // Advance gate.
            //
            // If the participant missed the vibration, hand the pace back to them:
            // show the reminder and wait for a press. This stops trials piling up
            // on someone who has lost the thread, which matters for patients.
            //
            // Critically, the RT window is ALREADY hard-closed above, and the press
            // routes through m_AwaitingAdvance rather than m_Responded, so it can
            // never be logged as a reaction time. The old code re-opened
            // m_CaptureResponses here and then waited unboundedly, which is how a
            // 71-second press became a "detection RT".
            //
            // The gate is bounded by AdvanceGateTimeoutSeconds so it can never hang.
            // -----------------------------------------------------------------
            bool missed = trial.RequiresResponse && !m_Responded;

            if (missed || m_TaskAsset.GateEveryTrial)
            {
                if (missed)
                {
                    Debug.Log(
                        $"[PPS MISS] trial={trial.trialId} | no response within " +
                        $"{m_TaskAsset.ResponseWindowSeconds:F2}s of the vibration"
                    );
                    m_Feedback?.ShowNoResponseMessage();
                }

                // Refractory gap: a late detection press landing here must not be
                // silently consumed as the advance press.
                if (m_TaskAsset.AdvanceGateDelaySeconds > 0f)
                    yield return new WaitForSeconds(m_TaskAsset.AdvanceGateDelaySeconds);

                m_AdvanceRequested = false;
                m_AwaitingAdvance  = true;

                float gateElapsed = 0f;
                float gateTimeout = m_TaskAsset.AdvanceGateTimeoutSeconds;

                while (!m_AdvanceRequested && gateElapsed < gateTimeout)
                {
                    if (m_AbortCurrentRunRequested)
                        break;

                    gateElapsed += Time.deltaTime;
                    yield return null;
                }

                m_AwaitingAdvance = false;

                if (missed)
                    m_Feedback?.HideNoResponseMessage();

                if (!m_AdvanceRequested)
                {
                    Debug.LogWarning(
                        $"[PPS ADVANCE TIMEOUT] trial={trial.trialId} | " +
                        $"advancing after {gateTimeout:F0}s with no press"
                    );
                }
            }

            // Practice correctness feedback.
            //   Hit               = vibration trial + response after the vibration
            //   Miss              = vibration trial + no response in the window
            //   False alarm       = no-vibration trial + response, OR a response
            //                       that landed before the vibration (anticipation)
            //   Correct rejection = no-vibration trial + no response
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

                bool hit        = vibrationTrial && respondedAfterVibration;
                bool miss       = vibrationTrial && !m_Responded;
                bool falseAlarm = (!vibrationTrial && m_Responded) ||
                                  (vibrationTrial && respondedBeforeVibration);

                Debug.Log(
                    $"[PPS PRACTICE FEEDBACK] trial={trial.trialId} | " +
                    $"hit={hit} | miss={miss} | falseAlarm={falseAlarm}"
                );

                // TODO: wire these to the corresponding PpsFeedback calls.
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

            m_CaptureResponses       = false;
            m_AwaitingAdvance        = false;
            m_AdvanceRequested       = false;
            m_CurrentTrialIsPractice = false;
            m_VibrationHasFired      = false;

            TrialCompleted?.Invoke(result);
        }

        /// <summary>
        /// Sends the vibration command to the vibrotactile output device.
        ///
        /// The confirmed onset time is overwritten in OnPulseStarted, because the
        /// device may not start vibrating on the exact frame this method is called.
        /// </summary>
        private void FireVibration(PpsTrialDefinition trial, DistanceStage stage)
        {
            m_VibrationHasFired = true;

            if (double.IsNaN(m_VibrationFiredTime))
                m_VibrationFiredTime = Time.timeAsDouble;

            m_Output?.Fire(
                m_TaskAsset.VibrationIntensity,
                m_TaskAsset.VibrationDurationMs
            );

            Debug.Log(
                $"[PPS VIBRATION] trial={trial.trialId} | modality={trial.modality} | " +
                $"stage={stage} | time={Time.timeAsDouble:F3}"
            );

            m_MarkerEmitter?.Emit("pps_vib_fired", trial.trialId, extra: stage.ToString());
        }

        /// <summary>
        /// Called by the vibration output device when the pulse actually starts.
        /// This timestamp is the reaction-time reference.
        /// </summary>
        private void OnPulseStarted()
        {
            m_VibrationFiredTime = Time.timeAsDouble;

            Debug.Log($"[PPS VIBRATION STARTED] confirmedTime={m_VibrationFiredTime:F3}");
        }

        /// <summary>
        /// Called when the participant presses.
        ///
        /// Routing, in order:
        ///   1. Advance gate open  -> this is a PACING press. Advance the trial.
        ///                            Never logged as a reaction time.
        ///   2. RT window open     -> this is a DETECTION press. Log it.
        ///   3. Otherwise          -> discard silently. This is the dead zone
        ///                            between the window closing and the gate
        ///                            opening, and presses here mean nothing.
        /// </summary>
        private void OnResponseReceived(ResponseEvent ev)
        {
            if (m_AwaitingAdvance)
            {
                m_AdvanceRequested = true;
                m_Feedback?.OnResponseSubmitted();
                return;
            }

            if (!m_CaptureResponses || m_Responded)
                return;

            m_Responded = true;
            m_FirstResponseTime = ev.timestamp;

            // Confirmation feedback fires only for presses that are actually
            // recorded. The old code fired it on every press, including discarded
            // ones, which teaches the participant that a late press "counted".
            m_Feedback?.OnResponseSubmitted();

            if (m_VibrationHasFired)
                Debug.Log("[PPS RESPONSE] Vibration response accepted.");
            else
                Debug.Log("[PPS RESPONSE] Response before vibration / false alarm.");

            m_MarkerEmitter?.Emit("pps_response");
        }

        private static string FormatTime(double value)
        {
            return double.IsNaN(value) ? "NA" : value.ToString("F3");
        }

        private static string FormatRt(float value)
        {
            return float.IsNaN(value) ? "NA" : value.ToString("F1");
        }
    }
}