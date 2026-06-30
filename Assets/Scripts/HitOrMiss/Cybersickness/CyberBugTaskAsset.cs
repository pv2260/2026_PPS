using System.Collections.Generic;
using UnityEngine;

namespace HitOrMiss.Cybersickness
{
    public enum CyberLanguage
    {
        English,
        French
    }

    [CreateAssetMenu(
        fileName = "CyberBugTask",
        menuName = "Parkinson/Cybersickness/Bug Prediction Task Asset")]
    public class CyberBugTaskAsset : ScriptableObject
    {
        [SerializeField] string m_TaskName = "Cyber Bug Prediction Task";

        [Header("Language")]
        [SerializeField] CyberLanguage m_CurrentLanguage = CyberLanguage.English;

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
        [SerializeField] float m_ItiSeconds = 2.0f;
        [SerializeField] float m_BreakDurationSeconds = 30f;

        [Header("Instruction text - English")]
        [TextArea(3, 8)]
        [SerializeField] string m_PracticeIntroTextEnglish =
            "Before we begin, you will complete a short practice round to learn what to expect and how to use the controller triggers.";

        [TextArea(2, 6)]
        [SerializeField] string m_TriggerDemoTextEnglish =
            "First, let’s learn which buttons to press.\n\n" +
            "Press the trigger on each controller.";

        [TextArea(3, 8)]
        [SerializeField] string m_ResponseMappingTextEnglish =
            "Great!\n\n" +
            "In this task, you will use the triggers to answer Yes or No.\n\n" +
            "Press LEFT for YES.\n" +
            "Press RIGHT for NO.";

        [TextArea(2, 6)]
        [SerializeField] string m_ApproachIntroTextEnglish =
            "In this task, objects may move toward you.\n\n" +
            "Do not worry — nothing can hurt you.";

        [TextArea(3, 8)]
        [SerializeField] string m_IntroTaskTextEnglish =
            "Now, let’s put everything together in a short task.\n\n" +
            "A small fish will swim in front of you.\n\n" +
            "Your task is to predict: will it touch you?\n\n" +
            "Answer YES or NO.";

        [TextArea(2, 8)]
        [SerializeField] string m_ControllerGuideTextEnglish =
            "Watch the fish carefully and answer before it reaches you.";

        [TextArea(2, 8)]
        [SerializeField] string m_ReadyTextEnglish =
            "Ready?\n\n" +
            "Watch the fish carefully and answer before it reaches you.";

        [TextArea(2, 8)]
        [SerializeField] string m_OutroTextEnglish =
            "The task is complete.\n\nThank you.";

        [Header("Instruction text - French")]
        [TextArea(3, 8)]
        [SerializeField] string m_PracticeIntroTextFrench =
            "Avant de commencer, vous ferez un court entraînement pour savoir à quoi vous attendre et apprendre à utiliser les gâchettes.";

        [TextArea(2, 6)]
        [SerializeField] string m_TriggerDemoTextFrench =
            "Commençons par apprendre quels boutons utiliser.\n\n" +
            "Appuyez sur la gâchette de chaque contrôleur.";

        [TextArea(3, 8)]
        [SerializeField] string m_ResponseMappingTextFrench =
            "Très bien !\n\n" +
            "Dans cette tâche, vous utiliserez les gâchettes pour répondre Oui ou Non.\n\n" +
            "Appuyez à GAUCHE pour OUI.\n" +
            "Appuyez à DROITE pour NON.";

        [TextArea(2, 6)]
        [SerializeField] string m_ApproachIntroTextFrench =
            "Dans cette tâche, des objets peuvent se déplacer vers vous.\n\n" +
            "Ne vous inquiétez pas — rien ne peut vous faire mal.";

        [TextArea(3, 8)]
        [SerializeField] string m_IntroTaskTextFrench =
            "Maintenant, mettons tout ensemble dans une courte tâche.\n\n" +
            "Un petit poisson va nager devant vous.\n\n" +
            "Votre tâche est de prédire : va-t-il vous toucher ?\n\n" +
            "Répondez OUI ou NON.";

        [TextArea(2, 8)]
        [SerializeField] string m_ControllerGuideTextFrench =
            "Regardez attentivement le poisson et répondez avant qu’il ne vous atteigne.";

        [TextArea(2, 8)]
        [SerializeField] string m_ReadyTextFrench =
            "Prêt ?\n\n" +
            "Regardez attentivement le poisson et répondez avant qu’il ne vous atteigne.";

        [TextArea(2, 8)]
        [SerializeField] string m_OutroTextFrench =
            "La tâche est terminée.\n\nMerci.";

        public string TaskName => m_TaskName;

        public CyberLanguage CurrentLanguage => m_CurrentLanguage;

        public int BlockCount => m_BlockCount;
        public float ItiSeconds => m_ItiSeconds;
        public float BreakDurationSeconds => m_BreakDurationSeconds;

        public string PracticeIntroText => GetText(m_PracticeIntroTextEnglish, m_PracticeIntroTextFrench);
        public string TriggerDemoText => GetText(m_TriggerDemoTextEnglish, m_TriggerDemoTextFrench);
        public string ResponseMappingText => GetText(m_ResponseMappingTextEnglish, m_ResponseMappingTextFrench);
        public string ApproachIntroText => GetText(m_ApproachIntroTextEnglish, m_ApproachIntroTextFrench);
        public string IntroTaskText => GetText(m_IntroTaskTextEnglish, m_IntroTaskTextFrench);
        public string ControllerGuideText => GetText(m_ControllerGuideTextEnglish, m_ControllerGuideTextFrench);
        public string ReadyText => GetText(m_ReadyTextEnglish, m_ReadyTextFrench);
        public string OutroText => GetText(m_OutroTextEnglish, m_OutroTextFrench);

        // Compatibility aliases, in case your app controller still uses these names.
        public string IntroText => PracticeIntroText;

        string GetText(string english, string french)
        {
            return m_CurrentLanguage == CyberLanguage.English
                ? english
                : french;
        }

        public void ToggleLanguage()
        {
            m_CurrentLanguage =
                m_CurrentLanguage == CyberLanguage.English
                    ? CyberLanguage.French
                    : CyberLanguage.English;
        }

        public void SetLanguage(CyberLanguage language)
        {
            m_CurrentLanguage = language;
        }

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