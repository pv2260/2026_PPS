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

        [Header("Anchor (single reference point for panels + stimuli)")]
        [Tooltip("Optional SessionAnchor that is calibrated to the participant's camera direction once at session start. Parent panels / DistanceLayout / floor cross to this anchor so they all appear from the same reference point.")]
        [SerializeField] private HitOrMiss.SessionAnchor m_SessionAnchor;
        [Tooltip("Camera transform used to calibrate the SessionAnchor. Leave empty to use Camera.main at runtime.")]
        [SerializeField] private Transform m_CameraForAnchor;

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

    // Show the standing cross during positioning/task setup. This cross is
    // the *reference* the participant is asked to look at — when they press
    // Continue on the Positioning panel below, the camera's current forward
    // direction defines the SessionAnchor's forward for the rest of the
    // session (panels, stimuli, fixation marks all originate from there).
    m_Ui.ShowStandingCross();

    yield return m_Ui.ShowPositioningAndWait();

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // Calibrate the SessionAnchor NOW — the participant has just confirmed
    // they're standing in position and looking at the reference cross. The
    // anchor's forward becomes whatever the camera is pointing at this
    // moment. Anything parented to the anchor (panels, DistanceLayout,
    // spawn origins, floor cross, etc.) snaps into place from this frame.
    if (m_SessionAnchor != null)
    {
        Transform cam = m_CameraForAnchor != null
            ? m_CameraForAnchor
            : (Camera.main != null ? Camera.main.transform : null);
        if (cam != null)
        {
            m_SessionAnchor.Calibrate(cam);
            Debug.Log($"[PPSAppController] SessionAnchor calibrated from positioning confirmation. " +
                      $"Camera forward at confirmation: {cam.forward}.");
        }
        else
        {
            Debug.LogWarning("[PPSAppController] No camera found for SessionAnchor calibration — neither m_CameraForAnchor nor Camera.main is set. Subsequent panels and stimuli will use the anchor's pre-calibration transform.");
        }
    }
    else
    {
        Debug.LogWarning("[PPSAppController] m_SessionAnchor not wired in the inspector — calibration skipped. Panels will appear at their authored transforms (which may not match the participant's forward direction).");
    }

    // ---- Practice 1: tactile only ----
    Debug.Log("[PPSAppController] Practice 1 intro: showing PracticeIntroVTOnly panel.");
    yield return m_Ui.ShowPracticeIntroVTOnlyAndWait();
    Debug.Log("[PPSAppController] PracticeIntroVTOnly panel closed — entering VT-only practice trials.");

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    var vtOnlyPractice = PpsTrialGenerator.GenerateVTOnlyPractice(m_TaskAsset);
    Debug.Log($"[PPSAppController] Generated VT-only practice list: " +
              $"{(vtOnlyPractice == null ? "NULL" : vtOnlyPractice.Length.ToString())} trials.");
    if (vtOnlyPractice == null || vtOnlyPractice.Length == 0)
    {
        Debug.LogError("[PPSAppController] VT-only practice list is empty — RunTrials would return immediately. Check PpsTrialGenerator.GenerateVTOnlyPractice or PpsTaskAsset.");
    }

    Debug.Log("[PPSAppController] Calling m_TaskManager.RunTrials for VT-only practice.");
    yield return m_TaskManager.RunTrials(vtOnlyPractice);
    Debug.Log("[PPSAppController] m_TaskManager.RunTrials returned for VT-only practice.");

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    // ---- Practice 2: visual + tactile ----
    Debug.Log("[PPSAppController] Practice 2 intro: showing PracticeIntroVTVisual panel.");
    yield return m_Ui.ShowPracticeIntroVTVisualAndWait();
    Debug.Log("[PPSAppController] PracticeIntroVTVisual panel closed — entering VT+Visual practice trials.");

    if (StopWasRequested())
    {
        yield return StopExperiment();
        yield break;
    }

    var vtVisualPractice = PpsTrialGenerator.GenerateVTVisualPractice(m_TaskAsset);
    Debug.Log($"[PPSAppController] Generated VT+Visual practice list: " +
              $"{(vtVisualPractice == null ? "NULL" : vtVisualPractice.Length.ToString())} trials.");
    if (vtVisualPractice == null || vtVisualPractice.Length == 0)
    {
        Debug.LogError("[PPSAppController] VT+Visual practice list is empty — RunTrials would return immediately.");
    }

    Debug.Log("[PPSAppController] Calling m_TaskManager.RunTrials for VT+Visual practice.");
    yield return m_TaskManager.RunTrials(vtVisualPractice);
    Debug.Log("[PPSAppController] m_TaskManager.RunTrials returned for VT+Visual practice.");

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