using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Runtime scheduler for the PPS looming task.
    /// Drives the session phase state machine (SubjectId → Instructions → Practice
    /// → Block × N → Outro), runs each trial as a coroutine, times vibrotactile
    /// events to the loom's current <see cref="DistanceStage"/>, collects responses
    /// from an <see cref="IResponseInputSource"/>, and logs both CSV and EEG markers.
    ///
    /// Flexibility boundaries:
    ///   * Protocol shape   → <see cref="PpsTaskAsset"/> (ScriptableObject)
    ///   * Trial generation → <see cref="PpsTrialGenerator"/>
    ///   * Response input   → <see cref="IResponseInputSource"/> (reused from HitOrMiss)
    ///   * Haptic delivery  → <see cref="IVibrotactileOutput"/>
    ///   * UI               → <see cref="SessionFlowPanels"/>
    /// </summary>
    public class PpsTaskManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] PpsTaskAsset m_TaskAsset;

        [Header("Scene references")]
        [SerializeField] LoomingPairController m_Loom;
        [SerializeField] DistanceLayout m_Layout;
        [SerializeField] SessionFlowPanels m_Ui;

        [Header("Output (placeholder or hardware)")]
        [Tooltip("Drag any MonoBehaviour that implements IVibrotactileOutput (e.g. OnScreenVibrationOutput).")]
        [SerializeField] MonoBehaviour m_VibrotactileOutputBehaviour;

        [Header("Logging (reused from HitOrMiss)")]
        [SerializeField] EegMarkerEmitter m_MarkerEmitter;

        [Header("Runtime options")]
        [SerializeField] bool m_AutoStartOnAwake = false;

        IVibrotactileOutput m_Output;
        IResponseInputSource m_InputSource;

        bool m_CaptureResponses;
        double m_VibrationFiredTime;
        double m_FirstResponseTime;
        bool m_Responded;
        System.Random m_ItiRng;

        StreamWriter m_CsvWriter;
        string m_CsvPath;

        public PpsPhase CurrentPhase { get; private set; } = PpsPhase.Idle;
        public int CurrentBlock { get; private set; } = -1;

        public event Action<PpsPhase> PhaseChanged;
        public event Action<PpsTrialDefinition> TrialStarted;
        public event Action<PpsTrialResult> TrialCompleted;
        public event Action SessionFinished;

        public PpsTaskAsset TaskAsset
        {
            get => m_TaskAsset;
            set => m_TaskAsset = value;
        }

        public void SetInputSource(IResponseInputSource source)
        {
            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;

            m_InputSource = source;

            if (m_InputSource != null)
                m_InputSource.ResponseReceived += OnResponseReceived;
        }

        void Awake()
        {
            m_Output = m_VibrotactileOutputBehaviour as IVibrotactileOutput;
            if (m_VibrotactileOutputBehaviour != null && m_Output == null)
                Debug.LogError($"[PpsTaskManager] {m_VibrotactileOutputBehaviour.name} does not implement IVibrotactileOutput.");

            if (m_Output != null)
                m_Output.PulseStarted += OnPulseStarted;
        }

        void OnDestroy()
        {
            if (m_Output != null)
                m_Output.PulseStarted -= OnPulseStarted;
            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;

            m_CsvWriter?.Dispose();
            m_CsvWriter = null;
        }

        void Start()
        {
            if (m_AutoStartOnAwake) StartSession();
        }

        public void StartSession()
        {
            if (m_TaskAsset == null)
            {
                Debug.LogError("[PpsTaskManager] No PpsTaskAsset assigned.");
                return;
            }
            if (m_Loom == null || m_Layout == null || m_Ui == null)
            {
                Debug.LogError("[PpsTaskManager] Missing scene references (loom, layout, or UI).");
                return;
            }

            m_Loom.Layout = m_Layout;
            m_ItiRng = m_TaskAsset.RngSeed.HasValue
                ? new System.Random(m_TaskAsset.RngSeed.Value + 9973)
                : new System.Random();
            StartCoroutine(RunSession());
        }

        IEnumerator RunSession()
        {
            SetPhase(PpsPhase.SubjectId);
            yield return m_Ui.ShowSubjectIdAndWait();

            OpenCsvFor(m_Ui.SubjectId);
            m_MarkerEmitter?.Emit("pps_session_start", extra: m_Ui.SubjectId);
            m_InputSource?.Enable();

            SetPhase(PpsPhase.Instructions);
            yield return m_Ui.ShowInstructionsAndWait();

            SetPhase(PpsPhase.PracticeIntro);
            yield return m_Ui.ShowPracticeIntroAndWait();

            SetPhase(PpsPhase.Practice);
            yield return RunTrialList(PpsTrialGenerator.GeneratePractice(m_TaskAsset));

            for (int b = 0; b < m_TaskAsset.BlockCount; b++)
            {
                CurrentBlock = b;
                SetPhase(PpsPhase.BlockIntro);
                yield return m_Ui.ShowBlockIntroAndWait();

                SetPhase(PpsPhase.Block);
                yield return RunTrialList(m_TaskAsset.GenerateBlock(b));

                if (b < m_TaskAsset.BlockCount - 1)
                {
                    SetPhase(PpsPhase.Rest);
                    yield return m_Ui.ShowRestAndAutoAdvance(m_TaskAsset.RestDurationSeconds);
                }
            }

            SetPhase(PpsPhase.Outro);
            yield return m_Ui.ShowOutro();

            m_MarkerEmitter?.Emit("pps_session_end");
            m_InputSource?.Disable();
            CloseCsv();

            SetPhase(PpsPhase.Idle);
            SessionFinished?.Invoke();
        }

        IEnumerator RunTrialList(PpsTrialDefinition[] trials)
        {
            foreach (var trial in trials)
            {
                yield return RunOneTrial(trial);
                float iti = NextItiSeconds();
                if (iti > 0f) yield return new WaitForSeconds(iti);
            }
        }

        float NextItiSeconds()
        {
            float min = m_TaskAsset.ItiMinSeconds;
            float max = m_TaskAsset.ItiMaxSeconds;
            if (max <= min) return min;
            double u = (m_ItiRng ?? new System.Random()).NextDouble();
            return (float)(min + u * (max - min));
        }

        IEnumerator RunOneTrial(PpsTrialDefinition trial)
        {
            var result = PpsTrialResult.Empty(trial);
            result.vibrationDeviceName = m_Output != null ? m_Output.DeviceName : "None";

            TrialStarted?.Invoke(trial);
            m_MarkerEmitter?.Emit("pps_trial_start", trial.trialId,
                trial.modality.ToString(), extra: trial.vibrationStage.ToString());

            m_CaptureResponses = true;   // Capture on V too, to log false alarms.
            m_VibrationFiredTime = double.NaN;
            m_FirstResponseTime = double.NaN;
            m_Responded = false;

            // Box for per-stage crossing timestamps (closure-safe; structs can't be
            // mutated cleanly through an Action<DistanceStage> lambda).
            double[] crossings = { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN };

            if (trial.modality == PpsModality.TactileOnly)
            {
                // No visual — fire at the time-matched moment a matched VT trial
                // would have crossed the target stage, then hold the remainder so
                // the trial window matches VT duration.
                float waitToFire = m_TaskAsset.TimeToReachStage(trial.speed, trial.vibrationStage);
                if (waitToFire > 0f) yield return new WaitForSeconds(waitToFire);

                m_Output?.Fire(m_TaskAsset.VibrationIntensity, m_TaskAsset.VibrationDurationMs);
                m_MarkerEmitter?.Emit("pps_vib_fired", trial.trialId, extra: trial.vibrationStage.ToString());

                float total = m_TaskAsset.DurationFor(trial.speed);
                float remaining = Mathf.Max(0f, total - waitToFire);
                if (remaining > 0f) yield return new WaitForSeconds(remaining);
            }
            else
            {
                result.loomOnsetTime = Time.timeAsDouble;
                m_MarkerEmitter?.Emit("pps_loom_onset", trial.trialId);

                bool vibFired = false;
                bool fireOnStageMatch = trial.modality == PpsModality.Both;

                yield return m_Loom.RunLoom(trial, m_TaskAsset, stage =>
                {
                    double now = Time.timeAsDouble;
                    int idx = (int)stage;
                    if (idx >= 0 && idx < crossings.Length && double.IsNaN(crossings[idx]))
                        crossings[idx] = now;

                    m_MarkerEmitter?.Emit("pps_stage_enter", trial.trialId, extra: stage.ToString());

                    if (fireOnStageMatch && !vibFired && stage == trial.vibrationStage)
                    {
                        vibFired = true;
                        m_Output?.Fire(m_TaskAsset.VibrationIntensity, m_TaskAsset.VibrationDurationMs);
                        m_MarkerEmitter?.Emit("pps_vib_fired", trial.trialId, extra: stage.ToString());
                    }
                });
            }

            result.crossingD4Time = crossings[(int)DistanceStage.D4];
            result.crossingD3Time = crossings[(int)DistanceStage.D3];
            result.crossingD2Time = crossings[(int)DistanceStage.D2];
            result.crossingD1Time = crossings[(int)DistanceStage.D1];

            if (trial.RequiresResponse && !m_Responded)
            {
                float grace = m_TaskAsset.ResponseGracePeriodSeconds;
                float elapsed = 0f;
                while (elapsed < grace && !m_Responded)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            result.vibrationFiredTime = m_VibrationFiredTime;
            result.responseTime = m_FirstResponseTime;
            result.responded = m_Responded;
            result.reactionTimeMs = (m_Responded && !double.IsNaN(m_VibrationFiredTime))
                ? (float)((m_FirstResponseTime - m_VibrationFiredTime) * 1000.0)
                : float.NaN;

            m_MarkerEmitter?.Emit("pps_trial_end", trial.trialId,
                extra: m_Responded ? result.reactionTimeMs.ToString("F1") : "no_response");

            m_CaptureResponses = false;
            WriteCsvRow(result);
            TrialCompleted?.Invoke(result);
        }

        // ---- Callbacks ----

        void OnPulseStarted()
        {
            m_VibrationFiredTime = Time.timeAsDouble;
        }

        void OnResponseReceived(ResponseEvent ev)
        {
            if (!m_CaptureResponses || m_Responded) return;
            m_Responded = true;
            m_FirstResponseTime = ev.timestamp;
            m_MarkerEmitter?.Emit("pps_response", extra: ev.rawSource);
        }

        void SetPhase(PpsPhase phase)
        {
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        // ---- CSV logging ----
        // Owned here (not delegated to HitOrMiss TaskLogger) because TaskLogger's
        // schema is hardcoded to TrialJudgement. PPS has its own row shape that
        // matches the VR Development Brief.

        void OpenCsvFor(string subjectId)
        {
            if (string.IsNullOrEmpty(subjectId)) subjectId = "P000";
            var dir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(dir);
            var sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            m_CsvPath = Path.Combine(dir, $"{subjectId}_{sessionId}_{m_TaskAsset.TaskName}.csv");
            m_CsvWriter = new StreamWriter(m_CsvPath, false, Encoding.UTF8);
            m_CsvWriter.WriteLine(PpsTrialResult.CsvHeader);
            m_CsvWriter.Flush();
            Debug.Log($"[PpsTaskManager] CSV: {m_CsvPath}");
        }

        void WriteCsvRow(PpsTrialResult result)
        {
            if (m_CsvWriter == null) return;
            m_CsvWriter.WriteLine(result.ToCsvRow());
            m_CsvWriter.Flush();
        }

        void CloseCsv()
        {
            m_CsvWriter?.Dispose();
            m_CsvWriter = null;
        }
    }
}
