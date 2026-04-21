using System;
using System.Collections;
using System.Collections.Generic;
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
    ///
    /// Changing any of these does not require editing this class.
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

        // Per-trial response capture (time-locked to the vibration event).
        bool m_CaptureResponses;
        double m_VibrationFiredTime;
        double m_FirstResponseTime;
        bool m_Responded;

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

                bool hasRest = b < m_TaskAsset.BlockCount - 1;
                if (hasRest)
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
                yield return new WaitForSeconds(m_TaskAsset.InterTrialIntervalSeconds);
            }
        }

        IEnumerator RunOneTrial(PpsTrialDefinition trial)
        {
            var result = PpsTrialResult.Empty(trial);
            result.vibrationDeviceName = m_Output != null ? m_Output.DeviceName : "None";

            TrialStarted?.Invoke(trial);
            m_MarkerEmitter?.Emit("pps_trial_start", trial.trialId, trial.modality.ToString(), extra: trial.vibrationStage.ToString());

            m_CaptureResponses = trial.RequiresResponse;
            m_VibrationFiredTime = double.NaN;
            m_FirstResponseTime = double.NaN;
            m_Responded = false;

            switch (trial.modality)
            {
                case PpsModality.VisualOnly:
                    yield return RunVisualOnly(trial, r => result = r);
                    break;
                case PpsModality.TactileOnly:
                    yield return RunTactileOnly(trial, r => result = r);
                    break;
                case PpsModality.Both:
                    yield return RunBoth(trial, r => result = r);
                    break;
            }

            // Grace window so late responses still count.
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

            result.loomOnsetTime = double.IsNaN(result.loomOnsetTime) ? Time.timeAsDouble : result.loomOnsetTime;
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

        IEnumerator RunVisualOnly(PpsTrialDefinition trial, Action<PpsTrialResult> update)
        {
            var r = PpsTrialResult.Empty(trial);
            r.vibrationDeviceName = m_Output?.DeviceName ?? "None";
            r.loomOnsetTime = Time.timeAsDouble;
            m_MarkerEmitter?.Emit("pps_loom_onset", trial.trialId);
            yield return m_Loom.RunLoom(trial, m_TaskAsset, stage =>
                m_MarkerEmitter?.Emit("pps_stage_enter", trial.trialId, extra: stage.ToString()));
            update(r);
        }

        IEnumerator RunTactileOnly(PpsTrialDefinition trial, Action<PpsTrialResult> update)
        {
            var r = PpsTrialResult.Empty(trial);
            r.vibrationDeviceName = m_Output?.DeviceName ?? "None";
            r.loomOnsetTime = Time.timeAsDouble;

            m_Output?.Fire(m_TaskAsset.VibrationIntensity, m_TaskAsset.VibrationDurationMs);
            m_MarkerEmitter?.Emit("pps_vib_fired", trial.trialId, extra: trial.vibrationStage.ToString());

            yield return new WaitForSeconds(m_TaskAsset.TactileOnlyDurationSeconds);
            update(r);
        }

        IEnumerator RunBoth(PpsTrialDefinition trial, Action<PpsTrialResult> update)
        {
            var r = PpsTrialResult.Empty(trial);
            r.vibrationDeviceName = m_Output?.DeviceName ?? "None";
            r.loomOnsetTime = Time.timeAsDouble;
            m_MarkerEmitter?.Emit("pps_loom_onset", trial.trialId);

            bool vibFired = false;
            yield return m_Loom.RunLoom(trial, m_TaskAsset, stage =>
            {
                m_MarkerEmitter?.Emit("pps_stage_enter", trial.trialId, extra: stage.ToString());
                if (!vibFired && stage == trial.vibrationStage)
                {
                    vibFired = true;
                    m_Output?.Fire(m_TaskAsset.VibrationIntensity, m_TaskAsset.VibrationDurationMs);
                    m_MarkerEmitter?.Emit("pps_vib_fired", trial.trialId, extra: stage.ToString());
                }
            });
            update(r);
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
        // Kept here (rather than reusing TaskLogger) because TaskLogger's schema is
        // hardcoded to TrialJudgement. PPS has a different row shape, and we want
        // each task to own its own CSV schema.

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
