using System;
using UnityEngine;

namespace HitOrMiss
{
    [CreateAssetMenu(fileName = "LocalizedTermTable", menuName = "Parkinson/HitOrMiss/Localized Term Table")]
    public class LocalizedTermTable : ScriptableObject
    {
        [SerializeField] LocalizedEntry[] m_Entries = new LocalizedEntry[0];

        public string Get(string key, SupportedLanguage language)
        {
            foreach (var entry in m_Entries)
            {
                if (string.Equals(entry.key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return language switch
                    {
                        SupportedLanguage.French => entry.french,
                        _ => entry.english
                    };
                }
            }
            return $"[{key}]";
        }
    }

    [Serializable]
    public struct LocalizedEntry
    {
        public string key;
        public string english;
        public string french;
    }
}
