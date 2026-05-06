using UnityEngine;

namespace HitOrMiss
{
    /// <summary>
    /// Wraps the fixation cross GameObject the participant looks at between
    /// trials. <see cref="HitOrMissAppController"/> shows it during the
    /// inter-trial interval and hides it while a ball is in flight, per the
    /// PDF spec ("Fixation on the cross / inter-trial-interval").
    ///
    /// You can plug a separate cross here, or reuse the crosshair already
    /// instantiated by <see cref="TrajectoryTaskManager"/> — just drag that
    /// GameObject into <see cref="m_Cross"/>.
    /// </summary>
    public class FixationCrossController : MonoBehaviour
    {
        [SerializeField] GameObject m_Cross;

        void Awake()
        {
            if (m_Cross == null) m_Cross = gameObject;
            Hide();
        }

        public void Show()
        {
            if (m_Cross != null) m_Cross.SetActive(true);
        }

        public void Hide()
        {
            if (m_Cross != null) m_Cross.SetActive(false);
        }

        public void SetActive(bool on)
        {
            if (on) Show();
            else Hide();
        }
    }
}
