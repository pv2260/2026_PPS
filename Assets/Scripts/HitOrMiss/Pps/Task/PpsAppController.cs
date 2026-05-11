using System.Collections;
using UnityEngine;

namespace HitOrMiss.Pps
{
    public class PPSAppController : MonoBehaviour
    {
        [Header("References")]
        [Header("Input")]
        [SerializeField] private KeyboardCommandInput m_KeyboardInput;
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

            if (m_KeyboardInput == null)
            {
                Debug.LogError("[PPSAppController] Keyboard input is not assigned.");
                yield break;
            }

            m_Running = true;
            m_TaskManager.SetInputSource(m_KeyboardInput);
            m_KeyboardInput.Enable();
            yield return RunTask1();
        }

        private IEnumerator RunTask1()
        {
            yield return m_Ui.ShowWelcomeAndWait();

            //after this, WelcomePanel hides and passthrough is visible
            yield return m_Ui.ShowTriggerCheckAndWait();
            yield return m_Ui.ShowInstructionsAndWait();
            m_Ui.ShowStandingCross();
            yield return m_Ui.ShowPositioningAndWait();

            // ---- Practice 1: tactile only ----

            yield return m_Ui.ShowPracticeIntroVTOnlyAndWait();

            Debug.Log("[PPS] Starting VT-only practice.");

            yield return m_TaskManager.RunTrials(
                PpsTrialGenerator.GenerateVTOnlyPractice(m_TaskAsset)
            );

            // ---- Practice 2: visual + tactile ----

            yield return m_Ui.ShowPracticeIntroVTVisualAndWait();

            Debug.Log("[PPS] Starting VT+Visual practice.");

            yield return m_TaskManager.RunTrials(
                PpsTrialGenerator.GenerateVTVisualPractice(m_TaskAsset)
            );
           
                                    
            yield return m_Ui.ShowNoFeedbackAndWait();
            yield return m_Ui.ShowReadyToStartAndWait();

            m_TaskManager.BeginLogging(m_SubjectIdFallback);

            for (int blockIndex = 0; blockIndex < m_TaskAsset.BlockCount; blockIndex++)
            {
                yield return m_Ui.ShowBlockCounterAndWait(blockIndex, m_TaskAsset.BlockCount);

                PpsTrialDefinition[] trials = m_TaskAsset.GenerateBlock(blockIndex);
                yield return m_TaskManager.RunTrials(trials);

                if (blockIndex < m_TaskAsset.BlockCount - 1)
                    yield return m_Ui.ShowBreakAndWait(m_TaskAsset.RestDurationSeconds);
            }

            m_TaskManager.EndLogging();
            m_Ui.HideStandingCross();
            yield return m_Ui.ShowEndAndWait("Task 1 complete.\n\nThank you.");
        }
    }
}