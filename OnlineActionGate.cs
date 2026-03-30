using UnityEngine;
using UnityEngine.Events;

public class OnlineActionGate : MonoBehaviour
{
    [SerializeField] private string offlineErrorMessage = "No network connection.";
    [SerializeField] private UnityEvent onAllowed;
    [SerializeField] private UnityEvent onBlocked;

    public void Execute()
    {
        var gsm = GameServiceManager.Instance;
        if (gsm == null)
        {
            Debug.LogWarning("[OnlineActionGate] GameServiceManager instance is missing.");
            return;
        }

        if (gsm.EnsureOnlineOrShowError(offlineErrorMessage))
            onAllowed?.Invoke();
        else
            onBlocked?.Invoke();
    }
}

