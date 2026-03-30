using System;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;

public class GameServiceManager : MonoBehaviour
{
    public static string PendingErrorMessage;
    public static event Action<string> OnErrorRequested;

    public static GameServiceManager Instance {get; private set;}
    private string mainmenuScene = "MainMenu";

    private void Awake()
    {
        if(Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await RunBootSequenceAsync();
    }

    private async Task RunBootSequenceAsync()
    {
        if (!HasInternetConnection())
        {
            ShowError("No network connection.");
            return;
        }

        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            ShowError("Failed to initialize online services.");
            return;
        }

        StartCoroutine(MonitorConnectionAsync());
        SceneManager.LoadScene(mainmenuScene);
    }

    private IEnumerator MonitorConnectionAsync()
    {
        var wait = new WaitForSeconds(10f);
        while(true)
        {
            yield return wait;
            if (!HasInternetConnection())
            {
                HandleConnectionLost();
                yield break;
            }
        }
    }

    public void HandleConnectionLost()
    {
        StopAllCoroutines();

        if(LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
        {
            LobbyManager.Instance.LeaveLobby();
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        ShowError("Lost connection to server.");
        SceneManager.LoadScene("Login");
    }

    public async void OnRetryButtonClicked()
    {
        PendingErrorMessage = null;
        await RunBootSequenceAsync();
    }

    public bool EnsureOnlineOrShowError(string offlineMessage = "No network connection.")
    {
        if (HasInternetConnection()) return true;
        ShowError(offlineMessage);
        return false;
    }

    public bool HasInternetConnection()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }

    private void ShowError(string message)
    {
        Debug.LogWarning($"[GameServiceManager] {message}");
        PendingErrorMessage = message;
        OnErrorRequested?.Invoke(message);
    }
}
