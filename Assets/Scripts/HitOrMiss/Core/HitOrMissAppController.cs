using System.Collections;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Top-level orchestrator for the Hit-or-Miss assessment (Task 2).
    ///
    /// Full session flow:
    ///
    ///   PrePracticePopups[]                 — Welcome, TriggerCheck, Positioning, etc.
    ///   ↓
    ///   ControllerPracticeIntroPanel        — "LEFT = YES, RIGHT = NO. Let's practice."
    ///   LeftControllerPracticePanel         — wait for LEFT trigger → GiantSquare blue → auto-advance
    ///   RightControllerPracticePanel        — wait for RIGHT trigger → GiantSquare orange → auto-advance
    ///   ↓
    ///   BallDemoIntroPanel                  — "Watch two examples."
    ///   BallDemoSequence (PASSIVE)          — [clear_hit, clear_miss]; no input accepted
    ///   ↓
    ///   EasyPracticeIntroPanel              — "Now your turn — easy ones first."
    ///   EasyPracticeTrials                  — 4 trials (2 clear_hit, 2 clear_miss) with green/red
    ///                                          + TooSlow panel on no-response; require-response-to-advance.
    ///                                          ≥2 errors → repeat silently
    ///   ↓
    ///   DifficultPracticeIntroPanel         — "Now slightly harder."
    ///   DifficultPracticeTrials             — 10 trials (2/2/3/3) with green/red + TooSlow.
    ///                                          ≥4 errors → PracticeRetryPanel → repeat difficult
    ///   ↓
    ///   NoFeedbackPanel                     — "Real task starts. No more feedback."
    ///   ReadyToStartPanel                   — "Ready? Press Start."
    ///   ↓  for each block:
    ///       BlockIntroPopup                 — "Block X / N. Press Begin."
    ///       block trials                    — no feedback (neutral indicator)
    ///       (if not last block:)
    ///         BreakPopup                    — auto-advances after BreakDurationSeconds
    ///         BlockReadyPopup               — "Ready for next block?"
    ///   ↓
    ///   OutroPopup
    ///
    /// Cross-cutting:
    ///   • Forced-response panels (5, 6) bypass the panel Behavior dropdown.
    ///   • Practice phases enable ResponseIndicator.SetPracticeMode(true) for
    ///     green/red feedback; turned off again before main blocks start.
    ///   • TooSlowPanel is fired by TaskManager.TooSlow event during practice
    ///     phases only.
    /// </summary>
    public class HitOrMissAppController : MonoBehaviour
    {
        // ---- Practice block configuration (hardcoded constants) ----
        const int kBallDemoTrialCount              = 2;
        const float kTooSlowDisplaySeconds         = 1.0f;

        [Header("Task")]
        [SerializeField] TrajectoryTaskAsset m_TaskAsset;
        [SerializeField] TrajectoryTaskManager m_TaskManager;

        [Header("Input")]
        [SerializeField] ControllerButtonInput m_ControllerInput;
        [SerializeField] KeyboardCommandInput m_KeyboardInput;
        [SerializeField] HandPinchInput m_HandPinchInput;
        [SerializeField] InputMode m_InputMode = InputMode.Controller;

        [Header("Logging")]
        [SerializeField] TaskLogger m_TaskLogger;
        [SerializeField] EegMarkerEmitter m_EegMarkerEmitter;

        [Header("Localization")]
        [SerializeField] LocalizedTermTable m_TermTable;
        [SerializeField] LocalizedUITextBinder[] m_UITextBinders;


        // ---- Pre-practice ----
        [Header("Pre-practice popups (Welcome → TriggerCheck → Positioning, etc.)")]
        [SerializeField] TaskPopupPanel[] m_PrePracticePopups;
        [Tooltip("Index into m_PrePracticePopups[] of the positioning popup. -1 = no positioning step. The standing cross is visible only while that popup is up.")]
        [SerializeField] int m_PositioningPopupIndex = -1;

        
        // ---- Positioning ----
        [Header("Fixation acknowledgement (shown after positioning)")]
        [Tooltip("Shown after positioning. Displays the fixation cross and waits for a trigger press to confirm the subject sees it.")]
        [SerializeField] TaskPopupPanel m_FixationAckPanel;

      

        // ---- Controller practice ----
        [Header("Controller practice (forced-response sequence)")]
        [SerializeField] TaskPopupPanel m_ControllerPracticeIntroPanel;
        [Tooltip("Waits for LEFT trigger; GiantSquare turns blue; auto-advances. Behavior dropdown is ignored.")]
        [SerializeField] TaskPopupPanel m_LeftControllerPracticePanel;
        [Tooltip("Waits for RIGHT trigger; GiantSquare turns orange; auto-advances. Behavior dropdown is ignored.")]
        [SerializeField] TaskPopupPanel m_RightControllerPracticePanel;

        [Header("Forced-response visual feedback")]
        [SerializeField] Color m_ForcedIdleColor      = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] Color m_ForcedLeftFillColor  = new Color(0.20f, 0.45f, 1.00f, 1f);
        [SerializeField] Color m_ForcedRightFillColor = new Color(1.00f, 0.55f, 0.10f, 1f);
        [SerializeField] float m_ForcedFlashSeconds = 0.5f;

        // ---- Ball demo (passive) ----
        [Header("Ball demo (2 passive trials)")]
        [Tooltip("Intro panel before the 2 passive demo balls.")]
        [SerializeField] TaskPopupPanel m_BallDemoIntroPanel;

        // ---- Active practice ----
        [Header("Easy practice (4 trials: 2 clear_hit, 2 clear_miss)")]
        [SerializeField] TaskPopupPanel m_EasyPracticeIntroPanel;

        [Header("Difficult practice (10 trials: 2/2/3/3)")]
        [SerializeField] TaskPopupPanel m_DifficultPracticeIntroPanel;
        [Tooltip("Shown only if participant fails the difficult block (≥4 errors). Easy failures repeat silently.")]
        [SerializeField] TaskPopupPanel m_PracticeRetryPanel;

        [Header("Shared practice feedback")]
        [Tooltip("Briefly shown on no-response trials (TaskManager.TooSlow event). Auto-hides.")]
        [SerializeField] TaskPopupPanel m_TooSlowPanel;
        [Tooltip("Visual feedback indicator. Practice mode → green/red. Main task → neutral.")]
        [SerializeField] ResponseIndicator m_ResponseIndicator;
        [Tooltip("Fullscreen green/red flash, fired on every trial judgement during practice. Leave empty to disable.")]
        [SerializeField] FullScreenFlash m_FullScreenFlash;

        // ---- Post-practice + main task ----
        [Header("Post-practice (before block 1)")]
        [SerializeField] TaskPopupPanel m_NoFeedbackPanel;
        [SerializeField] TaskPopupPanel m_ReadyToStartPanel;
        [Tooltip("Optional extra panels between Ready and block 1.")]
        [SerializeField] TaskPopupPanel[] m_ExtraPostPracticePopups;

        [Header("Per-block popups")]
        [SerializeField] TaskPopupPanel m_BlockIntroPopup;
        [SerializeField] TaskPopupPanel m_BreakPopup;
        [Tooltip("Shown after the break, before the next BlockIntroPopup. 'Ready for next block?' style.")]
        [SerializeField] TaskPopupPanel m_BlockReadyPopup;

        [Header("End")]
        [SerializeField] TaskPopupPanel m_OutroPopup;

        // ---- Visuals + Clinician ----
        [Header("Visuals")]
        [SerializeField] FixationCrossController m_FixationCross;
        [SerializeField] StandingCross m_StandingCross;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        // ---- Runtime state ----
        SupportedLanguage m_Language = SupportedLanguage.English;
        TaskPhase m_CurrentPhase = TaskPhase.Idle;
        Coroutine m_SessionCoroutine;
        IResponseInputSource m_InputSource;

        SessionMetadata m_SessionMetadata;
        bool m_SessionMetadataExplicitlySet;
        TrajectoryTaskAsset m_SessionAsset;

        // Practice-only TooSlow handler (added/removed around practice phases).
        System.Action<TrialDefinition> m_TooSlowHandler;

        public TaskPhase CurrentPhase => m_CurrentPhase;
        public TrajectoryTaskManager TaskManager => m_TaskManager;
        public string ParticipantId { get; set; } = "P000";
        public int CurrentBlockIndex { get; private set; }
        public bool IsPaused => m_TaskManager != null && m_TaskManager.IsPaused;
        public SupportedLanguage CurrentLanguage => m_Language;

        public event System.Action SessionPaused;
        public event System.Action SessionResumed;
        public event System.Action<TaskPhase> PhaseChanged;
        public event System.Action SessionStarted;
        public event System.Action SessionEnded;

        // ====================================================================
        // Lifecycle
        // ====================================================================

        void SetPhase(TaskPhase phase)
        {
            if (m_CurrentPhase == phase) return;
            m_CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        public void SetSessionMetadata(SessionMetadata metadata)
        {
            m_SessionMetadata = metadata;
            m_SessionMetadataExplicitlySet = true;
            if (!string.IsNullOrEmpty(metadata.participantId))
                ParticipantId = metadata.participantId;
        }

        void Awake()
        {
            if (m_TaskManager != null)
            {
                m_TaskManager.TaskAsset = m_TaskAsset;
                m_TaskManager.SetMarkerEmitter(m_EegMarkerEmitter);
            }
            HideAllPopups();
            if (m_FixationCross != null) m_FixationCross.Hide();
        }

        public void SetLanguage(SupportedLanguage language)
        {
            m_Language = language;
            if (m_UITextBinders == null) return;
            foreach (var binder in m_UITextBinders)
                if (binder != null) binder.Language = language;
        }
        

        public void StartSession()
        {
            if (m_CurrentPhase != TaskPhase.Idle)
            {
                Debug.LogWarning("[HitOrMissAppController] Session already running.");
                return;
            }

            var composite = new CompositeInputSource(m_ControllerInput, m_KeyboardInput, m_HandPinchInput);
            if (composite.Sources.Count == 0)
            {
                Debug.LogError("[HitOrMissAppController] No input sources assigned in inspector.");
                return;
            }
            m_InputSource = composite;
            m_TaskManager.SetInputSource(m_InputSource);

            if (!m_SessionMetadataExplicitlySet)
                m_SessionMetadata = SessionMetadata.CreateDefault(ParticipantId);
            else
                m_SessionMetadata.participantId = ParticipantId;

            m_SessionMetadata.PopulateFromTaskAsset(m_TaskAsset);
            if (string.IsNullOrEmpty(m_SessionMetadata.sessionId))
                m_SessionMetadata.sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (string.IsNullOrEmpty(m_SessionMetadata.sessionDate))
                m_SessionMetadata.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd");

            if (m_TaskAsset != null)
            {
                m_SessionAsset = m_TaskAsset.CreateSessionClone();
                m_SessionAsset.ApplyTask2SessionOverrides(m_SessionMetadata);
                m_TaskManager.TaskAsset = m_SessionAsset;
                Debug.Log($"[HitOrMissAppController] Session asset clone applied. " +
                          $"BlockCount={m_SessionAsset.BlockCount}, " +
                          $"TrialsPerBlock={m_SessionAsset.TrialsPerBlock}, " +
                          $"BreakDurationSeconds={m_SessionAsset.BreakDurationSeconds}");
            }

            if (m_TaskLogger != null)
            {
                m_TaskLogger.ParticipantId = ParticipantId;
                m_TaskLogger.SetMetadata(m_SessionMetadata);
                m_TaskLogger.BeginSession(TaskKind.Task2HitOrMiss, m_TaskAsset != null ? m_TaskAsset.TaskName : "HitOrMiss");
                m_TaskManager.TrialJudged += m_TaskLogger.LogTrial;
            }

            if (m_EegMarkerEmitter != null)
            {
                string sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                m_EegMarkerEmitter.BeginSession(sessionId);
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
            if (m_CurrentPhase == TaskPhase.Idle) return;
            if (m_TaskManager == null || !m_TaskManager.IsRunning || m_TaskManager.IsPaused) return;

            m_TaskManager.PauseBlock();
            m_EegMarkerEmitter?.Emit("session_paused");
            if (m_TaskLogger != null)
                m_TaskLogger.Flush(CurrentBlockIndex, m_TaskManager.NextTrialIndex);
            if (m_ClinicianPanel != null) m_ClinicianPanel.EnterPausedMode();
            SessionPaused?.Invoke();
        }

        public void ResumeSession()
        {
            if (m_TaskManager == null || !m_TaskManager.IsPaused) return;
            m_TaskManager.ResumeBlock();
            m_EegMarkerEmitter?.Emit("session_resumed");
            if (m_ClinicianPanel != null) m_ClinicianPanel.ExitPausedMode();
            SessionResumed?.Invoke();
        }

        // ====================================================================
        // Session coroutine
        // ====================================================================

        TrajectoryTaskAsset Asset => m_SessionAsset != null ? m_SessionAsset : m_TaskAsset;

        IEnumerator RunSession()
        {
            int blockCount = Asset != null ? Asset.BlockCount : 3;

            SetPhase(TaskPhase.Intro);
            m_EegMarkerEmitter?.Emit("phase_intro");
            yield return RunPrePracticeSequence();

            yield return RunFixationAcknowledgement();  

            SetPhase(TaskPhase.Practice);
            m_EegMarkerEmitter?.Emit("phase_controller_practice");
            yield return RunControllerPractice();

            m_EegMarkerEmitter?.Emit("phase_ball_demo");
            yield return RunBallDemo();

            m_EegMarkerEmitter?.Emit("phase_easy_practice");
            yield return RunEasyPractice();

            m_EegMarkerEmitter?.Emit("phase_difficult_practice");
            yield return RunDifficultPractice();
            Debug.Log("[SESSION] difficult done, showing NoFeedback");


            SetPhase(TaskPhase.Ready);
            yield return RunOnePopup(m_NoFeedbackPanel);
            Debug.Log("[SESSION] NoFeedback done, showing ReadyToStart");

            yield return RunOnePopup(m_ReadyToStartPanel);
            Debug.Log("[SESSION] ReadyToStart done, entering blocks");

            yield return RunPopupSequence(m_ExtraPostPracticePopups);
            Debug.Log($"[SESSION] blockCount={blockCount}, starting block loop");


            for (int b = 0; b < blockCount; b++)
            {
                CurrentBlockIndex = b;

                SetPhase(TaskPhase.BlockIntro);
                yield return RunOnePopup(m_BlockIntroPopup);

                SetPhase(TaskPhase.Block);
                m_EegMarkerEmitter?.Emit("phase_block");
                if (m_FixationCross != null) m_FixationCross.Show();

                var blockTrials = Asset.GenerateBlock(b, m_SessionMetadata.shoulderWidthCm);
                m_TaskManager.StartTrialList(b, blockTrials);
                while (m_TaskManager.IsRunning) yield return null;

                if (m_FixationCross != null) m_FixationCross.Hide();

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

        // ====================================================================
        // Practice phases
        // ====================================================================

        IEnumerator RunControllerPractice()
        {
            yield return RunOnePopup(m_ControllerPracticeIntroPanel);
            yield return ForcedResponsePopup(m_LeftControllerPracticePanel,  SemanticCommand.Hit,  m_ForcedLeftFillColor);
            yield return ForcedResponsePopup(m_RightControllerPracticePanel, SemanticCommand.Miss, m_ForcedRightFillColor);
        }

        IEnumerator RunBallDemo()
        {
            yield return RunOnePopup(m_BallDemoIntroPanel);

            var demoTrials = TrialGenerator.GenerateBallDemoTrials(Asset, m_SessionMetadata.shoulderWidthCm);

            if (m_FixationCross != null) m_FixationCross.Show();
            // passive=true → manager ignores all input on these trials.
            m_TaskManager.StartTrialList(-1, demoTrials, passive: true);
            while (m_TaskManager.IsRunning) yield return null;
            if (m_FixationCross != null) m_FixationCross.Hide();
        }

        IEnumerator RunEasyPractice()
        {
            yield return RunOnePopup(m_EasyPracticeIntroPanel);

            while (true)
            {
                int errors = 0;
                yield return RunFeedbackPracticeBlock(
                    composition: (Asset.EasyPracticeClearHits, Asset.EasyPracticeClearMisses,
                                Asset.EasyPracticeNearHits,  Asset.EasyPracticeNearMisses),
                    onErrorCount: e => errors = e);

                if (errors < Asset.EasyPracticeErrorThreshold)
                    break;

                Debug.Log($"[HitOrMissAppController] Easy practice failed " +
                        $"({errors} errors ≥ {Asset.EasyPracticeErrorThreshold}). Repeating silently.");
            }
        }

        IEnumerator RunDifficultPractice()
        {
            yield return RunOnePopup(m_DifficultPracticeIntroPanel);

            while (true)
            {
                int errors = 0;
                yield return RunFeedbackPracticeBlock(
                    composition: (Asset.DifficultPracticeClearHits, Asset.DifficultPracticeClearMisses,
                                Asset.DifficultPracticeNearHits,  Asset.DifficultPracticeNearMisses),
                    onErrorCount: e => errors = e);

                if (errors < Asset.DifficultPracticeErrorThreshold)
                {
                    Debug.Log($"[HitOrMissAppController] Difficult PASSED — {errors} errors.");
                    break;
                }

                Debug.Log($"[HitOrMissAppController] Difficult FAILED — {errors} errors. Showing retry.");
                yield return RunOnePopup(m_PracticeRetryPanel);
            }
        }

        /// <summary>
        /// Runs one practice block with green/red feedback and a TooSlowPanel
        /// on NoResponse. Tallies errors (Incorrect + NoResponse) and reports
        /// the count via <paramref name="onErrorCount"/>.
        /// </summary>
        IEnumerator RunFeedbackPracticeBlock(
            (int clearHits, int clearMisses, int nearHits, int nearMisses) composition,
            System.Action<int> onErrorCount)
        {
            // Practice-only: green/red feedback and TooSlow popup.
            if (m_ResponseIndicator != null)
            {
                m_ResponseIndicator.SetPracticeMode(true);
                // Bridge the manager's (cmd, matched) event to the indicator's
                // (cmd, matched, wasCorrect?) overload using the just-judged
                // trial. We use a closure variable updated by TrialJudged.
            }

            bool? lastCorrect = null;
            void OnTrialJudged(TrialJudgement j)
                        {
                            lastCorrect = j.result == TrialResult.Correct;

                            // Green/red flash based on correctness (unchanged).
                            if (m_FullScreenFlash != null)
                            {
                                if (j.result == TrialResult.Correct)
                                    m_FullScreenFlash.FlashCorrect();
                                else
                                    m_FullScreenFlash.FlashIncorrect();
                            }

                            // ALSO show the Too Slow panel if the response was past halfway.
                            // (A no-response timeout already fires the TooSlow event separately.)
                            if (j.wasTooSlow && j.result != TrialResult.NoResponse)
                                StartCoroutine(FlashTooSlowPanel());
                        }
            m_TaskManager.TrialJudged += OnTrialJudged;

            void OnResponseIndicator(SemanticCommand cmd, bool matched)
            {
                if (m_ResponseIndicator != null)
                    m_ResponseIndicator.Show(cmd, matched, lastCorrect);
            }
            m_TaskManager.ResponseIndicator += OnResponseIndicator;

            // Wire TooSlow only during practice.
            m_TooSlowHandler = _ => StartCoroutine(FlashTooSlowPanel());
            m_TaskManager.TooSlow += m_TooSlowHandler;

            int errors = 0;
            void TallyErrors(TrialJudgement j)
            {
                if (j.result == TrialResult.Incorrect || j.result == TrialResult.NoResponse)
                {
                    errors++;
                    Debug.Log($"[TALLY] error #{errors} — result={j.result} trial={j.trialId}");
                }
            }
            m_TaskManager.TrialJudged += TallyErrors;

            var trials = TrialGenerator.GeneratePracticeTrialsWithComposition(
                Asset, m_SessionMetadata.shoulderWidthCm,
                composition.clearHits, composition.clearMisses,
                composition.nearHits,  composition.nearMisses);
                
            m_TaskManager.AutoResolveTimeouts = true;   // practice: timeouts = errors, no waiting
            if (m_FixationCross != null) m_FixationCross.Show();
            m_TaskManager.StartTrialList(-1, trials);


            while (m_TaskManager.IsRunning) yield return null;
            if (m_FixationCross != null) m_FixationCross.Hide();
            
            m_TaskManager.AutoResolveTimeouts = false;   // restore for main task
            m_TaskManager.TrialJudged -= TallyErrors;
            m_TaskManager.TrialJudged -= OnTrialJudged;
            m_TaskManager.ResponseIndicator -= OnResponseIndicator;
            m_TaskManager.TooSlow -= m_TooSlowHandler;
            m_TooSlowHandler = null;
            if (m_ResponseIndicator != null)
                m_ResponseIndicator.SetPracticeMode(false);

            onErrorCount?.Invoke(errors);
        }

        IEnumerator FlashTooSlowPanel()
        {
            if (m_TooSlowPanel == null) yield break;
            m_TooSlowPanel.SetText(BuildPopupContext().ResolveText(m_TooSlowPanel));
            m_TooSlowPanel.Show();
            yield return new WaitForSeconds(kTooSlowDisplaySeconds);
            m_TooSlowPanel.Hide();
        }

        // ====================================================================
        // Forced-response popup (LEFT / RIGHT controller practice)
        // ====================================================================

        IEnumerator ForcedResponsePopup(TaskPopupPanel panel, SemanticCommand expected, Color fillColor)
        {
            if (panel == null)
            {
                Debug.LogWarning($"[HitOrMissAppController] Forced-response popup ({expected}) not assigned. Skipping.");
                yield break;
            }
            if (m_InputSource == null)
            {
                Debug.LogError("[HitOrMissAppController] No input source for forced-response popup.");
                yield break;
            }

            var ctx = BuildPopupContext();
            string text = ctx.ResolveText(panel);
            panel.SetText(text);
            panel.Show();

            var giantSquareButton = FindGiantSquareButton(panel);
            UnityEngine.UI.Image giantSquareImage = null;
            if (giantSquareButton != null)
            {
                giantSquareImage = giantSquareButton.GetComponent<UnityEngine.UI.Image>();
                giantSquareButton.onClick.RemoveAllListeners();
            }
            if (giantSquareImage != null)
                giantSquareImage.color = m_ForcedIdleColor;

            bool got = false;
            void Handler(ResponseEvent ev)
            {
                if (ev.command == expected) got = true;
            }
            m_InputSource.ResponseReceived += Handler;
            m_InputSource.Enable();

            while (!got) yield return null;

            m_InputSource.ResponseReceived -= Handler;

            if (giantSquareImage != null)
                giantSquareImage.color = fillColor;
            // Note: ResponseIndicator is NOT in practice mode here — the
            // forced-response panel is purely tutorial. We pass matched=true
            // so the neutral indicator just flashes the label.
            if (m_ResponseIndicator != null)
                m_ResponseIndicator.Show(expected, true);

            yield return new WaitForSeconds(m_ForcedFlashSeconds);
            panel.Hide();
        }

        static UnityEngine.UI.Button FindGiantSquareButton(TaskPopupPanel panel)
        {
            if (panel == null) return null;
            var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var b in buttons)
                if (b != null && b.name.Contains("GiantSquare")) return b;
            return null;
        }

        IEnumerator RunFixationAcknowledgement()
        {
            if (m_FixationAckPanel == null)
            {
                Debug.LogWarning("[HitOrMissAppController] Fixation acknowledgement panel not assigned. Skipping.");
                yield break;
            }
            if (m_InputSource == null)
            {
                Debug.LogError("[HitOrMissAppController] No input source for fixation acknowledgement.");
                yield break;
            }

            // Show the exact trial crosshair, at its real spawn-point position.
            if (m_TaskManager != null) m_TaskManager.ShowFixationCrosshair(true);

            var ctx = BuildPopupContext();
            m_FixationAckPanel.SetText(ctx.ResolveText(m_FixationAckPanel));
            m_FixationAckPanel.Show();

            // Any controller trigger (left or right) confirms.
            bool acknowledged = false;
            void Handler(ResponseEvent ev) => acknowledged = true;
            m_InputSource.ResponseReceived += Handler;
            m_InputSource.Enable();

            while (!acknowledged) yield return null;

            m_InputSource.ResponseReceived -= Handler;

            m_FixationAckPanel.Hide();
            if (m_TaskManager != null) m_TaskManager.ShowFixationCrosshair(false);
        }

        // ====================================================================
        // Popup sequencing helpers
        // ====================================================================

        IEnumerator RunPopupSequence(TaskPopupPanel[] sequence)
        {
            if (sequence == null) yield break;
            foreach (var panel in sequence)
            {
                if (panel == null) continue;
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
            if (panel == null) yield break;
            yield return panel.Run(BuildPopupContext());
        }

        IEnumerator RunPrePracticeSequence()
        {
            if (m_PrePracticePopups == null) yield break;
            for (int i = 0; i < m_PrePracticePopups.Length; i++)
            {
                var panel = m_PrePracticePopups[i];
                if (panel == null) continue;
                bool needsCross = (i == m_PositioningPopupIndex && m_StandingCross != null);
                if (needsCross) m_StandingCross.Show();
                yield return RunOnePopup(panel);
                if (needsCross) m_StandingCross.Hide();
            }
        }

        // ====================================================================
        // End / cleanup
        // ====================================================================

        void EndSession()
        {
            if (m_TaskLogger != null)
            {
                m_TaskManager.TrialJudged -= m_TaskLogger.LogTrial;
                m_TaskLogger.EndSession();
            }

            HideAllPopups();
            if (m_FixationCross != null) m_FixationCross.Hide();
            if (m_ClinicianPanel != null) m_ClinicianPanel.ExitTaskMode();

            m_EegMarkerEmitter?.EndSession();

            if (m_SessionAsset != null)
            {
                if (m_TaskManager != null) m_TaskManager.TaskAsset = m_TaskAsset;
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
            if (m_FixationAckPanel != null) m_FixationAckPanel.Hide();
            if (m_ControllerPracticeIntroPanel  != null) m_ControllerPracticeIntroPanel.Hide();
            if (m_LeftControllerPracticePanel   != null) m_LeftControllerPracticePanel.Hide();
            if (m_RightControllerPracticePanel  != null) m_RightControllerPracticePanel.Hide();
            if (m_BallDemoIntroPanel            != null) m_BallDemoIntroPanel.Hide();
            if (m_EasyPracticeIntroPanel        != null) m_EasyPracticeIntroPanel.Hide();
            if (m_DifficultPracticeIntroPanel   != null) m_DifficultPracticeIntroPanel.Hide();
            if (m_PracticeRetryPanel            != null) m_PracticeRetryPanel.Hide();
            if (m_TooSlowPanel                  != null) m_TooSlowPanel.Hide();
            if (m_NoFeedbackPanel               != null) m_NoFeedbackPanel.Hide();
            if (m_ReadyToStartPanel             != null) m_ReadyToStartPanel.Hide();
            if (m_BlockIntroPopup               != null) m_BlockIntroPopup.Hide();
            if (m_BreakPopup                    != null) m_BreakPopup.Hide();
            if (m_BlockReadyPopup               != null) m_BlockReadyPopup.Hide();
            if (m_OutroPopup                    != null) m_OutroPopup.Hide();
        }

        static void HideArray(TaskPopupPanel[] arr)
        {
            if (arr == null) return;
            foreach (var p in arr) if (p != null) p.Hide();
        }

        string GetLocalizedString(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || m_TermTable == null) return fallback;
            string v = m_TermTable.Get(key, m_Language);
            return string.IsNullOrEmpty(v) || (v.StartsWith("[") && v.EndsWith("]")) ? fallback : v;
        }
    }
}