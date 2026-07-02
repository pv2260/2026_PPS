using UnityEngine;

namespace HitOrMiss
{
    public class FixationCrossController : MonoBehaviour
    {
        [Header("Cross object")]
        [SerializeField] private GameObject m_Cross;

        [Header("Cross renderers")]
        [SerializeField] private Renderer[] m_CrossRenderers;

        [Header("Materials")]
        [SerializeField] private Material m_DefaultMaterial;
        [SerializeField] private Material m_ItiBlackMaterial;

        private void Awake()
        {
            if (m_Cross == null)
                m_Cross = gameObject;

            if (m_CrossRenderers == null || m_CrossRenderers.Length == 0)
                m_CrossRenderers = m_Cross.GetComponentsInChildren<Renderer>(true);

            SetDefaultColor();
            Hide();
        }

        public void Show()
        {
            if (m_Cross != null)
                m_Cross.SetActive(true);
        }

        public void Hide()
        {
            if (m_Cross != null)
                m_Cross.SetActive(false);
        }

        public void SetActive(bool on)
        {
            if (on) Show();
            else Hide();
        }

        public void SetItiColor()
        {
            SetMaterial(m_ItiBlackMaterial);
        }

        public void SetDefaultColor()
        {
            SetMaterial(m_DefaultMaterial);
        }

        private void SetMaterial(Material material)
        {
            if (material == null)
                return;

            if (m_CrossRenderers == null || m_CrossRenderers.Length == 0)
                return;

            foreach (Renderer r in m_CrossRenderers)
            {
                if (r != null)
                    r.sharedMaterial = material;
            }
        }
    }
}