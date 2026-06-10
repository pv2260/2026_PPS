using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HitOrMiss.Pps
{
    public class PPSGlobalButtonHaptics : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PPSControllerHaptics haptics;

        [Header("Install Settings")]
        [SerializeField] private bool includeInactiveButtons = true;
        [SerializeField] private bool addHoverHaptics = true;
        [SerializeField] private bool addClickHaptics = true;

        private void Awake()
        {
            Install();
        }

        [ContextMenu("Install Button Haptics")]
        public void Install()
        {
            Button[] buttons = GetComponentsInChildren<Button>(includeInactiveButtons);

            foreach (Button button in buttons)
            {
                InstallOnButton(button);
            }

            Debug.Log($"PPSGlobalButtonHaptics installed on {buttons.Length} buttons.");
        }

        private void InstallOnButton(Button button)
        {
            if (button == null)
                return;

            EventTrigger trigger = button.GetComponent<EventTrigger>();

            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            if (addHoverHaptics)
            {
                AddTriggerEntry(
                    trigger,
                    EventTriggerType.PointerEnter,
                    () =>
                    {
                        if (button.interactable)
                            haptics?.PlayHoverHaptic();
                    }
                );
            }

            if (addClickHaptics)
            {
                AddTriggerEntry(
                    trigger,
                    EventTriggerType.PointerClick,
                    () =>
                    {
                        if (button.interactable)
                            haptics?.PlayUIHaptic();
                    }
                );
            }
        }

        private void AddTriggerEntry(
            EventTrigger trigger,
            EventTriggerType eventType,
            UnityEngine.Events.UnityAction callback
        )
        {
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = eventType
            };

            entry.callback.AddListener(_ => callback.Invoke());
            trigger.triggers.Add(entry);
        }
    }
}