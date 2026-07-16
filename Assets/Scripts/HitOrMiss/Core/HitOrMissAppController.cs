using System.Collections;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Top-level orchestrator for the Hit-or-Miss assessment (Task 2).
    /// </summary>
    public class HitOrMissAppController : MonoBehaviour
    {
        const float kTooSlowDisplaySeconds = 1.0f;

        [Header("Task")]
        [SerializeField] TrajectoryTaskAsset m_TaskAsset;
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

        [Header("End")]
        [SerializeField] TaskPopupPanel m_OutroPopup;

        [Header("Visuals")]
        [SerializeField] FixationCrossController m_FixationCross;
        [SerializeField] StandingCross m_StandingCross;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        // ------------------------------------------------------------------
        // Start mode (clinician-panel opt-out)
        // ------------------------------------------------------------------
        [Header("Start mode (clinician-panel opt-out)")]
        [Tooltip("If true, the session begins automatically when the scene loads. " +
                 "Use this to run without the clinician panel. If false, the session " +
                 "waits for StartSession() from the clinician panel, the network, or a button.")]
        [SerializeField] bool m_AutoStartOnPlay = false;

        [Tooltip("If true, the clinician panel is bypassed and the SessionMetadata entered " +
                 "below is used directly.\n\n" +
                 "IMPORTANT: participant id and session are NOT read from here when an " +
                 "EegMarkerEmitter is present. Type those into the EegMarkerEmitter instead; " +
                 "the emitter is the single source of truth for id/session so the EEG stream " +
                 "and the CSV always agree. Use this block for everything else: shoulder width, " +
                 "group, equipment flags, session type, notes.")]
        [SerializeField] bool m_UseInspectorMetadata = false;

        [Tooltip("Session metadata used when 'Use Inspector Metadata' is true. Ignored otherwise. " +
                 "Leave participantId and sessionId blank when an EegMarkerEmitter is in the scene; " +
                 "they are overwritten by the emitter before logging begins. Right-click the " +
                 "component header and choose 'Inspector metadata: fill with defaults' to seed " +
                 "this block with sensible values (including Task 2 block/trial fields from the asset).")]
        [SerializeField] SessionMetadata m_InspectorMetadata;

        SupportedLanguage m_Language = SupportedLanguage.English;
        TaskPhase m_CurrentPhase = TaskPhase.Idle;
        Coroutine m_SessionCoroutine;
        IResponseInputSource m_InputSource;

        SessionMetadata m_SessionMetadata;
        bool m_HasExternalSessionMetadata;
        TrajectoryTaskAsset m_SessionAsset;

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

        TrajectoryTaskAsset Asset => m_SessionAsset != null ? m_SessionAsset : m_TaskAsset;

        void Awake()
        {
            if (m_TaskManager != null)
            {
                m_TaskManager.TaskAsset = m_TaskAsset;
                m_TaskManager.SetMarkerEmitter(m_EegMarkerEmitter);
            }

            HideAllPopups();

            if (m_FixationCross != null)
                m_FixationCross.Hide();
        }

        IEnumerator Start()
        {
            // Give every other component's Awake a frame to finish wiring
            // before we potentially auto-start the whole session.
            yield return null;

            // Clinician-panel opt-out: seed the session metadata from the
            // Inspector block. SetSessionMetadata sets m_HasExternalSessionMetadata,
            // so StartSession() uses these values instead of building defaults.
            // Participant id / session are still superseded by the
            // EegMarkerEmitter inside StartSession() when one is present.
            if (m_UseInspectorMetadata)
            {
                SetSessionMetadata(m_InspectorMetadata);
                Debug.Log("[HitOrMissAppController] Clinician panel bypassed. Using Inspector metadata " +
                          $"(shoulderWidthCm={m_InspectorMetadata.shoulderWidthCm}). " +
                          "Participant id / session will come from the EegMarkerEmitter if present.");
            }

            if (m_AutoStartOnPlay)
            {
                Debug.Log("[HitOrMissAppController] AutoStartOnPlay=true — starting session immediately.");
                StartSession();
            }
            else
            {
                Debug.Log("[HitOrMissAppController] AutoStartOnPlay=false — waiting for StartSession() (clinician panel, network, or a button).");
            }
        }

        [ContextMenu("Inspector metadata: fill with defaults")]
        void FillInspectorMetadataWithDefaults()
        {
            string keepId = string.IsNullOrEmpty(m_InspectorMetadata.participantId)
                ? "P000" : m_InspectorMetadata.participantId;

            m_InspectorMetadata = SessionMetadata.CreateDefault(keepId);

            // Pull the Task 2 block/trial/break fields off the asset so the
            // override block is non-zero even if ApplyTask2SessionOverrides
            // copies values unconditionally.
            if (m_TaskAsset != null)
                m_InspectorMetadata.PopulateFromTaskAsset(m_TaskAsset);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
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

            if (m_TaskAsset == null)
            {
                Debug.LogError("[HitOrMissAppController] No TrajectoryTaskAsset assigned.");
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

            bool externalMetadataProvided = m_HasExternalSessionMetadata;

            if (!externalMetadataProvided)
            {
                string defaultParticipantId =
                    m_EegMarkerEmitter != null && !string.IsNullOrEmpty(m_EegMarkerEmitter.ParticipantId)
                        ? m_EegMarkerEmitter.ParticipantId
                        : ParticipantId;

                m_SessionMetadata = SessionMetadata.CreateDefault(defaultParticipantId);
                m_SessionMetadata.PopulateFromTaskAsset(m_TaskAsset);

                Debug.Log("[HitOrMissAppController] Created default session metadata from task asset.");
            }
            else
            {
                Debug.Log("[HitOrMissAppController] Using externally provided session metadata.");
            }

            if (string.IsNullOrEmpty(m_SessionMetadata.participantId))
                m_SessionMetadata.participantId = ParticipantId;
            else
                ParticipantId = m_SessionMetadata.participantId;

            if (string.IsNullOrEmpty(m_SessionMetadata.sessionDate))
                m_SessionMetadata.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd");

            Debug.Log(
                $"[HitOrMissAppController] Metadata before task clone: " +
                $"participantId={m_SessionMetadata.participantId}, " +
                $"task2NumberOfBlocks={m_SessionMetadata.task2NumberOfBlocks}, " +
                $"task2TrialsPerBlock={m_SessionMetadata.task2TrialsPerBlock}, " +
                $"task2BreakDurationSeconds={m_SessionMetadata.task2BreakDurationSeconds}, " +
                $"shoulderWidthCm={m_SessionMetadata.shoulderWidthCm}"
            );

            m_SessionAsset = m_TaskAsset.CreateSessionClone();
            m_SessionAsset.ApplyTask2SessionOverrides(m_SessionMetadata);
            m_TaskManager.TaskAsset = m_SessionAsset;

            Debug.Log(
                $"[HitOrMissAppController] Session asset clone applied. " +
                $"BlockCount={m_SessionAsset.BlockCount}, " +
                $"TrialsPerBlock={m_SessionAsset.TrialsPerBlock}, " +
                $"BreakDurationSeconds={m_SessionAsset.BreakDurationSeconds}"
            );

            // Participant id + session have ONE authority: the EegMarkerEmitter
            // when it is present. This keeps the EEG marker stream and the CSV /
            // setup.json in agreement, and means you only type the id/session in
            // one place (the emitter). The Inspector metadata block supplies
            // everything else; its participantId / sessionId stand only when no
            // emitter is in the scene.
            if (m_EegMarkerEmitter != null)
            {
                m_EegMarkerEmitter.BeginSession();

                m_SessionMetadata.participantId = m_EegMarkerEmitter.ParticipantId;
                m_SessionMetadata.sessionId = m_EegMarkerEmitter.SessionId;
                ParticipantId = m_SessionMetadata.participantId;
            }

            if (m_TaskLogger != null)
            {
                m_TaskLogger.SetMetadata(m_SessionMetadata);

                m_TaskLogger.BeginSession(
                    TaskKind.Task2HitOrMiss,
                    m_SessionAsset != null ? m_SessionAsset.TaskName : m_TaskAsset.TaskName
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

            void Handler(ResponseEvent ev)
            {
                if (ev.command == SemanticCommand.Hit)
                {
                    leftPressed = true;
                    m_ControllerIntroDemo.LeftTriggerPressed();
                }
                else if (ev.command == SemanticCommand.Miss)
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

            void Handler(ResponseEvent ev)
            {
                if (ev.command == SemanticCommand.Hit)
                {
                    leftPressed = true;
                    m_ResponseMappingDemo.LeftPressed();
                }
                else if (ev.command == SemanticCommand.Miss)
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

            if (m_SessionAsset != null)
            {
                if (m_TaskManager != null)
                    m_TaskManager.TaskAsset = m_TaskAsset;

                Destroy(m_SessionAsset);
                m_SessionAsset = null;
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