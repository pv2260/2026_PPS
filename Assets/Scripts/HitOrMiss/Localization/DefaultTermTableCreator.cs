#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace HitOrMiss
{
    /// <summary>
    /// Editor utility: creates a pre-filled LocalizedTermTable asset.
    /// Access via menu: Parkinson > Create Default Term Table.
    /// </summary>
    public static class DefaultTermTableCreator
    {
        [MenuItem("Parkinson/Create Default Term Table")]
        static void Create()
        {
            var table = ScriptableObject.CreateInstance<LocalizedTermTable>();

            var entries = new LocalizedEntry[]
            {
                new()
                {
                    key = "instruction_title",
                    english = "Hit or Miss",
                    french = "Touche ou Rat\u00e9"
                },
                new()
                {
                    key = "instruction_body",
                    english =
                        "A ball will approach you from a distance.\n" +
                        "Your task is to predict whether it will HIT you or MISS you.\n\n" +
                        "The ball will disappear before reaching you,\n" +
                        "so you must judge from its trajectory.\n\n" +
                        "Press the LEFT trigger if you think it will HIT you.\n" +
                        "Press the RIGHT trigger if you think it will MISS you.\n\n" +
                        "Respond as quickly and accurately as you can.\n" +
                        "There are 80 trials per block. Please stay still.",
                    french =
                        "Une balle s'approchera de vous depuis une certaine distance.\n" +
                        "Votre t\u00e2che est de pr\u00e9dire si elle va vous TOUCHER ou vous RATER.\n\n" +
                        "La balle dispara\u00eetra avant de vous atteindre,\n" +
                        "vous devez donc juger \u00e0 partir de sa trajectoire.\n\n" +
                        "Appuyez sur la g\u00e2chette GAUCHE si vous pensez qu'elle va vous TOUCHER.\n" +
                        "Appuyez sur la g\u00e2chette DROITE si vous pensez qu'elle va vous RATER.\n\n" +
                        "R\u00e9pondez aussi vite et pr\u00e9cis\u00e9ment que possible.\n" +
                        "Il y a 80 essais par bloc. Veuillez rester immobile."
                },
                new()
                {
                    key = "block_complete",
                    english = "Block completed \u2014 take a small break",
                    french = "Bloc termin\u00e9 \u2014 faites une courte pause"
                },
                new()
                {
                    key = "experiment_end",
                    english = "End of the experiment \u2014 Thank you!",
                    french = "Fin de l'exp\u00e9rience \u2014 Merci !"
                },
                new()
                {
                    key = "rest_message",
                    english = "Please rest. The next block will begin shortly.",
                    french = "Veuillez vous reposer. Le prochain bloc commencera bient\u00f4t."
                },
                new()
                {
                    key = "ready",
                    english = "Ready",
                    french = "Pr\u00eat"
                },
                new()
                {
                    key = "say_hit",
                    english = "HIT",
                    french = "TOUCHE"
                },
                new()
                {
                    key = "say_miss",
                    english = "MISS",
                    french = "RAT\u00c9"
                },
                new()
                {
                    key = "pps.instructions",
                    english =
                        "In this experiment, you will see two lights approaching you.\n\n" +
                        "Occasionally, you will feel a brief vibration on your chest.\n\n" +
                        "Your goal is to:\n" +
                        "  \u2022 Focus on the approaching lights.\n" +
                        "  \u2022 Respond as quickly as possible when you feel the vibration.\n" +
                        "  \u2022 Press the response button the moment you detect the touch.\n\n" +
                        "Keep your hand ready on the button and respond as fast and accurately as you can.",
                    french =
                        "Dans cette exp\u00e9rience, vous verrez deux lumi\u00e8res s'approcher de vous.\n\n" +
                        "De temps en temps, vous sentirez une br\u00e8ve vibration sur votre poitrine.\n\n" +
                        "Votre objectif est de:\n" +
                        "  \u2022 Vous concentrer sur les lumi\u00e8res qui approchent.\n" +
                        "  \u2022 R\u00e9pondre aussi rapidement que possible lorsque vous sentez la vibration.\n" +
                        "  \u2022 Appuyer sur le bouton de r\u00e9ponse d\u00e8s que vous d\u00e9tectez le contact.\n\n" +
                        "Gardez la main pr\u00eate sur le bouton et r\u00e9pondez aussi vite et pr\u00e9cis\u00e9ment que possible."
                },
                new()
                {
                    key = "pps.practice_intro",
                    english =
                        "We will start with a few practice trials so you can get used to the lights and the vibration.\n\n" +
                        "These trials will not be recorded.\n\n" +
                        "When you are ready, press Continue.",
                    french =
                        "Nous allons commencer par quelques essais d'entra\u00eenement pour que vous vous habituiez aux lumi\u00e8res et \u00e0 la vibration.\n\n" +
                        "Ces essais ne seront pas enregistr\u00e9s.\n\n" +
                        "Lorsque vous \u00eates pr\u00eat, appuyez sur Continuer."
                },
                new()
                {
                    key = "pps.block_intro",
                    english =
                        "The next block is about to begin.\n\n" +
                        "Stay still, focus on the lights, and respond as quickly as you can when you feel a vibration.\n\n" +
                        "Press Continue when you are ready.",
                    french =
                        "Le prochain bloc va commencer.\n\n" +
                        "Restez immobile, concentrez-vous sur les lumi\u00e8res, et r\u00e9pondez aussi rapidement que possible lorsque vous sentez une vibration.\n\n" +
                        "Appuyez sur Continuer lorsque vous \u00eates pr\u00eat."
                },
                new()
                {
                    key = "pps.rest",
                    english = "Take a short break.\n\nThe next block will begin shortly.",
                    french = "Faites une courte pause.\n\nLe prochain bloc commencera bient\u00f4t."
                },
                new()
                {
                    key = "pps.outro",
                    english = "End of the experiment \u2014 Thank you for participating!",
                    french = "Fin de l'exp\u00e9rience \u2014 Merci d'avoir particip\u00e9 !"
                },
                new()
                {
                    key = "pps.vibration_message",
                    english = "VIBRATION",
                    french = "VIBRATION"
                }
            };

            // Use SerializedObject to set the private m_Entries field
            var so = new SerializedObject(table);
            var entriesProp = so.FindProperty("m_Entries");
            entriesProp.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var elem = entriesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("key").stringValue = entries[i].key;
                elem.FindPropertyRelative("english").stringValue = entries[i].english;
                elem.FindPropertyRelative("french").stringValue = entries[i].french;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = "Assets/HitOrMissTermTable.asset";
            AssetDatabase.CreateAsset(table, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = table;
            Debug.Log($"[DefaultTermTableCreator] Created term table at {path}");
        }
    }
}
#endif
