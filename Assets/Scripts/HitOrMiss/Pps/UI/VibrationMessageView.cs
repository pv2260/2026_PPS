using UnityEngine;

namespace HitOrMiss.Pps
{
    /// <summary>
    /// Tiny helper component attached to the "VIBRATION NOW" UI panel.
    /// Wraps SetActive so <see cref="OnScreenVibrationOutput"/> doesn't need to
    /// know about the underlying panel layout.
    /// </summary>
    public class VibrationMessageView : MonoBehaviour
    {
        [SerializeField] GameObject m_Panel;

        void Awake()
        {
            if (m_Panel == null) m_Panel = gameObject;
            Hide();
        }

        public void Show()
        {
            if (m_Panel != null) m_Panel.SetActive(true);
        }

        public void Hide()
        {
            if (m_Panel != null) m_Panel.SetActive(false);
        }
    }
}
