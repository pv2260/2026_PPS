using System.Collections;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Top-level orchestrator for the Hit-or-Miss assessment.
    ///
    /// Session structure (each phase is an editable array of TaskPopupPanel):
    ///
    ///   PrePracticePopups[]    — Welcome, TriggerCheck, Instructions, Positioning, etc.
    ///   ↓
    ///   ForceLeftPopup         — practice popup that requires a LEFT trigger press
    ///   ForceRightPopup        — practice popup that requires a RIGHT trigger press
    ///   PracticeTrialsPopups[] — any number of intro popups, then practice trials run
    ///   ↓
    ///   PostPracticePopups[]   — NoFeedback, ReadyToStart, etc.
    ///   ↓  for each block:
    ///       BlockIntroPopup
    ///       block trials (no feedback)
    ///       (if not last block:)
    ///         BreakPopup       — auto-advances after BreakDurationSeconds
    ///         BlockReadyPopup  — optional "ready for next?" continue-style
    ///   ↓
    ///   OutroPopup
    ///
    /// Adding or reordering popups is done in the Inspector — drop new
    /// TaskPopupPanel GameObjects into the relevant array. No code change.
    /// Each panel carries its own localization key + fallback text + behavior
    /// (WaitForButton / AutoAdvance / BreakWithCountdown).
    /// </summary>
    public class HitOrMissAppController : MonoBehaviour
    {
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

        [Header("Pre-practice popups (run in order before the practice phase)")]
        [Tooltip("Add panels in the order you want them shown. Welcome, Trigger Check, Instructions, Positioning, etc. Each panel carries its own localization key + behavior.")]
        [SerializeField] TaskPopupPanel[] m_PrePracticePopups;

        [Header("Practice phase (forced-response popups)")]
        [Tooltip("Practice popup that requires the participant to press LEFT (HIT). Body key set on the panel itself.")]
        [SerializeField] TaskPopupPanel m_ForceLeftPopup;
        [Tooltip("Practice popup that requires the participant to press RIGHT (MISS).")]
        [SerializeField] TaskPopupPanel m_ForceRightPopup;
        [Tooltip("Popups shown before the practice trials. Last one auto-advances; trials then run with HIT/MISS feedback.")]
        [SerializeField] TaskPopupPanel[] m_PracticeTrialPopups;

        [Header("Post-practice popups (run after practice trials, before block 1)")]
        [SerializeField] TaskPopupPanel[] m_PostPracticePopups;

        [Header("Per-block popups")]
        [Tooltip("Shown before each block. {block} is replaced with the 1-based block number.")]
        [SerializeField] TaskPopupPanel m_BlockIntroPopup;
        [Tooltip("Shown between blocks. Should have BreakWithCountdown behavior, DurationSource = TaskAssetBreak.")]
        [SerializeField] TaskPopupPanel m_BreakPopup;
        [Tooltip("Optional: shown after the break, before the next block starts.")]
        [SerializeField] TaskPopupPanel m_BlockReadyPopup;

        [Header("End")]
        [SerializeField] TaskPopupPanel m_OutroPopup;

        [Header("Visuals")]
        [Tooltip("Optional fixation cross shown between trials. If left empty the manager's crosshair is used as-is.")]
        [SerializeField] FixationCrossController m_FixationCross;
        [Tooltip("Optional AR positioning marker. If wired, the controller toggles it on while m_PositioningPopupIndex of m_PrePracticePopups[] is showing.")]
        [SerializeField] StandingCross m_StandingCross;
        [Tooltip("Index into m_PrePracticePopups[] of the positioning popup. -1 = no positioning step. The standing cross is visible only while that popup is up.")]
        [SerializeField] int m_PositioningPopupIndex = -1;

        [Header("Participant Feedback")]
        [Tooltip("HIT/MISS visual flash. Wired only during the practice phase per the spec (no feedback during the main task).")]
        [SerializeField] ResponseIndicator m_ResponseIndicator;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        SupportedLanguage m_Language = SupportedLanguage.English;
        TaskPhase m_CurrentPhase = TaskPhase.Idle;
        Coroutine m_SessionCoroutine;
        IResponseInputSource m_InputSource;

        SessionMetadata m_SessionMetadata;
        bool m_SessionMetadataExplicitlySet;

        // Session-local asset clone. Created from the inspector-assigned
        // m_TaskAsset at session start; clinician-form overrides
        // (task2_parameters) are applied here so the on-disk asset isn't
        // mutated. Cleared on session end.
        TrajectoryTaskAsset m_SessionAsset;

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

            // Build session metadata first; offset/blocks/etc. come from the
            // clinician form into m_SessionMetadata.task2_parameters.
            if (!m_SessionMetadataExplicitlySet)
                m_SessionMetadata = SessionMetadata.CreateDefault(ParticipantId);
            else
                m_SessionMetadata.participantId = ParticipantId;

            // Snapshot the *original* task-asset values into setup.json,
            // THEN clone the asset and apply the overrides. The setup.json
            // reflects what was requested; the runtime uses the override.
            m_SessionMetadata.PopulateFromTaskAsset(m_TaskAsset);
            if (string.IsNullOrEmpty(m_SessionMetadata.sessionId))
                m_SessionMetadata.sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (string.IsNullOrEmpty(m_SessionMetadata.sessionDate))
                m_SessionMetadata.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd");

            // Session-local clone — overrides applied here do NOT touch the
            // on-disk asset. Cleared in EndSession.
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
                m_TaskLogger.BeginSession(m_TaskAsset != null ? m_TaskAsset.TaskName : "HitOrMiss");
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
            m_EegMarkerEmitter?.Emit("session_paused", "", CurrentBlockIndex.ToString());
            if (m_TaskLogger != null)
                m_TaskLogger.Flush(CurrentBlockIndex, m_TaskManager.NextTrialIndex);
            if (m_ClinicianPanel != null) m_ClinicianPanel.EnterPausedMode();
            SessionPaused?.Invoke();
        }

        public void ResumeSession()
        {
            if (m_TaskManager == null || !m_TaskManager.IsPaused) return;
            m_TaskManager.ResumeBlock();
            m_EegMarkerEmitter?.Emit("session_resumed", "", CurrentBlockIndex.ToString());
            if (m_ClinicianPanel != null) m_ClinicianPanel.ExitPausedMode();
            SessionResumed?.Invoke();
        }

        // ====================================================================
        // Session coroutine — array-driven, no burned-in panel references.
        // ====================================================================

        // Convenience: returns the live session asset (the clone with
        // overrides applied), falling back to the editor-assigned source if
        // we're somehow running without a clone.
        TrajectoryTaskAsset Asset => m_SessionAsset != null ? m_SessionAsset : m_TaskAsset;

        IEnumerator RunSession()
        {
            int blockCount = Asset != null ? Asset.BlockCount : 3;

            // ---- Pre-practice ----
            SetPhase(TaskPhase.Intro);
            m_EegMarkerEmitter?.Emit("phase_intro");
            yield return RunPrePracticeSequence();

            // ---- Practice ----
            SetPhase(TaskPhase.Practice);
            m_EegMarkerEmitter?.Emit("phase_practice");
            yield return RunPracticePhase();

            // ---- Post-practice ----
            SetPhase(TaskPhase.Ready);
            yield return RunPopupSequence(m_PostPracticePopups);

            // ---- Block loop ----
            for (int b = 0; b < blockCount; b++)
            {
                CurrentBlockIndex = b;

                SetPhase(TaskPhase.BlockIntro);
                yield return RunOnePopup(m_BlockIntroPopup);

                SetPhase(TaskPhase.Block);
                m_EegMarkerEmitter?.Emit("phase_block", "", b.ToString());
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

            // ---- Outro ----
            SetPhase(TaskPhase.Outro);
            m_EegMarkerEmitter?.Emit("phase_outro");
            yield return RunOnePopup(m_OutroPopup);

            EndSession();
        }

        // ---- Practice ----

        IEnumerator RunPracticePhase()
        {
            bool feedbackWired = false;
            if (m_ResponseIndicator != null && m_TaskManager != null)
            {
                m_TaskManager.ResponseIndicator += m_ResponseIndicator.Show;
                feedbackWired = true;
            }

            yield return ForcedResponsePopup(m_ForceLeftPopup, SemanticCommand.Hit);
            yield return ForcedResponsePopup(m_ForceRightPopup, SemanticCommand.Miss);

            yield return RunPopupSequence(m_PracticeTrialPopups);

            var practiceTrials = Asset.GeneratePracticeTrials(m_SessionMetadata.shoulderWidthCm);
            if (m_FixationCross != null) m_FixationCross.Show();
            m_TaskManager.StartTrialList(-1, practiceTrials);
            while (m_TaskManager.IsRunning) yield return null;
            if (m_FixationCross != null) m_FixationCross.Hide();

            if (feedbackWired)
                m_TaskManager.ResponseIndicator -= m_ResponseIndicator.Show;
        }

        IEnumerator ForcedResponsePopup(TaskPopupPanel panel, SemanticCommand expected)
        {
            if (panel == null)
            {
                Debug.LogWarning($"[HitOrMissAppController] Forced-response popup ({expected}) not assigned. Skipping.");
                yield break;
            }
            var ctx = BuildPopupContext();
            string text = ctx.ResolveText(panel);

            panel.SetText(text);
            panel.Show();

            bool got = false;
            void Handler(ResponseEvent ev)
            {
                if (ev.command == expected) got = true;
            }

            if (m_InputSource == null)
            {
                Debug.LogError("[HitOrMissAppController] No input source for practice popup.");
                panel.Hide();
                yield break;
            }
            m_InputSource.ResponseReceived += Handler;
            m_InputSource.Enable();

            while (!got) yield return null;
            m_InputSource.ResponseReceived -= Handler;

            if (m_ResponseIndicator != null)
                m_ResponseIndicator.Show(expected, true);
            yield return new WaitForSeconds(Asset != null ? Asset.PracticeFeedbackSeconds : 1f);

            panel.Hide();
        }

        // ---- Popup sequencing helpers ----

        IEnumerator RunPopupSequence(TaskPopupPanel[] sequence)
        {
            if (sequence == null) yield break;
            foreach (var panel in sequence)
            {
                if (panel == null) continue;
                yield return RunOnePopup(panel);
            }
        }

        /// <summary>
        /// Pre-practice sequence with a slot-aware positioning step: the
        /// StandingCross is enabled only while the popup at
        /// m_PositioningPopupIndex is showing.
        /// </summary>
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

        IEnumerator RunOnePopup(TaskPopupPanel panel)
        {
            if (panel == null) yield break;
            yield return panel.Run(BuildPopupContext());
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

        // ---- Lifecycle ----

        void EndSession()
        {
            if (m_TaskLogger != null)
            {
                m_TaskManager.TrialJudged -= m_TaskLogger.LogTrial;
                m_TaskLogger.EndSession();
            }
            if (m_ResponseIndicator != null && m_TaskManager != null)
                m_TaskManager.ResponseIndicator -= m_ResponseIndicator.Show;

            HideAllPopups();
            if (m_FixationCross != null) m_FixationCross.Hide();
            if (m_ClinicianPanel != null) m_ClinicianPanel.ExitTaskMode();

            m_EegMarkerEmitter?.EndSession();

            // Drop the session-local asset clone. Restoring m_TaskManager's
            // pointer to the editor-assigned source asset means a future
            // session will rebuild its own clone with whatever overrides
            // the new metadata supplies.
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
            HideArray(m_PracticeTrialPopups);
            HideArray(m_PostPracticePopups);
            if (m_ForceLeftPopup != null) m_ForceLeftPopup.Hide();
            if (m_ForceRightPopup != null) m_ForceRightPopup.Hide();
            if (m_BlockIntroPopup != null) m_BlockIntroPopup.Hide();
            if (m_BreakPopup != null) m_BreakPopup.Hide();
            if (m_BlockReadyPopup != null) m_BlockReadyPopup.Hide();
            if (m_OutroPopup != null) m_OutroPopup.Hide();
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
            // LocalizedTermTable.Get returns "[key]" for missing entries; treat that as missing.
            return string.IsNullOrEmpty(v) || (v.StartsWith("[") && v.EndsWith("]")) ? fallback : v;
        }
    }
}
