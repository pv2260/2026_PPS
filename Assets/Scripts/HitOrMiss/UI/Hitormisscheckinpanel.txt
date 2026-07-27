using TMPro;
using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Periodic participant check-in for the Hit-or-Miss task.
    ///
    /// Shown by HitOrMissAppController when TrajectoryTaskManager raises
    /// CheckInDue, which happens every TrajectoryTaskAsset.CheckInIntervalTrials
    /// completed trials in a main block. The block is already frozen at a clean
    /// trial boundary by then, so this panel does not need to coordinate with
    /// anything; it only displays and waits to be dismissed.
    ///
    /// The point is progress plus a moment to breathe. Telling the participant
    /// where they are in the block is what makes a long block tolerable: without
    /// it, "how much longer" is unanswerable from inside the headset.
    ///
    /// Place it in the scene wherever the other task panels sit. This component
    /// does no positioning of its own.
    /// </summary>
    public class HitOrMissCheckInPanel : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Object toggled on and off. Leave empty to use this GameObject.")]
        [SerializeField] private GameObject m_Root;

        [SerializeField] private TMP_Text m_Text;

        [Header("Message")]
        [Tooltip("{0} = trials completed, {1} = trials in this block.")]
        [TextArea(2, 5)]
        [SerializeField] private string m_TemplateEnglish =
            "All good?\n\nYou are on trial {0} of {1}.\n\nPress a trigger when you are ready to continue.";

        [Tooltip("{0} = trials completed, {1} = trials in this block.")]
        [TextArea(2, 5)]
        [SerializeField] private string m_TemplateFrench =
            "Tout va bien ?\n\nVous êtes à l'essai {0} sur {1}.\n\nAppuyez sur une gâchette quand vous êtes prêt à continuer.";

        private void Awake()
        {
            if (m_Root == null)
                m_Root = gameObject;

            Hide();
        }

        public void Show(int trialsCompleted, int trialsInBlock, SupportedLanguage language)
        {
            string template = language == SupportedLanguage.French
                ? m_TemplateFrench
                : m_TemplateEnglish;

            if (string.IsNullOrEmpty(template))
                template = "All good?\n\nYou are on trial {0} of {1}.";

            if (m_Text != null)
                m_Text.text = string.Format(template, trialsCompleted, trialsInBlock);
            else
                Debug.LogWarning("[HitOrMissCheckInPanel] No TMP_Text assigned; showing an empty panel.", this);

            if (m_Root != null)
                m_Root.SetActive(true);
        }

        public void Hide()
        {
            if (m_Root != null)
                m_Root.SetActive(false);
        }
    }
}