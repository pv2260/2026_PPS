using UnityEngine;

namespace HitOrMiss
{
    [CreateAssetMenu(fileName = "LanguageCommandProfile", menuName = "Parkinson/HitOrMiss/Language Command Profile")]
    public class LanguageCommandProfile : ScriptableObject
    {
        [SerializeField] SupportedLanguage m_Language = SupportedLanguage.English;

        [Tooltip("Words/phrases that map to the HIT command")]
        [SerializeField] string[] m_HitAliases = { "hit" };

        [Tooltip("Words/phrases that map to the MISS command")]
        [SerializeField] string[] m_MissAliases = { "miss" };

        public SupportedLanguage Language => m_Language;
        public string[] HitAliases => m_HitAliases;
        public string[] MissAliases => m_MissAliases;

        public SemanticCommand Classify(string transcript)
        {
            if (string.IsNullOrEmpty(transcript))
                return SemanticCommand.None;

            string lower = transcript.Trim().ToLowerInvariant();

            foreach (string alias in m_HitAliases)
            {
                if (lower.Contains(alias.ToLowerInvariant()))
                    return SemanticCommand.Hit;
            }

            foreach (string alias in m_MissAliases)
            {
                if (lower.Contains(alias.ToLowerInvariant()))
                    return SemanticCommand.Miss;
            }

            return SemanticCommand.None;
        }
    }
}
