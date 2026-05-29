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

            yield return RunTask1();
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
                yield return m_TaskManager.RunTrials(trials);
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

            m_ControllerInput?.Disable();
            m_KeyboardInput?.Disable();

            if (m_Ui != null)
            {
                m_Ui.HideStandingCross();
                yield return m_Ui.ShowEndAndWait("Task stopped.\n\nThank you.");
            }

            m_Running = false;
        }
    }
}