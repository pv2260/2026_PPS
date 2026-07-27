using System.Collections;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Top-level orchestrator for the Hit-or-Miss assessment (Task 2).
    ///
    /// ONE SOURCE FOR EVERYTHING, funneled through a SessionConfig asset:
    ///   - protocol  -> SessionConfig.TaskAsset (shared, reusable). Runs directly,
    ///                  never overridden.
    ///   - identity  -> SessionConfig (panel off) or the clinical panel (panel on).
    ///                  Pushed INTO the EegMarkerEmitter via SetIdentity; the
    ///                  emitter no longer stores id/session itself.
    ///   - subject   -> SessionConfig.SubjectInfo (panel off) or the panel (panel on).
    /// SessionConfig.UseClinicalPanel is the switch. SessionMetadata is a
    /// write-once record assembled from these; nothing reads it back to drive
    /// the run.
    /// </summary>
    public class HitOrMissAppController : MonoBehaviour
    {
        const float kTooSlowDisplaySeconds = 1.0f;

        [Header("Session")]
        [Tooltip("The single per-session entry point. References the protocol asset and holds " +
                 "identity, subject data, and the use-panel switch. Create via " +
                 "Assets > Create > Parkinson > HitOrMiss > Session Config.")]
        [SerializeField] SessionConfigAsset m_SessionConfig;

        [Tooltip("When the SessionConfig has the clinical panel OFF, start the session " +
                 "automatically on Play. Turn this off to start from a button (call StartSession()).")]
        [SerializeField] bool m_AutoStartWhenPanelOff = true;

        [Header("Task")]
        [SerializeField] TrajectoryTaskManager m_TaskManager;

        [Header("Input")]
        [SerializeField] ControllerButtonInput m_ControllerInput;
        [SerializeField] KeyboardCommandInput m_KeyboardInput;
        [SerializeField] HandPinchInput m_HandPinchInput;

        [Header("Logging")]
        [SerializeField] TaskLogger m_TaskLogger;
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;

        [Header("Localization")]
        [SerializeField] LocalizedTermTable m_TermTable;
        [SerializeField] LocalizedUITextBinder[] m_UITextBinders;

        [Header("Welcome")]
        [Tooltip("Plain panel shown once at session start, before the popup sequence. A GameObject, " +
                 "not part of the TaskPopupPanel system. Dismissed by any trigger press.")]
        [SerializeField] GameObject m_WelcomePanel;

        [Header("Pre-practice popups")]
        [SerializeField] TaskPopupPanel[] m_PrePracticePopups;

        [Header("Controller practice")]
        [SerializeField] TaskPopupPanel m_ControllerPracticeIntroPanel;

        [Header("Controller trigger demo")]
        [SerializeField] TaskPopupPanel m_TriggerDemoPopup;
        [SerializeField] HitOrMissControllerIntroDemo m_ControllerIntroDemo;

        [Header("Response mapping demo")]
        [SerializeField] TaskPopupPanel m_ResponseMappingPopup;
        [SerializeField] HitOrMissResponseMappingDemo m_ResponseMappingDemo;

        [Header("Difficult practice")]
        [SerializeField] TaskPopupPanel m_DifficultPracticeIntroPanel;
        [SerializeField] TaskPopupPanel m_PracticeRetryPanel;

        [Header("Shared practice feedback")]
        [SerializeField] TaskPopupPanel m_TooSlowPanel;
        [SerializeField] ResponseIndicator m_ResponseIndicator;

        [Header("Post-practice")]
        [SerializeField] TaskPopupPanel m_NoFeedbackPanel;
        [SerializeField] TaskPopupPanel m_ReadyToStartPanel;
        [SerializeField] TaskPopupPanel[] m_ExtraPostPracticePopups;

        [Header("Per-block popups")]
        [SerializeField] TaskPopupPanel m_BlockIntroPopup;
        [SerializeField] TaskPopupPanel m_BreakPopup;
        [SerializeField] TaskPopupPanel m_BlockReadyPopup;

        [Header("Participant check-in")]
        [Tooltip("Shown every TrajectoryTaskAsset.CheckInIntervalTrials trials inside a main block. " +
                 "Leave empty to disable the panel; the block then continues without pausing.")]
        [SerializeField] HitOrMissCheckInPanel m_CheckInPanel;

        [Tooltip("Seconds the check-in panel ignores input after appearing. Without this the trigger " +
                 "press that answered the previous trial can bounce straight through the panel.")]
        [SerializeField] float m_CheckInArmDelaySeconds = 0.5f;

        [Header("End")]
        [SerializeField] TaskPopupPanel m_OutroPopup;

        [Header("Visuals")]
        [SerializeField] FixationCrossController m_FixationCross;
        [SerializeField] StandingCross m_StandingCross;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        SupportedLanguage m_Language = SupportedLanguage.English;
        TaskPhase m_CurrentPhase = TaskPhase.Idle;
        Coroutine m_SessionCoroutine;
        IResponseInputSource m_InputSource;

        SessionMetadata m_SessionMetadata;
        bool m_HasExternalSessionMetadata;

        System.Action<TrialDefinition> m_TooSlowHandler;

        public TaskPhase CurrentPhase => m_CurrentPhase;
        public TrajectoryTaskManager TaskManager => m_TaskManager;
        public string ParticipantId { get; set; } = "P000";
        public int CurrentBlockIndex { get; private set; }

        public bool IsPaused => m_TaskManager != null && m_TaskManager.IsPaused;

        public bool IsRecording =>
            m_TaskLogger != null && m_TaskLogger.IsSessionOpen;

        public SupportedLanguage CurrentLanguage => m_Language;

        public event System.Action SessionPaused;
        public event System.Action SessionResumed;
        public event System.Action<TaskPhase> PhaseChanged;
        public event System.Action SessionStarted;
        public event System.Action SessionEnded;

        // Protocol comes from the SessionConfig. During a session the task
        // runs a runtime CLONE of it (CreateSessionClone +
        // ApplyTask2SessionOverrides), so clinician-panel overrides adjust
        // this session without ever mutating the on-disk asset.
        TrajectoryTaskAsset m_RuntimeProtocol;
        TrajectoryTaskAsset Protocol => m_SessionConfig != null ? m_SessionConfig.TaskAsset : null;
        TrajectoryTaskAsset Asset => m_RuntimeProtocol != null ? m_RuntimeProtocol : Protocol;

        void Awake()
        {
            if (m_TaskManager != null)
            {
                if (Protocol != null)
                    m_TaskManager.TaskAsset = Protocol;

                m_TaskManager.SetMarkerEmitter(m_EegMarkerEmitter);
            }

            HideAllPopups();

            if (m_WelcomePanel != null)
                m_WelcomePanel.SetActive(false);

            if (m_FixationCross != null)
                m_FixationCross.Hide();
        }

        IEnumerator Start()
        {
            // Give every other component's Awake a frame to finish wiring.
            yield return null;

            if (m_SessionConfig == null)
            {
                Debug.LogWarning("[HitOrMissAppController] No SessionConfig assigned. " +
                                 "Waiting for StartSession() from the clinical panel, network, or a button.");
                yield break;
            }

            if (m_SessionConfig.UseClinicalPanel)
            {
                Debug.Log("[HitOrMissAppController] SessionConfig: clinical panel enabled. " +
                          "Waiting for the panel to start the session.");
                yield break;
            }

            // Panel off: this SessionConfig drives a panel-free run. Assemble the
            // metadata from it now; identity is pushed to the emitter in StartSession.
            SetSessionMetadata(m_SessionConfig.BuildMetadata());

            Debug.Log($"[HitOrMissAppController] SessionConfig drives the run (panel off): " +
                      $"participant={m_SessionConfig.ParticipantId}, session={m_SessionConfig.SessionNumber}.");

            if (m_AutoStartWhenPanelOff)
            {
                Debug.Log("[HitOrMissAppController] Auto-starting.");
                StartSession();
            }
            else
            {
                Debug.Log("[HitOrMissAppController] AutoStartWhenPanelOff=false — waiting for StartSession() from a button.");
            }
        }

        void SetPhase(TaskPhase phase)
        {
            if (m_CurrentPhase == phase)
                return;

            m_CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        public void SetSessionMetadata(SessionMetadata metadata)
        {
            m_SessionMetadata = metadata;
            m_HasExternalSessionMetadata = true;

            if (!string.IsNullOrEmpty(metadata.participantId))
                ParticipantId = metadata.participantId;
        }
        public void SetLanguage(SupportedLanguage language)
        {
            m_Language = language;

            if (m_UITextBinders == null)
                return;

            foreach (var binder in m_UITextBinders)
            {
                if (binder != null)
                    binder.Language = language;
            }
        }

        public void ToggleLanguage()
        {
            SetLanguage(
                m_Language == SupportedLanguage.English
                    ? SupportedLanguage.French
                    : SupportedLanguage.English
            );
        }

        public void SetLanguageEnglish()
        {
            SetLanguage(SupportedLanguage.English);
        }

        public void SetLanguageFrench()
        {
            SetLanguage(SupportedLanguage.French);
        }

        public void StartSession()
        {
            if (m_CurrentPhase != TaskPhase.Idle)
            {
                Debug.LogWarning("[HitOrMissAppController] Session already running.");
                return;
            }

            if (m_SessionConfig == null)
            {
                Debug.LogError("[HitOrMissAppController] No SessionConfig assigned.");
                return;
            }

            var protocol = Protocol;
            if (protocol == null)
            {
                Debug.LogError("[HitOrMissAppController] SessionConfig has no protocol asset (TaskAsset) assigned.");
                return;
            }

            if (m_TaskManager == null)
            {
                Debug.LogError("[HitOrMissAppController] No TrajectoryTaskManager assigned.");
                return;
            }

            if (m_EegMarkerEmitter == null)
            {
                m_EegMarkerEmitter = FindAnyObjectByType<EegMarkerEmitter>();

                if (m_EegMarkerEmitter == null)
                    Debug.LogWarning("[HitOrMissAppController] No EegMarkerEmitter found. EEG markers disabled.");
                else
                    Debug.Log("[HitOrMissAppController] Found EegMarkerEmitter automatically.");
            }

            m_TaskManager.SetMarkerEmitter(m_EegMarkerEmitter);

            var composite = new CompositeInputSource(
                m_ControllerInput,
                m_KeyboardInput,
                m_HandPinchInput
            );

            if (composite.Sources.Count == 0)
            {
                Debug.LogError("[HitOrMissAppController] No input sources assigned in inspector.");
                return;
            }

            m_InputSource = composite;
            m_TaskManager.SetInputSource(m_InputSource);

            m_TaskManager.CheckInDue -= OnCheckInDue;
            m_TaskManager.CheckInDue += OnCheckInDue;

            // Metadata source: the clinical panel (via SetSessionMetadata) or the
            // SessionConfig (assembled in Start, or here as a fallback).
            if (!m_HasExternalSessionMetadata)
            {
                m_SessionMetadata = m_SessionConfig.BuildMetadata();
                Debug.Log("[HitOrMissAppController] Built session metadata from SessionConfig.");
            }
            else
            {
                Debug.Log("[HitOrMissAppController] Using provided session metadata (panel or config).");
            }

            if (string.IsNullOrEmpty(m_SessionMetadata.participantId))
                m_SessionMetadata.participantId = ParticipantId;
            else
                ParticipantId = m_SessionMetadata.participantId;

            if (string.IsNullOrEmpty(m_SessionMetadata.sessionDate))
                m_SessionMetadata.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd");

            // Language: the panel's selection now actually drives the popup
            // localization (previously recorded but never applied).
            SetLanguage(
                string.Equals(m_SessionMetadata.language, "french", System.StringComparison.OrdinalIgnoreCase)
                    ? SupportedLanguage.French
                    : SupportedLanguage.English);

            // Protocol: clone the asset for this session and apply any
            // clinician-panel overrides (blocks, break duration) to the
            // CLONE. task2TrialsPerBlock arrives as 0 from the panel, which
            // deliberately skips the legacy flat-split path so the settled
            // per-category design in the asset always stands.
            m_RuntimeProtocol = protocol.CreateSessionClone();
            m_RuntimeProtocol.ApplyTask2SessionOverrides(m_SessionMetadata);
            m_TaskManager.TaskAsset = m_RuntimeProtocol;

            // Record the protocol that will run into the metadata for setup.json.
            // One-directional (clone -> record); never read back to drive the run.
            m_SessionMetadata.PopulateFromTaskAsset(m_RuntimeProtocol);

            Debug.Log(
                $"[HitOrMissAppController] Protocol from asset (sole authority): " +
                $"participantId={m_SessionMetadata.participantId}, " +
                $"session={m_SessionMetadata.sessionNumber}, " +
                $"BlockCount={protocol.BlockCount}, " +
                $"TrialsPerBlock={protocol.TrialsPerBlock}, " +
                $"shoulderWidthCm={m_SessionMetadata.shoulderWidthCm}"
            );

            // Identity flows FROM the metadata (config or panel) INTO the emitter.
            // The emitter is a sink for identity now, not a source of it.
            if (m_EegMarkerEmitter != null)
            {
                string sessionLabel = !string.IsNullOrEmpty(m_SessionMetadata.sessionId)
                    ? m_SessionMetadata.sessionId
                    : $"S{m_SessionMetadata.sessionNumber}";

                m_EegMarkerEmitter.SetIdentity(m_SessionMetadata.participantId, sessionLabel);
                m_EegMarkerEmitter.BeginSession();

                ParticipantId = m_SessionMetadata.participantId;
            }

            if (m_TaskLogger != null)
            {
                m_TaskLogger.SetMetadata(m_SessionMetadata);

                m_TaskLogger.BeginSession(
                    TaskKind.Task2HitOrMiss,
                    protocol.TaskName
                );

                m_TaskManager.TrialJudged -= m_TaskLogger.LogTrial;
                m_TaskManager.TrialJudged += m_TaskLogger.LogTrial;
            }

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.EnterTaskMode();

            m_SessionCoroutine = StartCoroutine(RunSession());
            SessionStarted?.Invoke();
        }

        public void StopSession()
        {
            if (m_SessionCoroutine != null)
            {
                StopCoroutine(m_SessionCoroutine);
                m_SessionCoroutine = null;
            }

            if (m_TaskManager != null && m_TaskManager.IsRunning)
                m_TaskManager.StopBlock();

            EndSession();
        }

        public void PauseSession()
        {
            if (m_CurrentPhase == TaskPhase.Idle)
                return;

            if (m_TaskManager == null || !m_TaskManager.IsRunning || m_TaskManager.IsPaused)
                return;

            m_TaskManager.PauseBlock();
            m_EegMarkerEmitter?.Emit("session_paused");

            if (m_TaskLogger != null)
                m_TaskLogger.Flush(CurrentBlockIndex, m_TaskManager.NextTrialIndex);

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.EnterPausedMode();

            SessionPaused?.Invoke();
        }

        public void ResumeSession()
        {
            if (m_TaskManager == null || !m_TaskManager.IsPaused)
                return;

            m_TaskManager.ResumeBlock();
            m_EegMarkerEmitter?.Emit("session_resumed");

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.ExitPausedMode();

            SessionResumed?.Invoke();
        }

        IEnumerator RunSession()
        {
            if (Asset == null)
            {
                Debug.LogError("[HitOrMissAppController] Cannot run session: Asset is null.");
                EndSession();
                yield break;
            }

            if (m_TaskManager == null)
            {
                Debug.LogError("[HitOrMissAppController] Cannot run session: TaskManager is null.");
                EndSession();
                yield break;
            }

            int blockCount = Asset.BlockCount;

            SetPhase(TaskPhase.Intro);
            m_EegMarkerEmitter?.Emit("phase_intro");

            // Welcome panel: a plain GameObject shown once before the popup flow.
            // Dismissed by any trigger press; falls back to a short timer if no
            // input source is wired, so the session can never hang here.
            if (m_WelcomePanel != null)
            {
                m_WelcomePanel.SetActive(true);

                if (m_InputSource != null)
                {
                    bool advance = false;
                    void OnWelcomeResponse(ResponseEvent _) => advance = true;

                    m_InputSource.ResponseReceived += OnWelcomeResponse;
                    m_InputSource.Enable();
                    try
                    {
                        while (!advance)
                            yield return null;
                    }
                    finally
                    {
                        m_InputSource.ResponseReceived -= OnWelcomeResponse;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(3f);
                }

                m_WelcomePanel.SetActive(false);
            }

            yield return RunPrePracticeSequence();

            SetPhase(TaskPhase.Practice);
            m_EegMarkerEmitter?.Emit("phase_controller_practice");
            yield return RunControllerPractice();

            m_EegMarkerEmitter?.Emit("phase_difficult_practice");
            yield return RunDifficultPractice();

            Debug.Log("[SESSION] Difficult practice done. Showing NoFeedback.");

            SetPhase(TaskPhase.Ready);
            yield return RunOnePopup(m_NoFeedbackPanel);

            Debug.Log("[SESSION] NoFeedback done. Showing ReadyToStart.");

            yield return RunOnePopup(m_ReadyToStartPanel);

            Debug.Log("[SESSION] ReadyToStart done. Entering blocks.");

            yield return RunPopupSequence(m_ExtraPostPracticePopups);

            Debug.Log(
                $"[SESSION] Starting block loop. " +
                $"blockCount={blockCount}, " +
                $"Asset.TrialsPerBlock={Asset.TrialsPerBlock}"
            );

            m_TaskManager.AutoResolveTimeouts = false;

            for (int b = 0; b < blockCount; b++)
            {
                CurrentBlockIndex = b;

                SetPhase(TaskPhase.BlockIntro);
                yield return RunOnePopup(m_BlockIntroPopup);

                SetPhase(TaskPhase.Block);
                m_EegMarkerEmitter?.Emit("phase_block");

                if (m_FixationCross != null)
                    m_FixationCross.Show();

                var blockTrials = Asset.GenerateBlock(
                    b,
                    m_SessionMetadata.shoulderWidthCm
                );

                int generatedCount = blockTrials != null ? blockTrials.Length : 0;

                Debug.Log(
                    $"[HitOrMissAppController] Block {b + 1}/{blockCount}: " +
                    $"generated blockTrials.Length={generatedCount}, " +
                    $"Asset.TrialsPerBlock={Asset.TrialsPerBlock}, " +
                    $"shoulderWidthCm={(m_SessionMetadata.shoulderWidthCm)}"
                );

                if (blockTrials == null || blockTrials.Length == 0)
                {
                    Debug.LogError(
                        $"[HitOrMissAppController] Block {b + 1} generated no trials. Ending session."
                    );

                    if (m_FixationCross != null)
                        m_FixationCross.Hide();

                    EndSession();
                    yield break;
                }

                if (blockTrials.Length != Asset.TrialsPerBlock)
                {
                    Debug.LogError(
                        $"[HitOrMissAppController] Trial count mismatch in block {b + 1}: " +
                        $"blockTrials.Length={blockTrials.Length}, " +
                        $"Asset.TrialsPerBlock={Asset.TrialsPerBlock}. " +
                        $"This can cause breakage if another component assumes Asset.TrialsPerBlock."
                    );
                }

                m_TaskManager.StartTrialList(b, blockTrials);

                while (m_TaskManager.IsRunning)
                    yield return null;

                Debug.Log(
                    $"[HitOrMissAppController] Block {b + 1} finished. " +
                    $"NextTrialIndex={m_TaskManager.NextTrialIndex}"
                );

                if (m_FixationCross != null)
                    m_FixationCross.Hide();

                if (b < blockCount - 1)
                {
                    SetPhase(TaskPhase.Rest);
                    m_EegMarkerEmitter?.Emit("phase_rest");
                    yield return RunOnePopup(m_BreakPopup);

                    SetPhase(TaskPhase.BlockReady);
                    yield return RunOnePopup(m_BlockReadyPopup);
                }
            }

            SetPhase(TaskPhase.Outro);
            m_EegMarkerEmitter?.Emit("phase_outro");
            yield return RunOnePopup(m_OutroPopup);

            EndSession();
        }

        IEnumerator RunControllerPractice()
        {
            yield return RunOnePopup(m_ControllerPracticeIntroPanel);
            yield return RunControllerIntroDemo();
            yield return RunResponseMappingDemo();
        }

        IEnumerator RunControllerIntroDemo()
        {
            if (m_TriggerDemoPopup == null)
            {
                Debug.LogWarning("[HitOrMissAppController] Trigger demo popup not assigned. Skipping.");
                yield break;
            }

            if (m_ControllerIntroDemo == null)
            {
                Debug.LogWarning("[HitOrMissAppController] HitOrMissControllerIntroDemo not assigned. Skipping.");
                yield break;
            }

            if (m_InputSource == null)
            {
                Debug.LogError("[HitOrMissAppController] No input source for trigger demo popup.");
                yield break;
            }

            var ctx = BuildPopupContext();
            m_TriggerDemoPopup.SetText(ctx.ResolveText(m_TriggerDemoPopup));
            m_TriggerDemoPopup.Show();

            m_ControllerIntroDemo.BeginDemo();

            bool leftPressed = false;
            bool rightPressed = false;

            // Which hand a command came from, for the demo highlight only.
            // RIGHT = Hit ("yes, it will hit me"), LEFT = Miss. This has to stay
            // in step with ControllerButtonInput and HandPinchInput, or the demo
            // lights up the opposite controller to the one actually pressed and
            // the participant is taught the wrong mapping.
            void Handler(ResponseEvent ev)
            {
                if (ev.command == SemanticCommand.Miss)
                {
                    leftPressed = true;
                    m_ControllerIntroDemo.LeftTriggerPressed();
                }
                else if (ev.command == SemanticCommand.Hit)
                {
                    rightPressed = true;
                    m_ControllerIntroDemo.RightTriggerPressed();
                }
            }

            m_InputSource.ResponseReceived += Handler;
            m_InputSource.Enable();

            try
            {
                while (!leftPressed || !rightPressed)
                    yield return null;

                yield return new WaitForSeconds(0.6f);
            }
            finally
            {
                m_InputSource.ResponseReceived -= Handler;
                m_TriggerDemoPopup.Hide();
            }
        }

        IEnumerator RunResponseMappingDemo()
        {
            if (m_ResponseMappingPopup == null)
            {
                Debug.LogWarning("[HitOrMissAppController] Response mapping popup not assigned. Skipping.");
                yield break;
            }

            if (m_ResponseMappingDemo == null)
            {
                Debug.LogWarning("[HitOrMissAppController] HitOrMissResponseMappingDemo not assigned. Skipping.");
                yield break;
            }

            if (m_InputSource == null)
            {
                Debug.LogError("[HitOrMissAppController] No input source for response mapping popup.");
                yield break;
            }

            var ctx = BuildPopupContext();
            m_ResponseMappingPopup.SetText(ctx.ResolveText(m_ResponseMappingPopup));
            m_ResponseMappingPopup.Show();

            m_ResponseMappingDemo.ResetDemo();

            bool leftPressed = false;
            bool rightPressed = false;

            // Same hand mapping as the trigger demo above: RIGHT = Hit, LEFT = Miss.
            void Handler(ResponseEvent ev)
            {
                if (ev.command == SemanticCommand.Miss)
                {
                    leftPressed = true;
                    m_ResponseMappingDemo.LeftPressed();
                }
                else if (ev.command == SemanticCommand.Hit)
                {
                    rightPressed = true;
                    m_ResponseMappingDemo.RightPressed();
                }
            }

            m_InputSource.ResponseReceived += Handler;
            m_InputSource.Enable();

            try
            {
                while (!leftPressed || !rightPressed)
                    yield return null;

                while (!m_ResponseMappingDemo.ReadyToAdvance)
                    yield return null;
            }
            finally
            {
                m_InputSource.ResponseReceived -= Handler;
                m_ResponseMappingPopup.Hide();
            }
        }

        IEnumerator RunDifficultPractice()
        {
            yield return RunOnePopup(m_DifficultPracticeIntroPanel);

            while (true)
            {
                int errors = 0;

                yield return RunFeedbackPracticeBlock(
                    composition: (
                        Asset.DifficultPracticeClearHits,
                        Asset.DifficultPracticeClearMisses,
                        Asset.DifficultPracticeNearHits,
                        Asset.DifficultPracticeNearMisses
                    ),
                    onErrorCount: e => errors = e
                );

                if (errors < Asset.DifficultPracticeErrorThreshold)
                {
                    Debug.Log($"[HitOrMissAppController] Difficult practice PASSED — {errors} errors.");
                    break;
                }

                Debug.Log($"[HitOrMissAppController] Difficult practice FAILED — {errors} errors. Showing retry.");
                yield return RunOnePopup(m_PracticeRetryPanel);
            }
        }

        IEnumerator RunFeedbackPracticeBlock(
            (int clearHits, int clearMisses, int nearHits, int nearMisses) composition,
            System.Action<int> onErrorCount)
        {
            bool previousAutoResolveTimeouts = m_TaskManager.AutoResolveTimeouts;

            if (m_ResponseIndicator != null)
                m_ResponseIndicator.SetPracticeMode(true);

            bool? lastCorrect = null;

            void OnTrialJudged(TrialJudgement j)
            {
                lastCorrect = j.result == TrialResult.Correct;

                if (j.wasTooSlow && j.result != TrialResult.NoResponse)
                    StartCoroutine(FlashTooSlowPanel());
            }

            void OnResponseIndicator(SemanticCommand cmd, bool matched)
            {
                if (m_ResponseIndicator != null)
                    m_ResponseIndicator.Show(cmd, matched, lastCorrect);
            }

            int errors = 0;

            void TallyErrors(TrialJudgement j)
            {
                if (j.result == TrialResult.Incorrect || j.result == TrialResult.NoResponse)
                {
                    errors++;
                    Debug.Log($"[TALLY] error #{errors} — result={j.result} trial={j.trialId}");
                }
            }

            m_TooSlowHandler = _ => StartCoroutine(FlashTooSlowPanel());

            m_TaskManager.TrialJudged += OnTrialJudged;
            m_TaskManager.TrialJudged += TallyErrors;
            m_TaskManager.ResponseIndicator += OnResponseIndicator;
            m_TaskManager.TooSlow += m_TooSlowHandler;

            bool completedNormally = false;

            try
            {
                var trials = TrialGenerator.GeneratePracticeTrialsWithComposition(
                    Asset,
                    m_SessionMetadata.shoulderWidthCm,
                    composition.clearHits,
                    composition.clearMisses,
                    composition.nearHits,
                    composition.nearMisses
                );

                Debug.Log(
                    $"[HitOrMissAppController] Starting difficult practice. " +
                    $"trials.Length={(trials != null ? trials.Length : 0)}"
                );

                m_TaskManager.AutoResolveTimeouts = true;

                if (m_FixationCross != null)
                    m_FixationCross.Show();

                m_TaskManager.StartTrialList(-1, trials);

                while (m_TaskManager.IsRunning)
                    yield return null;

                completedNormally = true;
            }
            finally
            {
                if (m_FixationCross != null)
                    m_FixationCross.Hide();

                m_TaskManager.AutoResolveTimeouts = previousAutoResolveTimeouts;

                m_TaskManager.TrialJudged -= OnTrialJudged;
                m_TaskManager.TrialJudged -= TallyErrors;
                m_TaskManager.ResponseIndicator -= OnResponseIndicator;

                if (m_TooSlowHandler != null)
                    m_TaskManager.TooSlow -= m_TooSlowHandler;

                m_TooSlowHandler = null;

                if (m_ResponseIndicator != null)
                    m_ResponseIndicator.SetPracticeMode(false);
            }

            if (completedNormally)
                onErrorCount?.Invoke(errors);
        }

        // The manager raises this from Update(), so the work has to move into a
        // coroutine. The block is already paused when we get here and stays paused
        // until ResumeFromCheckIn().
        void OnCheckInDue(int trialsCompleted, int trialsInBlock)
        {
            StartCoroutine(RunCheckIn(trialsCompleted, trialsInBlock));
        }

        IEnumerator RunCheckIn(int trialsCompleted, int trialsInBlock)
        {
            if (m_CheckInPanel == null)
            {
                Debug.LogWarning("[HitOrMissAppController] Check-in due but no panel assigned. Continuing.");
                m_TaskManager.ResumeFromCheckIn();
                yield break;
            }

            m_CheckInPanel.Show(trialsCompleted, trialsInBlock, m_Language);

            // Deliberate dead time before input is armed. The participant pressed a
            // trigger a moment ago to answer trial N; without this the panel can be
            // dismissed by that same press before they have read it.
            if (m_CheckInArmDelaySeconds > 0f)
                yield return new WaitForSeconds(m_CheckInArmDelaySeconds);

            if (m_InputSource != null)
            {
                bool dismissed = false;
                void OnDismiss(ResponseEvent _) => dismissed = true;

                m_InputSource.ResponseReceived += OnDismiss;

                // PauseBlock disabled input on the way in. Re-enable it for the
                // dismiss press only; the manager ignores responses while paused,
                // so this cannot be scored as a trial.
                m_InputSource.Enable();

                try
                {
                    while (!dismissed)
                        yield return null;
                }
                finally
                {
                    m_InputSource.ResponseReceived -= OnDismiss;
                }
            }
            else
            {
                yield return new WaitForSeconds(3f);
            }

            m_CheckInPanel.Hide();
            m_TaskManager.ResumeFromCheckIn();
        }

        IEnumerator FlashTooSlowPanel()
        {
            if (m_TooSlowPanel == null)
                yield break;

            m_TooSlowPanel.SetText(BuildPopupContext().ResolveText(m_TooSlowPanel));
            m_TooSlowPanel.Show();

            yield return new WaitForSeconds(kTooSlowDisplaySeconds);

            m_TooSlowPanel.Hide();
        }

        IEnumerator RunPopupSequence(TaskPopupPanel[] sequence)
        {
            if (sequence == null)
                yield break;

            foreach (var panel in sequence)
            {
                if (panel == null)
                    continue;

                yield return RunOnePopup(panel);
            }
        }

        PopupContext BuildPopupContext()
        {
            return new PopupContext
            {
                Localize = (key, fallback) => GetLocalizedString(key, fallback),
                GetBreakDuration = () => Asset != null ? Asset.BreakDurationSeconds : 60f,
                GetOutroDuration = () => Asset != null ? Asset.OutroDuration : 10f,
                CurrentBlockNumber = CurrentBlockIndex + 1,
            };
        }

        IEnumerator RunOnePopup(TaskPopupPanel panel)
        {
            if (panel == null)
                yield break;

            yield return panel.Run(BuildPopupContext());
        }

        IEnumerator RunPrePracticeSequence()
        {
            if (m_PrePracticePopups == null)
                yield break;

            for (int i = 0; i < m_PrePracticePopups.Length; i++)
            {
                var panel = m_PrePracticePopups[i];

                if (panel == null)
                    continue;

                yield return RunOnePopup(panel);
            }
        }

        void EndSession()
        {
            if (m_TaskManager != null)
            {
                m_TaskManager.CheckInDue -= OnCheckInDue;

                if (m_TaskLogger != null)
                    m_TaskManager.TrialJudged -= m_TaskLogger.LogTrial;

                if (m_TooSlowHandler != null)
                {
                    m_TaskManager.TooSlow -= m_TooSlowHandler;
                    m_TooSlowHandler = null;
                }

                m_TaskManager.AutoResolveTimeouts = false;
            }

            if (m_TaskLogger != null)
                m_TaskLogger.EndSession();

            HideAllPopups();

            if (m_FixationCross != null)
                m_FixationCross.Hide();

            if (m_ResponseIndicator != null)
                m_ResponseIndicator.SetPracticeMode(false);

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.ExitTaskMode();

            m_EegMarkerEmitter?.EndSession();

            // Release this session's protocol clone; the manager goes back to
            // pointing at the source asset so no destroyed reference lingers.
            if (m_RuntimeProtocol != null)
            {
                if (m_TaskManager != null)
                    m_TaskManager.TaskAsset = Protocol;
                Destroy(m_RuntimeProtocol);
                m_RuntimeProtocol = null;
            }

            SetPhase(TaskPhase.Idle);
            m_SessionCoroutine = null;
            SessionEnded?.Invoke();
        }

        void HideAllPopups()
        {
            HideArray(m_PrePracticePopups);
            HideArray(m_ExtraPostPracticePopups);

            if (m_ControllerPracticeIntroPanel != null) m_ControllerPracticeIntroPanel.Hide();
            if (m_TriggerDemoPopup != null) m_TriggerDemoPopup.Hide();
            if (m_ResponseMappingPopup != null) m_ResponseMappingPopup.Hide();
            if (m_DifficultPracticeIntroPanel != null) m_DifficultPracticeIntroPanel.Hide();
            if (m_PracticeRetryPanel != null) m_PracticeRetryPanel.Hide();
            if (m_TooSlowPanel != null) m_TooSlowPanel.Hide();
            if (m_NoFeedbackPanel != null) m_NoFeedbackPanel.Hide();
            if (m_ReadyToStartPanel != null) m_ReadyToStartPanel.Hide();
            if (m_BlockIntroPopup != null) m_BlockIntroPopup.Hide();
            if (m_BreakPopup != null) m_BreakPopup.Hide();
            if (m_BlockReadyPopup != null) m_BlockReadyPopup.Hide();
            if (m_OutroPopup != null) m_OutroPopup.Hide();
            if (m_WelcomePanel != null) m_WelcomePanel.SetActive(false);
            if (m_CheckInPanel != null) m_CheckInPanel.Hide();
        }

        static void HideArray(TaskPopupPanel[] arr)
        {
            if (arr == null)
                return;

            foreach (var p in arr)
            {
                if (p != null)
                    p.Hide();
            }
        }

        string GetLocalizedString(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || m_TermTable == null)
                return fallback;

            string v = m_TermTable.Get(key, m_Language);

            return string.IsNullOrEmpty(v) || (v.StartsWith("[") && v.EndsWith("]"))
                ? fallback
                : v;
        }
    }
}