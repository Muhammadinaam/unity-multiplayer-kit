using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;

public class GameServiceManager : MonoBehaviour
{

    [SerializeField] private GameObject noConnectionPanel;
    
    private string mainmenuScene = "MainMenu";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private async void Start()
    {
        await RunBootSequenceAsync();
    }

    private async Task RunBootSequenceAsync()
    {
        if(Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowError("No Network Connection");
            return;
        }

        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            ShowError("Failed to initialize services");
            return;
        }

        SceneManager.LoadScene(mainmenuScene);
    }

    public async void OnRetryButtonClicked()
    {
        noConnectionPanel.SetActive(false);
        await RunBootSequenceAsync();
    }

    private void ShowError(string message)
    {
        Debug.LogWarning($"[GameServiceManager] {message}");
        noConnectionPanel.SetActive(true);
    }
}
