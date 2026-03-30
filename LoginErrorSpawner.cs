using UnityEngine;

public class LoginErrorSpawner : MonoBehaviour
{
    [SerializeField] private ErrorPanel errorPanelPrefab;
    [SerializeField] private Transform parentOverride;

    private ErrorPanel instance;

    private void OnEnable()
    {
        GameServiceManager.OnErrorRequested += HandleErrorRequested;
    }

    private void OnDisable()
    {
        GameServiceManager.OnErrorRequested -= HandleErrorRequested;
    }

    private void Start()
    {
        // In case an error was queued before this scene finished loading.
        if (!string.IsNullOrWhiteSpace(GameServiceManager.PendingErrorMessage))
        {
            EnsureInstance();
            instance.Show(GameServiceManager.PendingErrorMessage);
            Debug.Log("Showing error panel: " + GameServiceManager.PendingErrorMessage);
            GameServiceManager.PendingErrorMessage = null;
        }
    }

    private void HandleErrorRequested(string message)
    {
        if (!isActiveAndEnabled) return;
        EnsureInstance();
        instance.Show(message);
    }

    private void EnsureInstance()
    {
        if (instance != null) return;
        if (errorPanelPrefab == null)
        {
            Debug.LogWarning("[LoginErrorSpawner] No ErrorPanel prefab assigned.");
            return;
        }

        var parent = parentOverride != null ? parentOverride : transform;
        instance = Instantiate(errorPanelPrefab, parent);

        if (GameServiceManager.Instance != null)
            instance.BindRetry(GameServiceManager.Instance.OnRetryButtonClicked);
    }
}

