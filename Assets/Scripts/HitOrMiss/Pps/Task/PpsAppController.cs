using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Top-level orchestrator for the PPS looming task (Task 1).
    ///
    /// ONE SOURCE FOR EVERYTHING, funneled through a PpsSessionConfig asset:
    ///   - protocol  -> PpsSessionConfig.TaskAsset (shared, reusable). Runs
    ///                  directly, never overridden.
    ///   - identity  -> PpsSessionConfig (panel off) or the clinical panel
    ///                  (panel on). Pushed INTO the EegMarkerEmitter via
    ///                  SetIdentity; the emitter no longer stores id/session.
    ///   - subject   -> PpsSessionConfig.SubjectInfo (panel off) or the panel.
    /// PpsSessionConfig.UseClinicalPanel is the switch. SessionMetadata is a
    /// write-once record; nothing reads it back to drive the run.
    /// </summary>
    public class PpsAppController : MonoBehaviour
    {
        [Header("Session")]
        [Tooltip("The single per-session entry point. References the PPS protocol asset and holds " +
                 "identity, subject data, and the use-panel switch. Create via " +
                 "Assets > Create > Parkinson > HitOrMiss > PPS Session Config.")]
        [SerializeField] private PpsSessionConfigAsset m_SessionConfig;

        [Tooltip("When the PpsSessionConfig has the clinical panel OFF, start the session " +
                 "automatically on Play. Turn this off to start from a button (call StartSession()).")]
        [SerializeField] private bool m_AutoStartWhenPanelOff = true;

        [Header("Input")]
        [SerializeField] private KeyboardCommandInput m_KeyboardInput;
        [SerializeField] private MonoBehaviour m_ControllerInputBehaviour;

        private IResponseInputSource m_ControllerInput;

        [SerializeField] private SessionFlowPanels m_Ui;
        [SerializeField] private PpsTaskManager m_TaskManager;

        [Header("Logging")]
        [SerializeField] TaskLogger m_TaskLogger;
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;

        // Cached delegate so we can unsubscribe with the same reference.
        System.Action<PpsTrialResult> m_LoggerTrialHandler;

        [Header("Visuals")]
        [SerializeField] FixationCrossController m_FixationCross;
        [SerializeField] StandingCross m_StandingCross;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        SessionMetadata m_SessionMetadata;
        bool m_SessionMetadataSet;

        // Protocol comes from the SessionConfig. During a session the task
        // runs a runtime CLONE of it, so clinician-panel overrides can adjust
        // this session without ever mutating the on-disk asset.
        PpsTaskAsset m_RuntimeProtocol;
        PpsTaskAsset Protocol => m_SessionConfig != null ? m_SessionConfig.TaskAsset : null;
        PpsTaskAsset Asset => m_RuntimeProtocol != null ? m_RuntimeProtocol : Protocol;

        Coroutine m_SessionCoroutine;
        private bool m_Running;
        private bool m_StopRequested;

        public bool IsRunning => m_Running;

        public bool IsPaused => m_TaskManager != null && m_TaskManager.IsPaused;


        public event System.Action SessionStarted;
        public event System.Action SessionEnded;
        public event System.Action SessionPaused;
        public event System.Action SessionResumed;
        /// <summary>Fires the moment the TaskLogger actually opens the
        /// per-session folder (i.e. real CSV recording begins). Useful so the
        /// clinician UI can flip a "REC" indicator only when data is being
        /// written, not during the practice phase.</summary>
        public event System.Action RecordingStarted;

        /// <summary>True while the TaskLogger is open and the main blocks are
        /// being recorded. False during practice and intro panels.</summary>
        public bool IsRecording => m_TaskLogger != null && m_TaskLogger.IsSessionOpen;
        private bool m_RestartCurrentBlockRequested;

        public string ParticipantId => m_SessionMetadata.participantId;

        /// <summary>
        /// Asks the running task to wind down at the next checkpoint.
        /// The task coroutine polls StopWasRequested() between steps.
        /// </summary>
        public void RequestStop()
        {
            m_StopRequested = true;

            // Abort the block that is currently running so the coroutine
            // reaches its next StopWasRequested() checkpoint immediately,
            // instead of only after all remaining trials in the block.
            // Mirrors StopTaskFromParticipantPause, which already did this.
            if (m_TaskManager != null)
                m_TaskManager.RequestAbortCurrentRun();

            // If the session is paused, the coroutine is blocked inside
            // RunTrials and would never poll the stop flag. Resume so it can
            // wind down. No-op when not paused.
            ResumeSession();

            Debug.Log("[PPSAppController] RequestStop() — aborting current run; task will end at the next checkpoint.");
        }

        /// <summary>
        /// Applies session metadata before StartSession (or via StartSession overload).
        /// Shoulder width is propagated to the task manager so the LED separation
        /// reflects the participant on record.
        /// </summary>
        public void SetSessionMetadata(SessionMetadata metadata)
        {
            m_SessionMetadata = metadata;
            m_SessionMetadataSet = true;

            if (m_TaskManager != null && metadata.shoulderWidthCm > 0f)
                m_TaskManager.SetParticipantShoulderWidthCm(metadata.shoulderWidthCm);
        }

        public void StartSession(SessionMetadata metadata)
        {
            SetSessionMetadata(metadata);
            StartSession();
        }

        public void StartSession()
        {
            if (m_Running)
            {
                Debug.LogWarning("[PPSAppController] StartSession ignored — session already running.");
                return;
            }
            if (m_SessionCoroutine != null)
            {
                Debug.LogWarning("[PPSAppController] StartSession ignored — session coroutine already pending.");
                return;
            }

            if (m_SessionConfig == null)
            {
                Debug.LogError("[PPSAppController] No PpsSessionConfig assigned.");
                return;
            }

            if (Protocol == null)
            {
                Debug.LogError("[PPSAppController] PpsSessionConfig has no PpsTaskAsset assigned.");
                return;
            }

            // Metadata source: the clinical panel (via SetSessionMetadata) or the
            // SessionConfig (assembled in Start, or here as a fallback).
            if (!m_SessionMetadataSet)
            {
                SetSessionMetadata(m_SessionConfig.BuildMetadata());
                Debug.Log("[PPSAppController] Built session metadata from PpsSessionConfig.");
            }

            // Protocol: clone the asset for this session and apply any
            // clinician-panel overrides to the CLONE. The on-disk asset is
            // never mutated; setup.json records what actually ran.
            m_RuntimeProtocol = Protocol.CreateSessionClone();
            m_RuntimeProtocol.ApplyTask1SessionOverrides(m_SessionMetadata);

            if (m_TaskManager != null)
                m_TaskManager.TaskAsset = m_RuntimeProtocol;

            // Record the protocol that will run into the metadata for setup.json.
            // One-directional (clone -> record); never read back to drive the run.
            m_SessionMetadata.PopulateFromPpsTaskAsset(m_RuntimeProtocol);

            Debug.Log("[PPSAppController] Protocol from asset (sole authority): " +
                      $"participant={m_SessionMetadata.participantId}, " +
                      $"session={m_SessionMetadata.sessionNumber}, " +
                      $"BlockCount={Protocol.BlockCount}, " +
                      $"VT={Protocol.VtTrialsPerBlock}, " +
                      $"V={Protocol.VisualOnlyTrialsPerBlock}, " +
                      $"T={Protocol.TactileOnlyTrialsPerBlock}, " +
                      $"Break={Protocol.RestDurationSeconds}s, " +
                      $"WideOffset={Protocol.WideOffsetMeters}m");

            m_StopRequested = false;
            m_SessionCoroutine = StartCoroutine(RunSessionInternal());
        }

        public void PauseSession()
        {
            if (!m_Running || m_TaskManager == null || m_TaskManager.IsPaused) return;

            m_TaskManager.PauseBlock();

            // Progress snapshot now lives in the shared TaskLogger; uses the
            // task manager's current block/trial cursor.
            m_TaskLogger?.Flush(m_TaskManager.CurrentBlockIndex, m_TaskManager.TrialsCompletedInBlock);

            m_EegMarkerEmitter?.Emit("pps_session_paused");
            SessionPaused?.Invoke();
            Debug.Log("[PPSAppController] Session paused.");
        }

        public void ResumeSession()
        {
            if (m_TaskManager == null || !m_TaskManager.IsPaused) return;

            m_TaskManager.ResumeBlock();
            m_EegMarkerEmitter?.Emit("pps_session_resumed");
            SessionResumed?.Invoke();
            Debug.Log("[PPSAppController] Session resumed.");
        }
        
        public void RequestParticipantPause()
        {
            Debug.Log("[PPS APP] Participant pause requested.");
            PauseSession();
        }

        public void ResumeFromParticipantPauseNextTrial()
        {
            Debug.Log("[PPS APP] Resume from participant pause.");

            ResumeSession();
        }

        public void RestartCurrentBlockFromParticipantPause()
        {
            Debug.Log("[PPS APP] Restart current block from participant pause.");

            m_RestartCurrentBlockRequested = true;

            if (m_TaskManager != null)
                m_TaskManager.RequestAbortCurrentRun();

            ResumeSession();
        }

        public void StopTaskFromParticipantPause()
        {
            Debug.Log("[PPS APP] Stop task from participant pause.");

            m_StopRequested = true;

            if (m_TaskManager != null)
                m_TaskManager.RequestAbortCurrentRun();

            ResumeSession();
        }

        private IEnumerator Start()
        {
            Debug.Log("[PPSAppController] Start called.");
            yield return null;

            if (m_Ui == null)
            {
                Debug.LogError("[PPSAppController] UI is not assigned.");
                yield break;
            }
            if (m_TaskManager == null)
            {
                Debug.LogError("[PPSAppController] TaskManager is not assigned.");
                yield break;
            }
            if (m_SessionConfig == null)
            {
                Debug.LogError("[PPSAppController] No PpsSessionConfig assigned.");
                yield break;
            }
            if (Protocol == null)
            {
                Debug.LogError("[PPSAppController] PpsSessionConfig has no PpsTaskAsset assigned.");
                yield break;
            }

            AutoWireOptionalReferences();

            if (m_SessionConfig.UseClinicalPanel)
            {
                Debug.Log("[PPSAppController] PpsSessionConfig: clinical panel enabled. " +
                          "Waiting for the panel to start the session.");
                yield break;
            }

            // Panel off: this SessionConfig drives a panel-free run.
            SetSessionMetadata(m_SessionConfig.BuildMetadata());
            Debug.Log($"[PPSAppController] PpsSessionConfig drives the run (panel off): " +
                      $"participant={m_SessionConfig.ParticipantId}, session={m_SessionConfig.SessionNumber}.");

            if (m_AutoStartWhenPanelOff)
            {
                Debug.Log("[PPSAppController] Auto-starting.");
                StartSession();
            }
            else
            {
                Debug.Log("[PPSAppController] AutoStartWhenPanelOff=false — waiting for StartSession() from a button.");
            }
        }

        void AutoWireOptionalReferences()
        {
            if (m_EegMarkerEmitter == null)
            {
                m_EegMarkerEmitter = FindAnyObjectByType<EegMarkerEmitter>();

                if (m_EegMarkerEmitter == null)
                    Debug.LogWarning("[PPSAppController] No EegMarkerEmitter in scene. EEG markers disabled.");
                else
                    Debug.LogWarning($"[PPSAppController] EegMarkerEmitter auto-wired to: {m_EegMarkerEmitter.name}");
            }

            if (m_EegMarkerEmitter != null && !m_EegMarkerEmitter.gameObject.activeInHierarchy)
            {
                Debug.LogError($"[PPSAppController] Assigned EegMarkerEmitter is inactive: {m_EegMarkerEmitter.name}");
            }

            if (m_TaskLogger == null)
            {
                m_TaskLogger = FindAnyObjectByType<TaskLogger>();

                if (m_TaskLogger == null)
                    Debug.LogWarning("[PPSAppController] No TaskLogger in scene. Trial data will NOT be written to disk.");
                else
                    Debug.LogWarning($"[PPSAppController] TaskLogger auto-wired to: {m_TaskLogger.name}");
            }
        }

        private IEnumerator RunSessionInternal()
        {
            // EEG marker session
            if (m_EegMarkerEmitter == null)
            {
                m_EegMarkerEmitter = FindAnyObjectByType<EegMarkerEmitter>();

                if (m_EegMarkerEmitter == null)
                {
                    Debug.LogWarning("[PPSAppController] No EegMarkerEmitter found in the scene. EEG markers will be disabled.");
                }
                else
                {
                    Debug.Log("[PPSAppController] Found EegMarkerEmitter automatically.");
                }
            }

            if (m_EegMarkerEmitter != null)
            {
                // Identity flows FROM the metadata (config or panel) INTO the
                // emitter, before BeginSession opens the marker CSV. The emitter
                // is a sink for identity now, not a source of it.
                string sessionLabel = !string.IsNullOrEmpty(m_SessionMetadata.sessionId)
                    ? m_SessionMetadata.sessionId
                    : $"S{m_SessionMetadata.sessionNumber}";

                m_EegMarkerEmitter.SetIdentity(m_SessionMetadata.participantId, sessionLabel);
                m_EegMarkerEmitter.BeginSession();
                m_TaskManager.SetMarkerEmitter(m_EegMarkerEmitter);
            }

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.EnterTaskMode();

            // Attention checks: the manager invokes this every N main-block
            // trials (N from the asset, 0 = off); the panel itself lives in
            // SessionFlowPanels (m_AttentionCheckPanel).
            if (m_Ui != null)
                m_TaskManager.AttentionCheckRoutine = () => m_Ui.ShowAttentionCheckAndWait();

            // Resolve controller input
            if (m_ControllerInputBehaviour != null)
            {
                m_ControllerInput = m_ControllerInputBehaviour as IResponseInputSource;
                if (m_ControllerInput == null)
                    Debug.LogError("[PPSAppController] m_ControllerInputBehaviour does not implement IResponseInputSource.");
            }

            if (m_ControllerInput != null)
            {
                m_TaskManager.SetInputSource(m_ControllerInput);
                m_ControllerInput.Enable();
            }
            else if (m_KeyboardInput != null)
            {
                m_TaskManager.SetInputSource(m_KeyboardInput);
            }
            m_KeyboardInput?.Enable();

            m_Running = true;
            SessionStarted?.Invoke();

            yield return RunTask1();

            m_Running = false;
            m_SessionCoroutine = null;
            SessionEnded?.Invoke();

            // Release this session's protocol clone; the manager goes back to
            // pointing at the source asset so no destroyed reference lingers.
            if (m_RuntimeProtocol != null)
            {
                if (m_TaskManager != null)
                    m_TaskManager.TaskAsset = Protocol;
                Destroy(m_RuntimeProtocol);
                m_RuntimeProtocol = null;
            }
        }

        private IEnumerator RunPreTaskPanels()
        {
            int step = 0;

            while (!StopWasRequested())
            {
                m_Ui.ClearBackRequest();

                switch (step)
                {
                    case 0:
                        yield return m_Ui.ShowWelcomeAndWait();
                        break;

                    case 1:
                        yield return m_Ui.ShowInstructionsAndWait();
                        break;

                    case 2:
                        m_Ui.ShowStandingCross();
                        yield return m_Ui.ShowPositioningAndWait();
                        break;

                    case 3:
                        yield return m_Ui.ShowPracticeIntroVTOnlyAndWait();
                        break;
                }

                if (StopWasRequested())
                    yield break;

                if (m_Ui.BackRequested)
                {
                    m_Ui.ClearBackRequest();

                    if (step == 2)
                        m_Ui.HideStandingCross();

                    step = Mathf.Max(0, step - 1);
                }
                else
                {
                    step++;
                }

                if (step > 3)
                    yield break;
            }
        }

        private IEnumerator RunTask1()
        {
            // Set tokens once up front so any panel that references
            // {blocksCount}, {currentBlock}, {totalBlocks}, or {breakTime}
            // resolves correctly from the first screen onward.
            m_Ui.SetTokens(
                blocksCount:   Asset.BlockCount,
                currentBlock:  0,
                totalBlocks:   Asset.BlockCount,
                breakSeconds:  Asset.RestDurationSeconds
            );

             // Navigable intro panels only.
            yield return RunPreTaskPanels();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

           // ---- Practice 1: tactile only / vibration only ----
            Debug.Log("[PPS] Starting VT-only practice.");
            yield return m_TaskManager.RunTrials(PpsTrialGenerator.GenerateVTOnlyPractice(Asset));
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }


            // ---- Practice 2 intro: visual only / light only ----
            yield return m_Ui.ShowPracticeIntroVOnlyAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            // ---- Practice 2: visual only / light only ----
            Debug.Log("[PPS] Starting Visual-only practice.");
            yield return m_TaskManager.RunTrials(PpsTrialGenerator.GenerateVOnlyPractice(Asset));
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }


            // ---- Practice 3 intro: vibration + visual ----
            yield return m_Ui.ShowPracticeIntroVTVisualAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            // ---- Practice 3: vibration + visual ----
            Debug.Log("[PPS] Starting VT+Visual practice.");
            yield return m_TaskManager.RunTrials(PpsTrialGenerator.GenerateVTVisualPractice(Asset));
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            yield return m_Ui.ShowNoFeedbackAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            yield return m_Ui.ShowReadyToStartAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }
            // Logging: open the shared TaskLogger with TaskKind.Task1Pps so
            // file names, setup.json, and session.json all carry the task1
            // layout. PpsTaskManager only emits TrialCompleted; the logger
            // owns the disk. Identity in the metadata already came from the
            // SessionConfig (or panel) and was pushed to the emitter earlier.
            if (m_TaskLogger != null)
            {
                m_TaskLogger.ParticipantId = m_SessionMetadata.participantId;

                m_TaskLogger.SetMetadata(m_SessionMetadata);
                m_TaskLogger.BeginSession(TaskKind.Task1Pps, Asset.TaskName);

                m_LoggerTrialHandler = m_TaskLogger.LogTrial;
                m_TaskManager.TrialCompleted += m_LoggerTrialHandler;

                Debug.Log("================================");
                Debug.Log($"[PpsAppController] RECORDING STARTED -> {m_TaskLogger.SessionDirectory}");
                Debug.Log("================================");
                RecordingStarted?.Invoke();
            }

            m_TaskManager.BeginSession();

            for (int blockIndex = 0; blockIndex < Asset.BlockCount; blockIndex++)
            {
                bool blockCompleted = false;

                while (!blockCompleted)
                {
                    m_RestartCurrentBlockRequested = false;

                    m_Ui.SetTokens(
                        blocksCount:  Asset.BlockCount,
                        currentBlock: blockIndex + 1,
                        totalBlocks:  Asset.BlockCount,
                        breakSeconds: Asset.RestDurationSeconds
                    );

                    yield return m_Ui.ShowBlockCounterAndWait(blockIndex, Asset.BlockCount);
                    if (StopWasRequested()) { yield return StopExperiment(); yield break; }

                    PpsTrialDefinition[] trials = Asset.GenerateBlock(blockIndex);

                    yield return m_TaskManager.RunTrials(trials, blockIndex);

                    if (StopWasRequested()) { yield return StopExperiment(); yield break; }

                    if (m_RestartCurrentBlockRequested)
                    {
                        Debug.Log($"[PPS APP] Restarting block {blockIndex + 1}.");
                        continue;
                    }

                    blockCompleted = true;
                }

                if (blockIndex < Asset.BlockCount - 1)
                {
                    yield return m_Ui.ShowBreakAndWait(Asset.RestDurationSeconds);
                    if (StopWasRequested()) { yield return StopExperiment(); yield break; }
                }
            }

            CloseLoggingSession();

            if (m_ClinicianPanel != null) m_ClinicianPanel.ExitTaskMode();

            m_ControllerInput?.Disable();
            m_KeyboardInput?.Disable();

            m_Ui.HideStandingCross();

            yield return m_Ui.ShowEndAndWait("Task 1 complete.\n\nThank you.");
        }

        /// <summary>
        /// Unsubscribes the logger from TrialCompleted and closes the session
        /// folder. Safe to call multiple times.
        /// </summary>
        void CloseLoggingSession()
        {
            m_TaskManager.EndSession();

            if (m_TaskLogger != null)
            {
                if (m_LoggerTrialHandler != null)
                {
                    m_TaskManager.TrialCompleted -= m_LoggerTrialHandler;
                    m_LoggerTrialHandler = null;
                }
                m_TaskLogger.EndSession();
            }
        }

        private bool StopWasRequested()
        {
            if (m_StopRequested) return true;
            return m_Ui != null && m_Ui.StopRequested;
        }

        private IEnumerator StopExperiment()
        {
            Debug.Log("[PPSAppController] Stop requested. Ending task.");

            CloseLoggingSession();

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.ExitTaskMode();

            m_ControllerInput?.Disable();
            m_KeyboardInput?.Disable();

            if (m_Ui != null)
            {
                m_Ui.HideStandingCross();
                // Show the end message WITHOUT blocking session teardown on
                // it. Blocking here delayed m_Running=false / SessionEnded by
                // the full end-panel wait (~10 s), which made the clinician
                // panel's End Session button feel unresponsive. Fire-and-
                // forget keeps the participant-facing message on screen while
                // the session state and the panel update immediately.
                StartCoroutine(m_Ui.ShowEndAndWait("Task stopped.\n\nThank you."));
            }
            yield break;
        }
    }
}