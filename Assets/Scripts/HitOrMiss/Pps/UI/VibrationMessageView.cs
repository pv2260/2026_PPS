using UnityEngine;

namespace HitOrMiss.Pps
{
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