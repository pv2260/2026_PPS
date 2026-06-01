using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    public class PPSAppController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyboardCommandInput m_KeyboardInput;
        [SerializeField] private MonoBehaviour m_ControllerInputBehaviour; // drag ControllerInput here

        // Resolved at runtime
        private IResponseInputSource m_ControllerInput;

        [SerializeField] private SessionFlowPanels m_Ui;
        [SerializeField] private PpsTaskManager m_TaskManager;
        [SerializeField] private PpsTaskAsset m_TaskAsset;

        [Header("Session")]
        [SerializeField] private string m_SubjectIdFallback = "P000";

        private bool m_Running;
        private bool m_StopRequested;

        // ---- Minimal public surface used by HitMissNetworkServer when it
        // is wired to a Task 1 (PPS) scene. Task 1 auto-starts in Start();
        // network clients can only observe state and request a stop. ----

        /// <summary>True while RunTask1 is executing.</summary>
        public bool IsRunning => m_Running;

        /// <summary>Returns the subject id used for this session.</summary>
        public string ParticipantId => m_SubjectIdFallback;

        /// <summary>Fired once when the task coroutine begins (after input wiring).</summary>
        public event System.Action SessionStarted;

        /// <summary>
        /// Fired once when the task coroutine winds down — both on natural
        /// completion (end-of-blocks) and on stop-requested early exit.
        /// </summary>
        public event System.Action SessionEnded;

        /// <summary>
        /// Asks the running task to wind down at the next opportunity.
        /// The active coroutine polls StopWasRequested() between steps and
        /// transitions to StopExperiment when the flag flips on.
        /// </summary>
        public void RequestStop()
        {
            m_StopRequested = true;
            Debug.Log("[PPSAppController] RequestStop() — task will end at the next checkpoint.");
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

            if (m_Running)
                yield break;

            // Resolve controller input from the inspector reference.
            if (m_ControllerInputBehaviour != null)
            {
                m_ControllerInput = m_ControllerInputBehaviour as IResponseInputSource;
                if (m_ControllerInput == null)
                    Debug.LogError("[PPSAppController] m_ControllerInputBehaviour does not implement IResponseInputSource.");
            }

            // Wire controller as primary, keyboard as fallback.
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

            // Single point where the session ends — covers both natural
            // completion and stop-requested early exit. RunTask1 / StopExperiment
            // no longer touch m_Running so this stays the only source of truth.
            m_Running = false;
            SessionEnded?.Invoke();
        }

        private IEnumerator RunTask1()
        {
            yield return m_Ui.ShowWelcomeAndWait();
            if (StopWasRequested()) { yield return StopExperiment(); yield break; }

            yield return m_Ui.ShowTriggerCheckAndWait();
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

            // Start logging only for the main task, not practice.
            m_TaskManager.BeginLogging(m_SubjectIdFallback);

            for (int blockIndex = 0; blockIndex < m_TaskAsset.BlockCount; blockIndex++)
            {
                yield return m_Ui.ShowBlockCounterAndWait(blockIndex, m_TaskAsset.BlockCount);
                if (StopWasRequested()) { yield return StopExperiment(); yield break; }

                PpsTrialDefinition[] trials = m_TaskAsset.GenerateBlock(blockIndex);
                yield return m_TaskManager.RunTrials(trials, blockIndex);
                if (StopWasRequested()) { yield return StopExperiment(); yield break; }

                if (blockIndex < m_TaskAsset.BlockCount - 1)
                {
                    yield return m_Ui.ShowBreakAndWait(m_TaskAsset.RestDurationSeconds);
                    if (StopWasRequested()) { yield return StopExperiment(); yield break; }
                }
            }

            m_TaskManager.EndLogging();

            m_ControllerInput?.Disable();
            m_KeyboardInput?.Disable();

            m_Ui.HideStandingCross();

            yield return m_Ui.ShowEndAndWait("Task 1 complete.\n\nThank you.");

            // m_Running and SessionEnded are set by Start() after RunTask1 returns,
            // so the natural-completion and stop-requested paths converge.
        }

        private bool StopWasRequested()
        {
            if (m_StopRequested) return true;
            return m_Ui != null && m_Ui.StopRequested;
        }

        private IEnumerator StopExperiment()
        {
            Debug.Log("[PPSAppController] Stop requested. Ending task.");

            if (m_TaskManager != null)
                m_TaskManager.EndLogging();

            m_ControllerInput?.Disable();
            m_KeyboardInput?.Disable();

            if (m_Ui != null)
            {
                m_Ui.HideStandingCross();
                yield return m_Ui.ShowEndAndWait("Task stopped.\n\nThank you.");
            }

            // m_Running and SessionEnded are set by Start() after RunTask1 returns.
        }
    }
}