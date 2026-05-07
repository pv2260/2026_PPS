using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss
{
    /// <summary>
    /// Behavior of one popup step in the session flow. Selected per-panel from
    /// the Inspector, so the controller doesn't need to know what kind of
    /// popup it's showing — it just calls <see cref="Run"/> and the panel
    /// decides what "showing" means.
    /// </summary>
    public enum PopupBehavior
    {
        /// <summary>Show the panel; wait until the participant clicks the Continue button.</summary>
        WaitForButton,

        /// <summary>Show the panel for a fixed duration, no button needed (intro / outro screens).</summary>
        AutoAdvance,

        /// <summary>Show the panel with a countdown overlay; auto-advance when the timer expires (break screens).</summary>
        BreakWithCountdown,
    }

    /// <summary>
    /// One participant-facing popup panel. Carries its own localization key,
    /// fallback text, button label, and behavior — so the session flow on
    /// <see cref="HitOrMissAppController"/> can iterate an arbitrary array of
    /// these without per-panel branching code.
    ///
    /// Adding a new popup never requires a code change: drop a new
    /// <c>TaskPopupPanel</c> into the scene, fill in its key/fallback/behavior,
    /// drag it into the controller's flow array at the right position.
    /// </summary>
    public class TaskPopupPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject m_Root;
        [SerializeField] TMP_Text m_Body;
        [SerializeField] Button m_ContinueButton;
        [SerializeField] TMP_Text m_ContinueLabel;
        [SerializeField] TMP_Text m_CountdownText;

        [Header("Localization (looked up in the active LocalizedTermTable)")]
        [Tooltip("Key for the body text. Empty = use FallbackText literally.")]
        [SerializeField] string m_BodyKey = "";
        [TextArea(2, 6)]
        [Tooltip("Shown if the term table has no entry for BodyKey. May contain {block} placeholder for block number.")]
        [SerializeField] string m_BodyFallback = "";

        [Tooltip("Key for the Continue button label. Empty = use ButtonLabelFallback literally.")]
        [SerializeField] string m_ButtonLabelKey = "";
        [SerializeField] string m_ButtonLabelFallback = "Continue";

        [Header("Behavior")]
        [SerializeField] PopupBehavior m_Behavior = PopupBehavior.WaitForButton;

        [Tooltip("AutoAdvance / BreakWithCountdown: how long to wait before advancing. Source of the wait depends on m_DurationSource.")]
        [SerializeField] float m_DurationSeconds = 5f;

        [Tooltip("If set, overrides m_DurationSeconds at runtime. Common values: 'BreakDurationSeconds', 'OutroDuration'. The controller resolves these against the active TaskAsset.")]
        [SerializeField] DurationSource m_DurationSource = DurationSource.LiteralSeconds;

        bool m_Pressed;
        bool m_Initialized;

        public string BodyKey => m_BodyKey;
        public string BodyFallback => m_BodyFallback;
        public string ButtonLabelKey => m_ButtonLabelKey;
        public string ButtonLabelFallback => m_ButtonLabelFallback;
        public PopupBehavior Behavior => m_Behavior;
        public float DurationSeconds => m_DurationSeconds;
        public DurationSource DurationSource => m_DurationSource;

        void EnsureInit()
        {
            if (m_Initialized) return;
            m_Initialized = true;
            if (m_Root == null) m_Root = gameObject;
            if (m_ContinueButton != null)
                m_ContinueButton.onClick.AddListener(OnContinue);
        }

        void Awake() => EnsureInit();

        void OnDestroy()
        {
            if (m_ContinueButton != null)
                m_ContinueButton.onClick.RemoveListener(OnContinue);
        }

        // ---- Display ----

        public void Show()
        {
            EnsureInit();
            var target = m_Root != null ? m_Root : gameObject;
            target.SetActive(true);
        }

        public void Hide()
        {
            var target = m_Root != null ? m_Root : gameObject;
            target.SetActive(false);
        }

        public void SetText(string text)
        {
            if (m_Body != null && text != null) m_Body.text = text;
        }

        public void SetButtonLabel(string label)
        {
            if (m_ContinueLabel != null && label != null) m_ContinueLabel.text = label;
        }

        // ---- Unified runner ----

        /// <summary>
        /// Runs this panel according to its configured behavior. Localization
        /// and runtime values are passed in through <paramref name="ctx"/> so
        /// the panel doesn't need to know about app-level systems.
        /// </summary>
        public IEnumerator Run(PopupContext ctx)
        {
            EnsureInit();
            string body = ctx.ResolveText(this);
            string label = ctx.ResolveButtonLabel(this);
            float duration = ctx.ResolveDuration(this);

            SetText(body);
            SetButtonLabel(label);

            switch (m_Behavior)
            {
                case PopupBehavior.WaitForButton:
                    yield return ShowAndWaitForButton();
                    break;
                case PopupBehavior.AutoAdvance:
                    yield return ShowForSeconds(duration, showCountdown: false);
                    break;
                case PopupBehavior.BreakWithCountdown:
                    yield return ShowForSeconds(duration, showCountdown: true);
                    break;
            }
        }

        IEnumerator ShowAndWaitForButton()
        {
            Show();
            m_Pressed = false;
            while (!m_Pressed) yield return null;
            Hide();
        }

        IEnumerator ShowForSeconds(float seconds, bool showCountdown)
        {
            Show();
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (showCountdown && m_CountdownText != null)
                    m_CountdownText.text = FormatCountdown(remaining);
                remaining -= Time.deltaTime;
                yield return null;
            }
            if (showCountdown && m_CountdownText != null) m_CountdownText.text = "";
            Hide();
        }

        // ---- Compatibility wrappers (kept so any external caller still works) ----

        public IEnumerator ShowAndWaitForButton(string text, string buttonLabel = null, float autoAdvanceSeconds = 0f)
        {
            SetText(text);
            if (buttonLabel != null) SetButtonLabel(buttonLabel);
            Show();
            m_Pressed = false;
            float elapsed = 0f;
            while (!m_Pressed)
            {
                if (autoAdvanceSeconds > 0f)
                {
                    elapsed += Time.deltaTime;
                    if (elapsed >= autoAdvanceSeconds) break;
                }
                yield return null;
            }
            Hide();
        }

        public IEnumerator ShowForSeconds(string text, float seconds, bool showCountdown = false)
        {
            SetText(text);
            yield return ShowForSeconds(seconds, showCountdown);
        }

        static string FormatCountdown(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return m > 0 ? $"{m}:{s:D2}" : $"{s}s";
        }

        void OnContinue() => m_Pressed = true;
        public void ForceAdvance() => m_Pressed = true;
    }

    /// <summary>
    /// Where a popup gets its display duration from. Used when the panel's
    /// behavior is AutoAdvance or BreakWithCountdown.
    /// </summary>
    public enum DurationSource
    {
        LiteralSeconds,         // use TaskPopupPanel.DurationSeconds directly
        TaskAssetBreak,         // pull from TrajectoryTaskAsset.BreakDurationSeconds
        TaskAssetOutro,         // pull from TrajectoryTaskAsset.OutroDuration
    }

    /// <summary>
    /// Per-call context handed to <see cref="TaskPopupPanel.Run"/> so the
    /// panel can resolve its localization key and duration source against
    /// the controller's active state without depending on it directly.
    /// </summary>
    public struct PopupContext
    {
        public System.Func<string, string, string> Localize; // (key, fallback) -> resolved text
        public System.Func<float> GetBreakDuration;
        public System.Func<float> GetOutroDuration;
        public int CurrentBlockNumber;                       // 1-based; injected into "{block}" placeholders

        public string ResolveText(TaskPopupPanel p)
        {
            string raw = Localize != null
                ? Localize(p.BodyKey, p.BodyFallback)
                : (string.IsNullOrEmpty(p.BodyFallback) ? "" : p.BodyFallback);
            if (CurrentBlockNumber > 0 && raw != null && raw.Contains("{block}"))
                raw = raw.Replace("{block}", CurrentBlockNumber.ToString());
            return raw;
        }

        public string ResolveButtonLabel(TaskPopupPanel p)
        {
            return Localize != null
                ? Localize(p.ButtonLabelKey, p.ButtonLabelFallback)
                : (string.IsNullOrEmpty(p.ButtonLabelFallback) ? "Continue" : p.ButtonLabelFallback);
        }

        public float ResolveDuration(TaskPopupPanel p)
        {
            return p.DurationSource switch
            {
                DurationSource.TaskAssetBreak => GetBreakDuration != null ? GetBreakDuration() : p.DurationSeconds,
                DurationSource.TaskAssetOutro => GetOutroDuration != null ? GetOutroDuration() : p.DurationSeconds,
                _ => p.DurationSeconds,
            };
        }
    }
}
