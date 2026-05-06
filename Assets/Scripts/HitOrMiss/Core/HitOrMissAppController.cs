using System.Collections;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Top-level orchestrator for the Hit-or-Miss assessment.
    ///
    /// Session flow per the AR-Task PDF (29.04.2026 spec):
    ///   1. Popup 1 (intro + START PRACTICE)
    ///   2. Practice phase:
    ///        Popup 2 — force LEFT trigger, show "HIT" feedback
    ///        Popup 3 — force RIGHT trigger, show "MISS" feedback
    ///        Popup 4 — intro to practice trials
    ///        N practice trials with HIT/MISS feedback (not logged)
    ///   3. Popup 5 (no-feedback warning + START TASK)
    ///   4. For each block:
    ///        Popup 6 — block intro
    ///        Block trials (no feedback)
    ///        Popup 7 — break (countdown)        ← skipped after the last block
    ///        Popup 8 — ready for next block      ← skipped after the last block
    ///   5. Popup 9 (thank you)
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

        [Header("Popup panels (9 total, see RunSession for which is used where)")]
        [SerializeField] TaskPopupPanel m_Popup1Intro;
        [SerializeField] TaskPopupPanel m_Popup2Left;
        [SerializeField] TaskPopupPanel m_Popup3Right;
        [SerializeField] TaskPopupPanel m_Popup4Practice;
        [SerializeField] TaskPopupPanel m_Popup5Ready;
        [SerializeField] TaskPopupPanel m_Popup6BlockIntro;
        [SerializeField] TaskPopupPanel m_Popup7Break;
        [SerializeField] TaskPopupPanel m_Popup8NextBlock;
        [SerializeField] TaskPopupPanel m_Popup9Outro;

        [Header("Visuals")]
        [Tooltip("Optional fixation cross shown between trials. If left empty the manager's crosshair is used as-is.")]
        [SerializeField] FixationCrossController m_FixationCross;

        [Header("Participant Feedback")]
        [Tooltip("HIT/MISS visual flash. Wired only during the practice phase per the PDF (no feedback during the main task).")]
        [SerializeField] ResponseIndicator m_ResponseIndicator;

        [Header("Clinician")]
        [SerializeField] ClinicianControlPanel m_ClinicianPanel;

        SupportedLanguage m_Language = SupportedLanguage.English;
        TaskPhase m_CurrentPhase = TaskPhase.Idle;
        Coroutine m_SessionCoroutine;
        IResponseInputSource m_InputSource;

        // Session metadata captured by the clinician form (Phase C). Until that
        // form exists, callers can call SetSessionMetadata directly; otherwise
        // a default record is built from ParticipantId and the task asset.
        SessionMetadata m_SessionMetadata;
        bool m_SessionMetadataExplicitlySet;

        public TaskPhase CurrentPhase => m_CurrentPhase;
        public TrajectoryTaskManager TaskManager => m_TaskManager;
        public string ParticipantId { get; set; } = "P000";
        public int CurrentBlockIndex { get; private set; }
        public bool IsPaused => m_TaskManager != null && m_TaskManager.IsPaused;

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
            Debug.Log($"[HitOrMissAppController] StartSession called. CurrentPhase={m_CurrentPhase}");
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

            // Configure logger
            if (m_TaskLogger != null)
            {
                m_TaskLogger.ParticipantId = ParticipantId;

                if (!m_SessionMetadataExplicitlySet)
                    m_SessionMetadata = SessionMetadata.CreateDefault(ParticipantId);
                else
                    m_SessionMetadata.participantId = ParticipantId;

                m_SessionMetadata.PopulateFromTaskAsset(m_TaskAsset);
                if (string.IsNullOrEmpty(m_SessionMetadata.sessionId))
                    m_SessionMetadata.sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                if (string.IsNullOrEmpty(m_SessionMetadata.sessionDate))
                    m_SessionMetadata.sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd");

                m_TaskLogger.SetMetadata(m_SessionMetadata);
                m_TaskLogger.BeginSession(m_TaskAsset != null ? m_TaskAsset.TaskName : "HitOrMiss");
                m_TaskManager.TrialJudged += m_TaskLogger.LogTrial;
            }

            // Configure EEG
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
            if (m_CurrentPhase == TaskPhase.Idle)
            {
                Debug.LogWarning("[HitOrMissAppController] PauseSession called but no session is running.");
                return;
            }
            if (m_TaskManager == null || !m_TaskManager.IsRunning || m_TaskManager.IsPaused) return;

            m_TaskManager.PauseBlock();
            m_EegMarkerEmitter?.Emit("session_paused", "", CurrentBlockIndex.ToString());

            if (m_TaskLogger != null)
                m_TaskLogger.Flush(CurrentBlockIndex, m_TaskManager.NextTrialIndex);

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.EnterPausedMode();

            SessionPaused?.Invoke();
            Debug.Log("[HitOrMissAppController] Session paused.");
        }

        public void ResumeSession()
        {
            if (m_TaskManager == null || !m_TaskManager.IsPaused) return;

            m_TaskManager.ResumeBlock();
            m_EegMarkerEmitter?.Emit("session_resumed", "", CurrentBlockIndex.ToString());

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.ExitPausedMode();

            SessionResumed?.Invoke();
            Debug.Log("[HitOrMissAppController] Session resumed.");
        }

        // ====================================================================
        // Session coroutine: implements PDF popup flow
        // ====================================================================

        IEnumerator RunSession()
        {
            Debug.Log("[HOM] RunSession entered");
            int blockCount = m_TaskAsset != null ? m_TaskAsset.BlockCount : 3;

            // ---- Popup 1 ----
            SetPhase(TaskPhase.Intro);
            Debug.Log("[HOM] About to show Popup1");
            m_EegMarkerEmitter?.Emit("phase_intro");
            yield return ShowPopupAndWait(m_Popup1Intro, m_TaskAsset.Popup1IntroKey,
                "Hit or Miss Task — press CONTINUE to start the practice.");
            Debug.Log("[HOM] Popup1 dismissed");

            // ---- Practice ----
            SetPhase(TaskPhase.Practice);
            m_EegMarkerEmitter?.Emit("phase_practice");
            yield return RunPracticePhase();

            // ---- Popup 5 ----
            SetPhase(TaskPhase.Ready);
            yield return ShowPopupAndWait(m_Popup5Ready, m_TaskAsset.Popup5ReadyKey,
                "You are all set. No feedback during the task. Press START TASK.");

            // ---- Block loop ----
            for (int b = 0; b < blockCount; b++)
            {
                CurrentBlockIndex = b;

                SetPhase(TaskPhase.BlockIntro);
                yield return ShowPopupAndWait(m_Popup6BlockIntro, m_TaskAsset.Popup6BlockIntroKey,
                    $"Let's start with Block {b + 1}.", b + 1);

                SetPhase(TaskPhase.Block);
                m_EegMarkerEmitter?.Emit("phase_block", "", b.ToString());
                if (m_FixationCross != null) m_FixationCross.Show();
                // Generate the block here so the participant's shoulder width
                // (Phase E) shapes the lateral-offset bands and curve
                // magnitudes. The manager runs whatever trial list we hand it.
                var blockTrials = m_TaskAsset.GenerateBlock(b, m_SessionMetadata.shoulderWidthCm);
                m_TaskManager.StartTrialList(b, blockTrials);
                while (m_TaskManager.IsRunning) yield return null;
                if (m_FixationCross != null) m_FixationCross.Hide();

                if (b < blockCount - 1)
                {
                    SetPhase(TaskPhase.Rest);
                    m_EegMarkerEmitter?.Emit("phase_rest");
                    float breakSeconds = m_TaskAsset != null ? m_TaskAsset.BreakDurationSeconds : 60f;
                    yield return ShowPopupForSeconds(m_Popup7Break, m_TaskAsset.Popup7BreakKey,
                        $"All done with Block {b + 1}. Take a little break.", breakSeconds, showCountdown: true);

                    SetPhase(TaskPhase.BlockReady);
                    yield return ShowPopupAndWait(m_Popup8NextBlock, m_TaskAsset.Popup8NextBlockKey,
                        $"Ready to start with Block {b + 2}? Let's go!", b + 2);
                }
            }

            // ---- Popup 9 ----
            SetPhase(TaskPhase.Outro);
            m_EegMarkerEmitter?.Emit("phase_outro");
            float outroDuration = m_TaskAsset != null ? m_TaskAsset.OutroDuration : 10f;
            yield return ShowPopupForSeconds(m_Popup9Outro, m_TaskAsset.Popup9OutroKey,
                "You are all done. Thank you for your participation.", outroDuration);

            EndSession();
        }

        // ---- Practice ----

        IEnumerator RunPracticePhase()
        {
            // Wire response indicator for practice only — main task has no feedback.
            bool feedbackWired = false;
            if (m_ResponseIndicator != null && m_TaskManager != null)
            {
                m_TaskManager.ResponseIndicator += m_ResponseIndicator.Show;
                feedbackWired = true;
            }

            // Practice popup 2: force LEFT (Hit), feedback "HIT"
            yield return ForcedResponsePopup(m_Popup2Left, m_TaskAsset.Popup2LeftKey,
                "Press LEFT now (this should mean HIT).",
                SemanticCommand.Hit);

            // Practice popup 3: force RIGHT (Miss), feedback "MISS"
            yield return ForcedResponsePopup(m_Popup3Right, m_TaskAsset.Popup3RightKey,
                "Press RIGHT now (this should mean MISS).",
                SemanticCommand.Miss);

            // Practice popup 4: intro then run practice trials
            yield return ShowPopupAndWait(m_Popup4Practice, m_TaskAsset.Popup4PracticeKey,
                "Now let's put it into practice.");

            var practiceTrials = m_TaskAsset.GeneratePracticeTrials(m_SessionMetadata.shoulderWidthCm);
            if (m_FixationCross != null) m_FixationCross.Show();
            m_TaskManager.StartTrialList(-1, practiceTrials);
            while (m_TaskManager.IsRunning) yield return null;
            if (m_FixationCross != null) m_FixationCross.Hide();

            if (feedbackWired)
                m_TaskManager.ResponseIndicator -= m_ResponseIndicator.Show;
        }

        IEnumerator ForcedResponsePopup(TaskPopupPanel panel, string textKey, string fallback, SemanticCommand expected)
        {
            string text = GetLocalizedString(textKey, fallback);
            if (panel == null)
            {
                Debug.LogWarning($"[HitOrMissAppController] Forced-response popup is null (key={textKey}). Skipping.");
                yield break;
            }
            panel.SetText(text);
            panel.Show();

            bool got = false;
            void Handler(ResponseEvent ev)
            {
                if (ev.command == expected) got = true;
            }

            if (m_InputSource == null)
            {
                Debug.LogError("[HitOrMissAppController] No input source assigned for practice popup.");
                panel.Hide();
                yield break;
            }
            m_InputSource.ResponseReceived += Handler;
            m_InputSource.Enable();

            while (!got) yield return null;

            m_InputSource.ResponseReceived -= Handler;

            // Show feedback flash
            if (m_ResponseIndicator != null)
                m_ResponseIndicator.Show(expected, true);
            yield return new WaitForSeconds(m_TaskAsset != null ? m_TaskAsset.PracticeFeedbackSeconds : 1f);

            panel.Hide();
        }

        // ---- Popup helpers ----

        IEnumerator ShowPopupAndWait(TaskPopupPanel panel, string textKey, string fallback, int blockNumber = 0)
        {
            if (panel == null)
            {
                Debug.LogWarning($"[HitOrMissAppController] Popup panel for key={textKey} not assigned. Skipping.");
                yield break;
            }
            string text = GetLocalizedString(textKey, fallback);
            if (text != null && blockNumber > 0)
                text = text.Replace("{block}", blockNumber.ToString());
            yield return panel.ShowAndWaitForButton(text);
        }

        IEnumerator ShowPopupForSeconds(TaskPopupPanel panel, string textKey, string fallback, float seconds, bool showCountdown = false)
        {
            if (panel == null)
            {
                Debug.LogWarning($"[HitOrMissAppController] Popup panel for key={textKey} not assigned. Falling back to plain wait.");
                yield return new WaitForSeconds(seconds);
                yield break;
            }
            string text = GetLocalizedString(textKey, fallback);
            yield return panel.ShowForSeconds(text, seconds, showCountdown);
        }

        // ---- Lifecycle ----

        void EndSession()
        {
            if (m_TaskLogger != null)
            {
                m_TaskManager.TrialJudged -= m_TaskLogger.LogTrial;
                m_TaskLogger.EndSession();
            }

            // Defensive: unwire response indicator if practice didn't clean up.
            if (m_ResponseIndicator != null && m_TaskManager != null)
                m_TaskManager.ResponseIndicator -= m_ResponseIndicator.Show;

            HideAllPopups();
            if (m_FixationCross != null) m_FixationCross.Hide();

            if (m_ClinicianPanel != null)
                m_ClinicianPanel.ExitTaskMode();

            m_EegMarkerEmitter?.EndSession();
            SetPhase(TaskPhase.Idle);
            m_SessionCoroutine = null;

            SessionEnded?.Invoke();
            Debug.Log("[HitOrMissAppController] Session complete. Data saved.");
        }

        void HideAllPopups()
        {
            if (m_Popup1Intro != null)      m_Popup1Intro.Hide();
            if (m_Popup2Left != null)       m_Popup2Left.Hide();
            if (m_Popup3Right != null)      m_Popup3Right.Hide();
            if (m_Popup4Practice != null)   m_Popup4Practice.Hide();
            if (m_Popup5Ready != null)      m_Popup5Ready.Hide();
            if (m_Popup6BlockIntro != null) m_Popup6BlockIntro.Hide();
            if (m_Popup7Break != null)      m_Popup7Break.Hide();
            if (m_Popup8NextBlock != null)  m_Popup8NextBlock.Hide();
            if (m_Popup9Outro != null)      m_Popup9Outro.Hide();
        }

        string GetLocalizedString(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || m_TermTable == null) return fallback;
            string v = m_TermTable.Get(key, m_Language);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
