using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss
{
    /// <summary>
    /// One participant-facing popup panel (PDF popups 1, 5, 6, 7, 8, 9, plus
    /// the practice popups 2-4). Wraps a body TMP_Text and an optional
    /// Continue button so <see cref="HitOrMissAppController"/> can author the
    /// flow as plain coroutines:
    ///
    ///     yield return popup1.ShowAndWaitForButton(text);
    ///     yield return popup7.ShowForSeconds(text, breakSeconds);
    ///
    /// Each popup is one GameObject in the scene with this component on its
    /// root. The Inspector slots wire the panel root, body text, optional
    /// button + label, and (for the break popup) an optional countdown text.
    /// </summary>
    public class TaskPopupPanel : MonoBehaviour
    {
        [SerializeField] GameObject m_Root;
        [SerializeField] TMP_Text m_Body;
        [SerializeField] Button m_ContinueButton;
        [SerializeField] TMP_Text m_ContinueLabel;
        [SerializeField] TMP_Text m_CountdownText;

        bool m_Pressed;
        bool m_Initialized;

        void EnsureInit()
        {
            if (m_Initialized) return;
            m_Initialized = true;
            if (m_Root == null) m_Root = gameObject;
            if (m_ContinueButton != null)
                m_ContinueButton.onClick.AddListener(OnContinue);
        }

        void Awake()
        {
            // Awake only runs if this GameObject is active in the scene at
            // load time. Either way, Show()/Hide() lazy-init via EnsureInit
            // so the panel works whether authored active or inactive. We
            // intentionally do NOT call Hide() here — HitOrMissAppController
            // calls HideAllPopups() in its own Awake to put the scene in a
            // known state, and that path already handles either authoring.
            EnsureInit();
        }

        void OnDestroy()
        {
            if (m_ContinueButton != null)
                m_ContinueButton.onClick.RemoveListener(OnContinue);
        }

        public void Show()
        {
            EnsureInit();
            var target = m_Root != null ? m_Root : gameObject;
            target.SetActive(true);
        }

        public void Hide()
        {
            // No EnsureInit needed — we just need to set inactive. Fall back
            // to the script's own GameObject if Awake never had a chance to
            // populate m_Root.
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

        /// <summary>
        /// Coroutine: shows the panel, waits until the participant clicks the
        /// Continue button (or until <paramref name="autoAdvanceSeconds"/> &gt;
        /// 0 elapses), then hides and returns.
        /// </summary>
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

        /// <summary>
        /// Coroutine: shows the panel for a fixed duration (auto-advance, no
        /// button). Used for break/outro screens. If the panel has a
        /// countdown TMP_Text wired, it gets updated each frame.
        /// </summary>
        public IEnumerator ShowForSeconds(string text, float seconds, bool showCountdown = false)
        {
            SetText(text);
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

        static string FormatCountdown(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return m > 0 ? $"{m}:{s:D2}" : $"{s}s";
        }

        void OnContinue() => m_Pressed = true;

        /// <summary>External method to advance — wire to controller-trigger handlers if needed.</summary>
        public void ForceAdvance() => m_Pressed = true;
    }
}
