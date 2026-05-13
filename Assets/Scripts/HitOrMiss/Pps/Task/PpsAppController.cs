using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// High-level controller for the PPS experiment flow.
    ///
    /// This class coordinates:
    /// - UI screens and participant instructions
    /// - practice sequence
    /// - main experimental blocks
    /// - rest breaks
    /// - session start/end logging
    /// - input source assignment
    ///
    /// Important:
    /// Trial-level behavior should remain inside PpsTaskManager.
    /// This class should only decide what happens next in the experiment flow.
    /// </summary>
    public class PPSAppController : MonoBehaviour
    {
        [Header("References")]

        [Header("Input")]
        [SerializeField] private KeyboardCommandInput m_KeyboardInput;

        // Handles all session-flow UI panels such as welcome,
        // instructions, practice intro, breaks, and end screen.
        [SerializeField] private SessionFlowPanels m_Ui;

        // Runs the actual PPS trials.
        // PPSAppController tells it which trials to run, but does not control trial internals.
        [SerializeField] private PpsTaskManager m_TaskManager;

        // Contains timing, block count, trial generation settings, and task configuration.
        [SerializeField] private PpsTaskAsset m_TaskAsset;

        [Header("Session")]

        // Fallback participant/session ID used when no external subject ID is provided yet.
        [SerializeField] private string m_SubjectIdFallback = "P000";

        // Prevents the task flow from starting more than once.
        private bool m_Running;

        /// <summary>
        /// Unity entry point for this controller.
        ///
        /// This starts the experiment flow after the first frame so that
        /// other scene objects have time to initialize.
        /// </summary>
        private IEnumerator Start()
        {
            Debug.Log("[PPSAppController] Start called.");

            // Wait one frame before starting.
            // This helps avoid initialization-order issues with UI and scene references.
            yield return null;

            // Validate required scene references before running the task.
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

            // Do not start a second copy of the task flow.
            if (m_Running)
                yield break;

            if (m_KeyboardInput == null)
            {
                Debug.LogError("[PPSAppController] Keyboard input is not assigned.");
                yield break;
            }

            m_Running = true;

            // For now, responses come from keyboard input.
            // Later this can be replaced with an XR input source using the same interface.
            m_TaskManager.SetInputSource(m_KeyboardInput);

            // Enable keyboard command input before the flow begins.
            m_KeyboardInput.Enable();

            yield return RunTask1();
        }

        /// <summary>
        /// Runs the full Task 1 experiment flow.
        ///
        /// This method controls the session-level order:
        /// - welcome
        /// - trigger check
        /// - instructions
        /// - positioning
        /// - practice trials
        /// - main blocks
        /// - breaks
        /// - end screen
        ///
        /// The actual trial behavior is delegated to PpsTaskManager.
        /// </summary>
private IEnumerator RunTask1()
{
    // Initial participant-facing setup screens.
    yield return m_Ui.ShowWelcomeAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // After this screen, the welcome panel hides and passthrough is visible.
    yield return m_Ui.ShowTriggerCheckAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    yield return m_Ui.ShowInstructionsAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // Show the standing cross during positioning/task setup.
    m_Ui.ShowStandingCross();

    yield return m_Ui.ShowPositioningAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // ---- Practice 1: tactile only ----
    yield return m_Ui.ShowPracticeIntroVTOnlyAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    Debug.Log("[PPS] Starting VT-only practice.");

    yield return m_TaskManager.RunTrials(
        PpsTrialGenerator.GenerateVTOnlyPractice(m_TaskAsset)
    );

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // ---- Practice 2: visual + tactile ----
    yield return m_Ui.ShowPracticeIntroVTVisualAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    Debug.Log("[PPS] Starting VT+Visual practice.");

    yield return m_TaskManager.RunTrials(
        PpsTrialGenerator.GenerateVTVisualPractice(m_TaskAsset)
    );

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // Tell the participant that practice feedback will stop before the real task starts.
    yield return m_Ui.ShowNoFeedbackAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // Final confirmation before the main experimental blocks.
    yield return m_Ui.ShowReadyToStartAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // Start logging only for the main task, not the practice trials.
    m_TaskManager.BeginLogging(m_SubjectIdFallback);

    // Run all configured experimental blocks.
    for (int blockIndex = 0; blockIndex < m_TaskAsset.BlockCount; blockIndex++)
    {
        // Show the participant which block is about to start.
        yield return m_Ui.ShowBlockCounterAndWait(blockIndex, m_TaskAsset.BlockCount);

        if (StopWasRequested())
        {
            yield return StopExperiment();
            yield break;
        }

        // Generate and run the trials for this block.
        PpsTrialDefinition[] trials = m_TaskAsset.GenerateBlock(blockIndex);
        yield return m_TaskManager.RunTrials(trials);

        if (StopWasRequested())
        {
            yield return StopExperiment();
            yield break;
        }

        // Show a rest screen between blocks, but not after the last block.
        if (blockIndex < m_TaskAsset.BlockCount - 1)
        {
            yield return m_Ui.ShowBreakAndWait(m_TaskAsset.RestDurationSeconds);

            if (StopWasRequested())
            {
                yield return StopExperiment();
                yield break;
            }
        }
    }

    // Stop writing trial data and close the session.
    m_TaskManager.EndLogging();

    // Disable input after the task is complete.
    if (m_KeyboardInput != null)
        m_KeyboardInput.Disable();

    // Remove the standing cross before the final screen.
    m_Ui.HideStandingCross();

    // Final participant-facing end screen.
    yield return m_Ui.ShowEndAndWait("Task 1 complete.\n\nThank you.");

    m_Running = false;
}


        private bool StopWasRequested()
        {
            return m_Ui != null && m_Ui.StopRequested;
        }

        private IEnumerator StopExperiment()
        {
            Debug.Log("[PPSAppController] Stop requested. Ending task.");

            if (m_TaskManager != null)
                m_TaskManager.EndLogging();

            if (m_KeyboardInput != null)
                m_KeyboardInput.Disable();

            if (m_Ui != null)
            {
                m_Ui.HideStandingCross();
                yield return m_Ui.ShowEndAndWait("Task stopped.\n\nThank you.");
            }

            m_Running = false;
        }
    }
    
}