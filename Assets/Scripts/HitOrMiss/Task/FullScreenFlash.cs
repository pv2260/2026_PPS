using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss
{
    /// <summary>
    /// Fullscreen color flash overlay used for green/red correctness feedback
    /// during the practice phases. Sits on its own Screen Space - Overlay
    /// Canvas with a single full-rect Image; the Image starts transparent and
    /// briefly flashes the chosen color then fades back out.
    ///
    /// The controller enables/disables this only during the active practice
    /// phases (easy + difficult). The main task never flashes — feedback is
    /// suppressed by spec.
    ///
    /// Setup in scene:
    ///   • GameObject with a Canvas (Screen Space - Overlay, sort order high)
    ///   • Child Image stretched to fill (anchor min 0,0; max 1,1; offsets 0)
    ///     with a white sprite or no sprite; color alpha set to 0 in editor
    ///   • This component on the root; drag the Image into m_OverlayImage
    /// </summary>
    public class FullScreenFlash : MonoBehaviour
    {
        [Header("Overlay")]
        [Tooltip("Full-rect Image used as the flash surface. Anchor it to fill the canvas.")]
        [SerializeField] Image m_OverlayImage;

        [Header("Colors (alpha controls peak opacity)")]
        [SerializeField] Color m_CorrectColor   = new Color(0.20f, 0.85f, 0.20f, 0.50f);
        [SerializeField] Color m_IncorrectColor = new Color(0.95f, 0.20f, 0.20f, 0.50f);

        [Header("Timing")]
        [Tooltip("How long the flash stays visible at full opacity (seconds). Snaps off at the end — no fade.")]
        [SerializeField] float m_FlashSeconds = 0.20f;

        Coroutine m_FlashCoroutine;

        void Awake()
        {
            if (m_OverlayImage == null)
                m_OverlayImage = GetComponentInChildren<Image>(includeInactive: true);

            if (m_OverlayImage != null)
            {
                // Start invisible. We don't disable the GameObject — toggling
                // the Image's color alpha is cheaper and avoids canvas
                // rebuilds on every flash.
                var c = m_OverlayImage.color;
                c.a = 0f;
                m_OverlayImage.color = c;
                m_OverlayImage.raycastTarget = false; // never block input
            }
            else
            {
                Debug.LogWarning("[FullScreenFlash] No Image assigned and none found in children — flashes will not display.");
            }
        }

        /// <summary>Flash green (participant got the trial right).</summary>
        public void FlashCorrect() => Flash(m_CorrectColor);

        /// <summary>Flash red (participant got the trial wrong, or didn't respond).</summary>
        public void FlashIncorrect() => Flash(m_IncorrectColor);

        /// <summary>Flash an arbitrary color (advanced).</summary>
        public void Flash(Color color)
        {
            if (m_OverlayImage == null) return;
            if (m_FlashCoroutine != null) StopCoroutine(m_FlashCoroutine);
            m_FlashCoroutine = StartCoroutine(FlashRoutine(color));
        }

        IEnumerator FlashRoutine(Color color)
        {
            // Snap to full opacity, hold solid for m_FlashSeconds, snap off.
            // No fade — keeps the cue crisp and unambiguous.
            m_OverlayImage.color = color;

            if (m_FlashSeconds > 0f)
                yield return new WaitForSeconds(m_FlashSeconds);

            m_OverlayImage.color = new Color(color.r, color.g, color.b, 0f);
            m_FlashCoroutine = null;
        }
    }
}