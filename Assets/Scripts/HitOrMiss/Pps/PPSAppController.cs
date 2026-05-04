using UnityEngine;

namespace HitOrMiss.Pps
{
    public class PpsAppController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PpsTaskManager taskManager;

        public void ContinueAfterId()
        {
            Debug.LogError("[PpsAppController] CONTINUE BUTTON DEFINITELY CLICKED");

            if (taskManager == null)
            {
                Debug.LogError("[PpsAppController] taskManager is NULL");
                return;
            }

            Debug.LogError("[PpsAppController] Calling taskManager.StartSession()");
            taskManager.StartSession();
        }
    }
}