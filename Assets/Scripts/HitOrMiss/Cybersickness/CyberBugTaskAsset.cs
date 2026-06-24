using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    [CreateAssetMenu(
        fileName = "CyberBugTask",
        menuName = "Parkinson/Cybersickness/Bug Prediction Task Asset")]
    public class CyberBugTaskAsset : ScriptableObject
    {
        [SerializeField] string m_TaskName = "Cyber Bug Prediction Task";

        [Header("Protocol")]
        [SerializeField] int m_BlockCount = 1;

        [Tooltip("Per block. Total trials = Miss + ContactSplash.")]
        [SerializeField] int m_MissTrialsPerBlock = 10;

        [SerializeField] int m_ContactSplashTrialsPerBlock = 10;

        [Header("Bug trajectory")]
        [SerializeField] float m_StartDistance = 5f;

        [Tooltip("How close the bug gets on contact trials.")]
        [SerializeField] float m_ContactDistance = 0.25f;

        [Tooltip("How far behind the participant the bug flies on miss trials.")]
        [SerializeField] float m_PassBehindDistance = 1.2f;

        [Tooltip("Clear lateral miss distance in meters.")]
        [SerializeField] float m_MissLateralOffset = 0.85f;

        [SerializeField] float m_SlowSpeed = 1.4f;
        [SerializeField] float m_FastSpeed = 2.4f;

        [Header("Timing")]
        [SerializeField] float m_ItiSeconds = 1.5f;
        [SerializeField] float m_BreakDurationSeconds = 30f;

        [Header("Instruction text")]
        [TextArea(4, 12)]
        [SerializeField] string m_IntroText =
            "In this task, a bug will approach you.\n\n" +
            "Sometimes it will clearly miss you.\n" +
            "Sometimes it will touch you and splash.\n\n" +
            "Your task is to predict whether the bug will touch you.";

        [TextArea(4, 12)]
        [SerializeField] string m_ControllerGuideText =
            "Use the triggers to answer:\n\n" +
            "LEFT trigger = YES\n" +
            "RIGHT trigger = NO\n\n" +
            "You will answer the question:\n\n" +
            "Will the bug touch you?";

        [TextArea(2, 8)]
        [SerializeField] string m_ReadyText =
            "Ready?\n\n" +
            "Watch the bug carefully and answer before it reaches you.";

        [TextArea(2, 8)]
        [SerializeField] string m_OutroText =
            "The task is complete.\n\nThank you.";

        public string TaskName => m_TaskName;

        public int BlockCount => m_BlockCount;
        public float ItiSeconds => m_ItiSeconds;
        public float BreakDurationSeconds => m_BreakDurationSeconds;

        public string IntroText => m_IntroText;
        public string ControllerGuideText => m_ControllerGuideText;
        public string ReadyText => m_ReadyText;
        public string OutroText => m_OutroText;

        public CyberBugTrialDefinition[] GenerateBlock(int blockIndex)
        {
            List<CyberBugTrialDefinition> trials = new();

            for (int i = 0; i < m_MissTrialsPerBlock; i++)
                trials.Add(BuildTrial(blockIndex, CyberBugCondition.Miss));

            for (int i = 0; i < m_ContactSplashTrialsPerBlock; i++)
                trials.Add(BuildTrial(blockIndex, CyberBugCondition.ContactSplash));

            Shuffle(trials);

            for (int i = 0; i < trials.Count; i++)
            {
                var trial = trials[i];

                trial.blockIndex = blockIndex;
                trial.trialIndex = i;
                trial.trialId = $"CYBERBUG_B{blockIndex + 1}_T{i + 1:00}_{trial.condition}";

                trials[i] = trial;
            }

            return trials.ToArray();
        }

        CyberBugTrialDefinition BuildTrial(int blockIndex, CyberBugCondition condition)
        {
            bool fast = Random.value > 0.5f;
            float speed = fast ? m_FastSpeed : m_SlowSpeed;

            float side = Random.value > 0.5f ? 1f : -1f;

            CyberBugTrialDefinition trial = new CyberBugTrialDefinition
            {
                blockIndex = blockIndex,
                trialIndex = 0,

                condition = condition,

                startDistance = m_StartDistance,
                speed = speed,

                contactDistance = m_ContactDistance,
                passBehindDistance = m_PassBehindDistance,

                lateralOffset = 0f,

                willTouch = false,
                willSplash = false,

                expectedResponse = CyberYesNoResponse.No
            };

            switch (condition)
            {
                case CyberBugCondition.Miss:
                    trial.lateralOffset = side * m_MissLateralOffset;
                    trial.willTouch = false;
                    trial.willSplash = false;
                    trial.expectedResponse = CyberYesNoResponse.No;
                    break;

                case CyberBugCondition.ContactSplash:
                    trial.lateralOffset = 0f;
                    trial.willTouch = true;
                    trial.willSplash = true;
                    trial.expectedResponse = CyberYesNoResponse.Yes;
                    break;
            }

            return trial;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void OnValidate()
        {
            if (m_BlockCount < 1) m_BlockCount = 1;

            m_MissTrialsPerBlock = Mathf.Max(0, m_MissTrialsPerBlock);
            m_ContactSplashTrialsPerBlock = Mathf.Max(0, m_ContactSplashTrialsPerBlock);

            if (m_StartDistance <= 0f) m_StartDistance = 5f;
            if (m_ContactDistance < 0f) m_ContactDistance = 0.25f;
            if (m_PassBehindDistance < 0f) m_PassBehindDistance = 1.2f;

            if (m_MissLateralOffset < 0.1f) m_MissLateralOffset = 0.85f;

            if (m_SlowSpeed <= 0f) m_SlowSpeed = 1f;
            if (m_FastSpeed <= m_SlowSpeed) m_FastSpeed = m_SlowSpeed + 0.5f;

            if (m_ItiSeconds < 0f) m_ItiSeconds = 0f;
            if (m_BreakDurationSeconds < 0f) m_BreakDurationSeconds = 0f;
        }
    }
}