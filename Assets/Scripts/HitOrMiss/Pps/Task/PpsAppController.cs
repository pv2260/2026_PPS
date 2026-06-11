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
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;
        [Tooltip("Single TaskLogger shared with Task 2. Drag the scene's TaskLogger here. " +
                 "It writes the per-session folder, trials CSV, setup.json, and session.json.")]
        [SerializeField] TaskLogger m_TaskLogger;

        // Cached delegate so we can unsubscribe with the same reference.
        System.Action<PpsTrialResult> m_LoggerTrialHandler;

        [Header("Visuals")]
        [SerializeField] FixationCrossController m_FixationCross;
        [SerializeField] StandingCross m_StandingCross;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        [Header("Session")]
        [SerializeField] private string m_SubjectIdFallback = "P000";

        [Header("Start mode")]
        [Tooltip("If true, the task begins automatically when the scene loads (legacy / dev shortcut). " +
                 "If false, waits for StartSession() to be called by the clinician panel or the HTTP server. " +
                 "Set to false for the clinician-driven flow that matches Task 2.")]
        [SerializeField] bool m_AutoStartOnPlay = false;

        SessionMetadata m_SessionMetadata;
        bool m_SessionMetadataSet;

        Coroutine m_SessionCoroutine;
        private bool m_Running;
        private bool m_StopRequested;

        public bool IsRunning => m_Running;

        public bool IsPaused => m_TaskManager != null && m_TaskManager.IsPaused;

        public string ParticipantId =>
            m_SessionMetadataSet && !string.IsNullOrEmpty(m_SessionMetadata.participantId)
                ? m_SessionMetadata.participantId
                : m_SubjectIdFallback;

        public event System.Action SessionStarted;
        public event System.Action SessionEnded;
        public event System.Action SessionPaused;
        public event System.Action SessionResumed;
        private bool m_RestartCurrentBlockRequested;

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

            Debug.Log($"[PPSAppController] Metadata applied. participantId={metadata.participantId}, shoulderWidthCm={metadata.shoulderWidthCm}");
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
                m_SessionMetadata = SessionMetadata.CreateDefault(m_SubjectIdFallback);
                m_SessionMetadata.PopulateFromPpsTaskAsset(m_TaskAsset);
                m_SessionMetadataSet = true;
                Debug.Log("[PPSAppController] No metadata set; using defaults derived from the task asset.");
            }
            else
            {
                m_SessionMetadata.PopulateFromPpsTaskAsset(m_TaskAsset);
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

            if (m_AutoStartOnPlay)
            {
                Debug.Log("[PPSAppController] AutoStartOnPlay=true — starting session immediately.");
                StartSession();
            }
            else
            {
                Debug.Log("[PPSAppController] AutoStartOnPlay=false — waiting for clinician StartSession() (panel or network).");
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
                    Debug.Log("[PPSAppController] EegMarkerEmitter auto-wired.");
            }

            if (m_TaskLogger == null)
            {
                m_TaskLogger = FindAnyObjectByType<TaskLogger>();
                if (m_TaskLogger == null)
                    Debug.LogWarning("[PPSAppController] No TaskLogger in scene. Trial data will NOT be written to disk.");
                else
                    Debug.Log("[PPSAppController] TaskLogger auto-wired.");
            }
        }

        private IEnumerator RunSessionInternal()
        {
            // EEG marker session
            if (m_EegMarkerEmitter != null)
            {
                string sessionId = !string.IsNullOrEmpty(m_SessionMetadata.sessionId)
                    ? m_SessionMetadata.sessionId
                    : System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                m_EegMarkerEmitter.BeginSession(sessionId);
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

        private IEnumerator RunTask1()
        {
            // Set tokens once up front so any panel that references
            // {blocksCount}, {currentBlock}, {totalBlocks}, or {breakTime}
            // resolves correctly from the first screen onward.
            m_Ui.SetTokens(
                blocksCount:   m_TaskAsset.BlockCount,
                currentBlock:  0,
                totalBlocks:   m_TaskAsset.BlockCount,
                breakSeconds:  m_TaskAsset.RestDurationSeconds
            );

            yield return m_Ui.ShowWelcomeAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            yield return m_Ui.ShowInstructionsAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            m_Ui.ShowStandingCross();

            yield return m_Ui.ShowPositioningAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            // ---- Practice 1: tactile only ----
            yield return m_Ui.ShowPracticeIntroVTOnlyAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            Debug.Log("[PPS] Starting VT-only practice.");
            yield return m_TaskManager.RunTrials(PpsTrialGenerator.GenerateVTOnlyPractice(m_TaskAsset));
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            // ---- Practice 2: visual + tactile ----
            yield return m_Ui.ShowPracticeIntroVTVisualAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            Debug.Log("[PPS] Starting VT+Visual practice.");
            yield return m_TaskManager.RunTrials(PpsTrialGenerator.GenerateVTVisualPractice(m_TaskAsset));
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
                m_TaskLogger.ParticipantId = ParticipantId;
                m_SessionMetadata.PopulateFromPpsTaskAsset(m_TaskAsset);
                m_TaskLogger.SetMetadata(m_SessionMetadata);
                m_TaskLogger.BeginSession(TaskKind.Task1Pps, m_TaskAsset.TaskName);

                m_LoggerTrialHandler = m_TaskLogger.LogTrial;
                m_TaskManager.TrialCompleted += m_LoggerTrialHandler;
            }

            m_TaskManager.BeginSession();

            for (int blockIndex = 0; blockIndex < m_TaskAsset.BlockCount; blockIndex++)
            {
                bool blockCompleted = false;

                while (!blockCompleted)
                {
                    m_RestartCurrentBlockRequested = false;

                    m_Ui.SetTokens(
                        blocksCount:  m_TaskAsset.BlockCount,
                        currentBlock: blockIndex + 1,
                        totalBlocks:  m_TaskAsset.BlockCount,
                        breakSeconds: m_TaskAsset.RestDurationSeconds
                    );

                    yield return m_Ui.ShowBlockCounterAndWait(blockIndex, m_TaskAsset.BlockCount);
                    if (StopWasRequested()) { yield return StopExperiment(); yield break; }

                    PpsTrialDefinition[] trials = m_TaskAsset.GenerateBlock(blockIndex);

                    yield return m_TaskManager.RunTrials(trials, blockIndex);

                    if (StopWasRequested()) { yield return StopExperiment(); yield break; }

                    if (m_RestartCurrentBlockRequested)
                    {
                        Debug.Log($"[PPS APP] Restarting block {blockIndex + 1}.");
                        continue;
                    }

                    blockCompleted = true;
                }

                if (blockIndex < m_TaskAsset.BlockCount - 1)
                {
                    yield return m_Ui.ShowBreakAndWait(m_TaskAsset.RestDurationSeconds);
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
        /// folder. Safe to call multiple times; no-op if logging never began.
        /// </summary>
        void CloseLoggingSession()
        {
            m_TaskManager.EndSession();

            if (m_TaskLogger == null) return;

            if (m_LoggerTrialHandler != null)
            {
                m_TaskManager.TrialCompleted -= m_LoggerTrialHandler;
                m_LoggerTrialHandler = null;
            }
            m_TaskLogger.EndSession();
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

            if (m_ClinicianPanel != null) m_ClinicianPanel.ExitTaskMode();

            m_ControllerInput?.Disable();
            m_KeyboardInput?.Disable();

            if (m_Ui != null)
            {
                m_Ui.HideStandingCross();
                yield return m_Ui.ShowEndAndWait("Task stopped.\n\nThank you.");
            }
        }
    }
}
