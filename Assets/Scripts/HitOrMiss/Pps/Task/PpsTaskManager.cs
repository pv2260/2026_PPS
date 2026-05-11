using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace HitOrMiss.Pps
{
    public class PpsTaskManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PpsTaskAsset m_TaskAsset;

        [Header("Scene references")]
        [SerializeField] private LoomingPairController m_Loom;
        [SerializeField] private DistanceLayout m_Layout;

        [Header("Practice Feedback")]
        [SerializeField] private SessionFlowPanels m_Ui;


        private bool m_CurrentTrialIsPractice;
        private bool m_VibrationHasFired;

        [Header("Output")]
        [SerializeField] private MonoBehaviour m_VibrotactileOutputBehaviour;

        [Header("Logging")]
        [SerializeField] private EegMarkerEmitter m_MarkerEmitter;

        private IVibrotactileOutput m_Output;
        private IResponseInputSource m_InputSource;
        private bool m_CaptureResponses;
        private double m_VibrationFiredTime;
        private double m_FirstResponseTime;
        private bool m_Responded;

        private System.Random m_ItiRng;

        private StreamWriter m_CsvWriter;
        private string m_CsvPath;

        public event Action<PpsTrialDefinition> TrialStarted;
        public event Action<PpsTrialResult> TrialCompleted;

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
        }

        private void OnDestroy()
        {
            if (m_Output != null)
                m_Output.PulseStarted -= OnPulseStarted;

            if (m_InputSource != null)
                m_InputSource.ResponseReceived -= OnResponseReceived;

            EndLogging();
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

        public void BeginLogging(string subjectId)
        {
            if (m_TaskAsset == null)
            {
                Debug.LogError("[PpsTaskManager] Cannot begin logging. No PpsTaskAsset assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(subjectId))
                subjectId = "P000";

            var dir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(dir);

            var sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            m_CsvPath = Path.Combine(dir, $"{subjectId}_{sessionId}_{m_TaskAsset.TaskName}.csv");

            m_CsvWriter = new StreamWriter(m_CsvPath, false, Encoding.UTF8);
            m_CsvWriter.WriteLine(PpsTrialResult.CsvHeader);
            m_CsvWriter.Flush();

            m_MarkerEmitter?.Emit("pps_session_start", extra: subjectId);
            m_InputSource?.Enable();

            Debug.Log($"[PpsTaskManager] CSV: {m_CsvPath}");
        }

        public void EndLogging()
        {
            m_MarkerEmitter?.Emit("pps_session_end");
            m_InputSource?.Disable();

            m_CsvWriter?.Dispose();
            m_CsvWriter = null;
        }

        public IEnumerator RunTrials(PpsTrialDefinition[] trials)
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

            foreach (var trial in trials)
            {
                yield return RunOneTrial(trial);

                float iti = NextItiSeconds();
                if (iti > 0f)
                    yield return new WaitForSeconds(iti);
            }
        }

        private float NextItiSeconds()
        {
            float min = m_TaskAsset.ItiMinSeconds;
            float max = m_TaskAsset.ItiMaxSeconds;

            if (max <= min)
                return min;

            double u = (m_ItiRng ?? new System.Random()).NextDouble();
            return (float)(min + u * (max - min));
        }

        private IEnumerator RunOneTrial(PpsTrialDefinition trial)
        {
            var result = PpsTrialResult.Empty(trial);
            result.vibrationDeviceName = m_Output != null ? m_Output.DeviceName : "None";
            m_CurrentTrialIsPractice = trial.isPractice;

            Debug.Log(
                $"[PPS TRIAL START] " +
                $"id={trial.trialId} | " +
                $"modality={trial.modality} | " +
                $"speed={trial.speed} | " +
                $"width={trial.width} | " +
                $"vibrationStage={trial.vibrationStage} | " +
                $"requiresResponse={trial.RequiresResponse}"
            );

            TrialStarted?.Invoke(trial);

            m_MarkerEmitter?.Emit(
                "pps_trial_start",
                trial.trialId,
                trial.modality.ToString(),
                extra: trial.vibrationStage.ToString()
            );
            m_CurrentTrialIsPractice = trial.isPractice;
            m_VibrationHasFired = false;
            m_CaptureResponses = true;
            m_VibrationFiredTime = double.NaN;
            m_FirstResponseTime = double.NaN;
            m_Responded = false;

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
                float waitToFire = m_TaskAsset.TimeToReachStage(trial.speed, trial.vibrationStage);

                if (waitToFire > 0f)
                    yield return new WaitForSeconds(waitToFire);

                FireVibration(trial, trial.vibrationStage);

                float total = m_TaskAsset.DurationFor(trial.speed);
                float remaining = Mathf.Max(0f, total - waitToFire);

                if (remaining > 0f)
                    yield return new WaitForSeconds(remaining);
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

                    Debug.Log(
                        $"[PPS STAGE] trial={trial.trialId} | " +
                        $"stage={stage} | " +
                        $"time={now:F3}"
                    );

                    m_MarkerEmitter?.Emit("pps_stage_enter", trial.trialId, extra: stage.ToString());

                    if (fireOnStageMatch && !vibFired && stage == trial.vibrationStage)
                    {
                        vibFired = true;
                        FireVibration(trial, stage);
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
            result.reactionTimeMs =
                m_Responded && !double.IsNaN(m_VibrationFiredTime)
                    ? (float)((m_FirstResponseTime - m_VibrationFiredTime) * 1000.0)
                    : float.NaN;

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

            m_MarkerEmitter?.Emit(
                "pps_trial_end",
                trial.trialId,
                extra: m_Responded ? result.reactionTimeMs.ToString("F1") : "no_response"
            );

            m_CaptureResponses = false;
            m_CurrentTrialIsPractice = false;
            m_VibrationHasFired = false;
            WriteCsvRow(result);
            TrialCompleted?.Invoke(result);
        }

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

            m_MarkerEmitter?.Emit("pps_vib_fired", trial.trialId, extra: stage.ToString());
        }
        private void OnPulseStarted()
        {
            m_VibrationFiredTime = Time.timeAsDouble;

            Debug.Log(
                $"[PPS VIBRATION STARTED] " +
                $"confirmedTime={m_VibrationFiredTime:F3}"
            );
        }

// Once spacebar has been pressed, the taskmanager can decide whether it was a reponse to a vibration event or a false alarm
        private void OnResponseReceived(ResponseEvent ev)
        {
            if (!m_CaptureResponses || m_Responded)
                return;

            m_Responded = true;
            m_FirstResponseTime = ev.timestamp;

            if (m_VibrationHasFired)
            {
                Debug.Log("[PPS RESPONSE] Felt vibration response accepted.");

                if (m_CurrentTrialIsPractice && m_Ui != null)
                    StartCoroutine(m_Ui.ShowPracticeFeedback("Felt it"));
            }
            else
            {
                Debug.Log("[PPS RESPONSE] Response before vibration / false alarm.");

                if (m_CurrentTrialIsPractice && m_Ui != null)
                    StartCoroutine(m_Ui.ShowPracticeFeedback("Too early"));
            }

            m_MarkerEmitter?.Emit("pps_response", extra: ev.rawSource);
        }

        private void WriteCsvRow(PpsTrialResult result)
        {
            if (m_CsvWriter == null)
                return;

            m_CsvWriter.WriteLine(result.ToCsvRow());
            m_CsvWriter.Flush();
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