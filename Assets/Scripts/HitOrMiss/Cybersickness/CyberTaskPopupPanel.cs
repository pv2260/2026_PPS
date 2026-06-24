using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitOrMiss.Cybersickness
{
    public enum CyberPopupBehavior
    {
        WaitForAdvance,
        AutoAdvance
    }

    public class CyberTaskPopupPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject m_Root;
        [SerializeField] TMP_Text m_BodyText;
        [SerializeField] Button m_ContinueButton;
        [SerializeField] TMP_Text m_ContinueButtonLabel;

        [Header("Text")]
        [TextArea(3, 10)]
        [SerializeField] string m_BodyFallback = "";

        [SerializeField] string m_ButtonLabel = "Continue";

        [Header("Behavior")]
        [SerializeField] CyberPopupBehavior m_Behavior = CyberPopupBehavior.WaitForAdvance;

        [Tooltip("Only used when behavior is AutoAdvance.")]
        [SerializeField] float m_AutoAdvanceSeconds = 5f;

        [Header("Testing")]
        [SerializeField] bool m_AllowKeyboardAdvance = true;

        bool m_AdvanceRequested;
        bool m_IsRunning;

        public bool IsRunning => m_IsRunning;
        public string BodyFallback => m_BodyFallback;

        void Awake()
        {
            if (m_Root == null)
                m_Root = gameObject;

            if (m_ContinueButton != null)
            {
                m_ContinueButton.onClick.RemoveListener(Advance);
                m_ContinueButton.onClick.AddListener(Advance);
            }

            Hide();
        }

        void Update()
        {
            if (!m_IsRunning) return;
            if (!m_AllowKeyboardAdvance) return;
            if (m_Behavior != CyberPopupBehavior.WaitForAdvance) return;

            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Advance();
            }
        }

        public IEnumerator Run()
        {
            SetText(m_BodyFallback);
            SetButtonLabel(m_ButtonLabel);

            if (m_Behavior == CyberPopupBehavior.WaitForAdvance)
                yield return ShowAndWaitForAdvance();
            else
                yield return ShowForSeconds(m_AutoAdvanceSeconds);
        }

        public IEnumerator Run(string text)
        {
            SetText(text);
            SetButtonLabel(m_ButtonLabel);

            if (m_Behavior == CyberPopupBehavior.WaitForAdvance)
                yield return ShowAndWaitForAdvance();
            else
                yield return ShowForSeconds(m_AutoAdvanceSeconds);
        }

        IEnumerator ShowAndWaitForAdvance()
        {
            m_AdvanceRequested = false;
            Show();

            while (!m_AdvanceRequested)
                yield return null;

            Hide();
        }

        IEnumerator ShowForSeconds(float seconds)
        {
            Show();

            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Hide();
        }

        public void Show()
        {
            if (m_Root == null)
                m_Root = gameObject;

            Debug.LogError($"[CYBER POPUP] Show called on {gameObject.name}. Root = {m_Root.name}");

            // Make sure parent objects are active too.
            Transform t = m_Root.transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                {
                    Debug.LogError($"[CYBER POPUP] Activating parent/root: {t.gameObject.name}");
                    t.gameObject.SetActive(true);
                }

                t = t.parent;
            }

            m_IsRunning = true;

            if (m_ContinueButton != null)
            {
                m_ContinueButton.gameObject.SetActive(
                    m_Behavior == CyberPopupBehavior.WaitForAdvance);
            }
        }

        public void Hide()
        {
            m_IsRunning = false;
            m_AdvanceRequested = false;

            if (m_Root == null)
                m_Root = gameObject;

            // Only hide the popup itself, not the Canvas or PopupPanel parent.
            m_Root.SetActive(false);
        }

        public void SetText(string text)
        {
            if (m_BodyText != null)
                m_BodyText.text = text;
        }

        public void SetButtonLabel(string label)
        {
            if (m_ContinueButtonLabel != null)
                m_ContinueButtonLabel.text = label;
        }

        public void Advance()
        {
            if (!m_IsRunning) return;

            m_AdvanceRequested = true;
        }

        public void ContinueFromController()
        {
            Advance();
        }

        public void ForceAdvance()
        {
            Advance();
        }
    }
}