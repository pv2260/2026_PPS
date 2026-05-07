using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss.Pps
{
    public class PPSAppController : MonoBehaviour
    {
        private bool m_Running;

        [Header("References")]
        [SerializeField] private PpsTaskManager m_TaskManager;
        [SerializeField] private SessionFlowPanels m_Ui;
        [SerializeField] private PpsTaskAsset m_TaskAsset;

        [Header("Experiment structure")]
        [SerializeField] private int m_BlockCount = 3;
        [SerializeField] private int m_BlockRepetitions = 1;
        [SerializeField] private float m_RestDurationSeconds = 30f;

        [Header("Subject setup")]
        [SerializeField] private float m_ShoulderWidthCm = 40f;

        private void Start()
        {
            if (m_Running)
                return;

            m_Running = true;
            StartCoroutine(RunExperiment());
        }

        private IEnumerator RunExperiment()
        {
            yield return m_Ui.ShowWelcomeAndWait();

            m_TaskManager.BeginLogging("test-subject");

            yield return m_Ui.ShowInstructionsAndWait();

            yield return m_Ui.ShowPositioningAndWait();

            yield return RunPractice();

            yield return m_Ui.ShowReadyToStartAndWait();

            for (int blockIndex = 0; blockIndex < m_BlockCount; blockIndex++)
            {
                yield return m_Ui.ShowBlockIntroAndWait(
                    $"Block {blockIndex + 1}/{m_BlockCount}\n\n" +
                    "Press Begin when you are ready."
                );

                PpsTrialDefinition[] blockTrials = GenerateRuntimeBlock(blockIndex);

                yield return m_TaskManager.RunTrials(blockTrials);

                if (blockIndex < m_BlockCount - 1)
                {
                    yield return m_Ui.ShowRestAndAutoAdvance(m_RestDurationSeconds);
                }
            }

            yield return m_Ui.ShowOutro(
                "Task 1 complete.\n\nThank you.",
                5f
            );

            m_TaskManager.EndLogging();
        }

        private IEnumerator RunPractice()
        {
            yield return m_Ui.ShowPracticeIntroVTOnlyAndWait();

            m_Ui.ShowTrialStatus("Get ready...");
            yield return new WaitForSeconds(1.0f);

            yield return m_TaskManager.RunTrials(
                PpsTrialGenerator.GenerateChestVibrationPractice(m_TaskAsset)
            );

            m_Ui.HideTrialStatus();

            yield return m_Ui.ShowPracticeIntroVTVisualAndWait();

            m_Ui.ShowTrialStatus("Get ready...");
            yield return new WaitForSeconds(1.0f);

            yield return m_TaskManager.RunTrials(
                PpsTrialGenerator.GenerateLightsAndVibrationPractice(m_TaskAsset)
            );

            m_Ui.HideTrialStatus();

            yield return m_Ui.ShowNoFeedbackAndWait();
        }

        private PpsTrialDefinition[] GenerateRuntimeBlock(int blockIndex)
        {
            var trials = new List<PpsTrialDefinition>();

            for (int r = 0; r < Mathf.Max(1, m_BlockRepetitions); r++)
            {
                PpsTrialDefinition[] generated = m_TaskAsset.GenerateBlock(blockIndex);

                if (generated != null)
                    trials.AddRange(generated);
            }

            Shuffle(trials);
            return trials.ToArray();
        }

        private void Shuffle(List<PpsTrialDefinition> trials)
        {
            for (int i = 0; i < trials.Count; i++)
            {
                int j = Random.Range(i, trials.Count);

                PpsTrialDefinition tmp = trials[i];
                trials[i] = trials[j];
                trials[j] = tmp;
            }
        }
    }
}