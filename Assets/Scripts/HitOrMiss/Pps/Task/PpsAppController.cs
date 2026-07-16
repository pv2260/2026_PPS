using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    public class PpsAppController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyboardCommandInput m_KeyboardInput;
        [SerializeField] private MonoBehaviour m_ControllerInputBehaviour;

        private IResponseInputSource m_ControllerInput;

        [SerializeField] private SessionFlowPanels m_Ui;
        [SerializeField] private PpsTaskManager m_TaskManager;
        [SerializeField] private PpsTaskAsset m_TaskAsset;

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

        [Header("Start mode")]
        [Tooltip("If true, the task begins automatically when the scene loads (legacy / dev shortcut). " +
                 "If false, waits for StartSession() to be called by the clinician panel or the HTTP server. " +
                 "Set to false for the clinician-driven flow that matches Task 2.")]
        [SerializeField] bool m_AutoStartOnPlay = false;

        // ------------------------------------------------------------------
        // Inspector session (clinician-panel opt-out)
        // ------------------------------------------------------------------
        [Header("Inspector session (clinician-panel opt-out)")]
        [Tooltip("If true, the clinician panel is bypassed and the SessionMetadata entered " +
                 "below is used directly.\n\n" +
                 "IMPORTANT: participant id and session are NOT read from here when an " +
                 "EegMarkerEmitter is present. Type those into the EegMarkerEmitter instead; " +
                 "the emitter is the single source of truth for id/session so the EEG stream " +
                 "and the CSV always agree. Use this block for everything else: shoulder width, " +
                 "group, equipment flags, session type, notes. Any task-count fields left at " +
                 "zero fall back to the PpsTaskAsset values.")]
        [SerializeField] bool m_UseInspectorMetadata = false;

        [Tooltip("Session metadata used when 'Use Inspector Metadata' is true. Ignored otherwise. " +
                 "Leave participantId and sessionNumber blank/zero when an EegMarkerEmitter is in " +
                 "the scene; they are overwritten by the emitter before logging begins.")]
        [SerializeField] SessionMetadata m_InspectorMetadata;

        SessionMetadata m_SessionMetadata;
        bool m_SessionMetadataSet;

        // Runtime-only clone of m_TaskAsset that carries the clinician form's
        // overrides (block count, per-modality trial counts, ITI, etc). Lives
        // for the duration of one session; destroyed on EndSession / Stop.
        PpsTaskAsset m_SessionAsset;
        PpsTaskAsset Asset => m_SessionAsset != null ? m_SessionAsset : m_TaskAsset;

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

        public string ParticipantId
        {
            get
            {
                if (m_EegMarkerEmitter != null)
                    return m_EegMarkerEmitter.ParticipantId;

                return m_SessionMetadata.participantId;
            }
        }

        /// <summary>
        /// Asks the running task to wind down at the next checkpoint.
        /// The task coroutine polls StopWasRequested() between steps.
        /// </summary>
        public void RequestStop()
        {
            m_StopRequested = true;
            Debug.Log("[PPSAppController] RequestStop() — task will end at the next checkpoint.");
        }

        /// <summary>
        /// Applies session metadata before StartSession (or via StartSession overload).
        /// Subject id and shoulder width are propagated to the task manager so the
        /// LED separation and log filename reflect the participant on record.
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

            if (!m_SessionMetadataSet)
            {
                m_SessionMetadata.PopulateFromPpsTaskAsset(m_TaskAsset);
                m_SessionMetadataSet = true;
                Debug.Log("[PPSAppController] No metadata set; using defaults derived from the task asset.");
            }
            // NOTE: when metadata IS already set (clinician form OR Inspector
            // opt-out), we do NOT overwrite it from the asset. Those values are
            // the source of truth; they get pushed INTO the asset clone below.

            // Apply form overrides onto a session-local clone so the on-disk
            // PpsTaskAsset stays untouched. The clone is what the TaskManager
            // and PpsTrialGenerator read for block count, trial counts, ITI,
            // wide offset, etc.
            if (m_TaskAsset != null)
            {
                m_SessionAsset = m_TaskAsset.CreateSessionClone();
                m_SessionAsset.ApplyTask1SessionOverrides(m_SessionMetadata);
                if (m_TaskManager != null) m_TaskManager.TaskAsset = m_SessionAsset;

                Debug.Log("[PPSAppController] Session asset clone applied. " +
                          $"BlockCount={m_SessionAsset.BlockCount}, " +
                          $"VT={m_SessionAsset.VtTrialsPerBlock}, " +
                          $"V={m_SessionAsset.VisualOnlyTrialsPerBlock}, " +
                          $"T={m_SessionAsset.TactileOnlyTrialsPerBlock}, " +
                          $"Break={m_SessionAsset.RestDurationSeconds}s, " +
                          $"WideOffset={m_SessionAsset.WideOffsetMeters}m");
            }

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
            if (m_TaskAsset == null)
            {
                Debug.LogError("[PPSAppController] TaskAsset is not assigned.");
                yield break;
            }

            AutoWireOptionalReferences();

            // Clinician-panel opt-out: seed the session metadata from the
            // Inspector block before any auto-start decision. This sets
            // m_SessionMetadataSet = true, so StartSession() will not fall back
            // to the asset defaults, and the values flow into the session
            // asset clone the same way the clinician form's values would.
            // Clinician-panel opt-out: seed the session metadata from the
            // Inspector block before any auto-start decision. This sets
            // m_SessionMetadataSet = true, so StartSession() will not fall back
            // to the asset defaults. Participant id / session in this block are
            // superseded later by the EegMarkerEmitter when one is present.
            if (m_UseInspectorMetadata)
            {
                SetSessionMetadata(m_InspectorMetadata);
                Debug.Log("[PPSAppController] Clinician panel bypassed. Using Inspector metadata " +
                          $"(shoulderWidthCm={m_InspectorMetadata.shoulderWidthCm}). " +
                          "Participant id / session will come from the EegMarkerEmitter if present.");
            }

            if (m_AutoStartOnPlay)
            {
                Debug.Log("[PPSAppController] AutoStartOnPlay=true — starting session immediately.");
                StartSession();
            }
            else
            {
                Debug.Log("[PPSAppController] AutoStartOnPlay=false — waiting for StartSession() (clinician panel, network, or a button).");
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
                m_EegMarkerEmitter.BeginSession();
                m_TaskManager.SetMarkerEmitter(m_EegMarkerEmitter);
            }

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.EnterTaskMode();

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
            // owns the disk.
            if (m_TaskLogger != null)
            {
                // Participant id + session number have ONE authority: the
                // EegMarkerEmitter when it is present. This keeps the EEG
                // marker stream and the CSV / setup.json in agreement, and
                // means you only ever type the id/session in one place (the
                // emitter). The Inspector metadata block supplies everything
                // else (shoulder width, group, equipment, session type,
                // notes); its participantId / sessionNumber are used only as a
                // fallback when no emitter is in the scene.
                if (m_EegMarkerEmitter != null)
                {
                    m_SessionMetadata.participantId = m_EegMarkerEmitter.ParticipantId;
                    m_SessionMetadata.sessionNumber = ParseSessionNumber(m_EegMarkerEmitter.SessionId);

                    Debug.Log(
                        $"[PPSAppController] Participant id / session taken from EegMarkerEmitter: " +
                        $"participant={m_SessionMetadata.participantId}, " +
                        $"session={m_SessionMetadata.sessionNumber}."
                    );
                }
                else if (m_UseInspectorMetadata)
                {
                    Debug.Log(
                        $"[PPSAppController] No EegMarkerEmitter in scene. Using Inspector " +
                        $"participant id / session: participant={m_SessionMetadata.participantId}, " +
                        $"session={m_SessionMetadata.sessionNumber}."
                    );
                }
                else
                {
                    Debug.LogWarning("[PPSAppController] No EegMarkerEmitter assigned. Using existing session metadata.");
                }

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
        /// Unsubscribes the logger from TrialCompleted, closes the session
        /// folder, and restores the original PpsTaskAsset on the task manager
        /// (destroying the runtime clone). Safe to call multiple times.
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

            // Hand the manager back the on-disk asset and dispose of the
            // session clone so we don't leak ScriptableObjects across runs.
            if (m_SessionAsset != null)
            {
                if (m_TaskManager != null) m_TaskManager.TaskAsset = m_TaskAsset;
                Destroy(m_SessionAsset);
                m_SessionAsset = null;
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
                yield return m_Ui.ShowEndAndWait("Task stopped.\n\nThank you.");
            }
        }

        private int ParseSessionNumber(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return 1;

            sessionId = sessionId.Trim();

            if (sessionId.StartsWith("S", System.StringComparison.OrdinalIgnoreCase))
                sessionId = sessionId.Substring(1);

            if (int.TryParse(sessionId, out int parsed))
                return parsed;

            Debug.LogWarning($"[PPSAppController] Could not parse session ID '{sessionId}'. Defaulting to session 1.");
            return 1;
        }
    }
}