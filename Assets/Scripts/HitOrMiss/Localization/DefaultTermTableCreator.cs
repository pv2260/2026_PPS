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
