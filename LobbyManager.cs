// ============================================================
// LobbyManager  –  Unity Lobby + Relay wrapper
// ============================================================
// SETUP
//   1. Install packages: com.unity.services.lobby  com.unity.services.relay
//   2. Enable Lobby + Relay in the Unity Dashboard (project settings).
//   3. Add this component to your persistent scene object (e.g. NetworkManager GO).
//   4. Make sure UnityTransport is on the same GO as NetworkManager.
//
// USAGE
//   await LobbyManager.Instance.InitializeAsync();          // call once on app start
//   await LobbyManager.Instance.CreateLobbyAsync(false);   // public  match -> host
//   await LobbyManager.Instance.CreateLobbyAsync(true);    // private match -> host
//   LobbyManager.Instance.LobbyCode                        // share this code for private
//   await LobbyManager.Instance.QuickJoinAsync();           // join any public lobby
//   await LobbyManager.Instance.JoinByCodeAsync(code);     // join private lobby
//   LobbyManager.Instance.LeaveLobby();
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string lobbyName = "My Game";

    public Lobby  CurrentLobby  { get; private set; }
    public string LobbyCode     => CurrentLobby?.LobbyCode;
    public bool   IsInitialized { get; private set; }

    public event Action<Lobby>  OnLobbyCreated;
    public event Action<Lobby>  OnLobbyJoined;
    public event Action         OnLobbyLeft;
    public event Action<string> OnError;

    private Coroutine heartbeat;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Init ─────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        IsInitialized = true;
    }

    // ── Create ───────────────────────────────────────────────────────────────

    public async Task CreateLobbyAsync(bool isPrivate)
    {
        try
        {
            var alloc     = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            var relayCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var opts = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayCode) }
                }
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, opts);
            SetRelayHost(alloc);
            NetworkManager.Singleton.StartHost();
            heartbeat = StartCoroutine(HeartbeatLoop());
            OnLobbyCreated?.Invoke(CurrentLobby);
        }
        catch (Exception e) { OnError?.Invoke(e.Message); }
    }

    // ── Join ─────────────────────────────────────────────────────────────────

    public async Task QuickJoinAsync()
    {
        try
        {
            CurrentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            await JoinRelayAndStartClient();
            OnLobbyJoined?.Invoke(CurrentLobby);
        }
        catch (Exception e) { OnError?.Invoke(e.Message); }
    }

    public async Task JoinByCodeAsync(string lobbyCode)
    {
        try
        {
            CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            await JoinRelayAndStartClient();
            OnLobbyJoined?.Invoke(CurrentLobby);
        }
        catch (Exception e) { OnError?.Invoke(e.Message); }
    }

    // ── Leave ────────────────────────────────────────────────────────────────

    public async void LeaveLobby()
    {
        if (CurrentLobby == null) return;
        StopHeartbeat();
        try { await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId); }
        catch { /* already gone */ }
        CurrentLobby = null;
        NetworkManager.Singleton.Shutdown();
        OnLobbyLeft?.Invoke();
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private async Task JoinRelayAndStartClient()
    {
        var relayCode = CurrentLobby.Data["RelayCode"].Value;
        var join      = await RelayService.Instance.JoinAllocationAsync(relayCode);
        SetRelayClient(join);
        NetworkManager.Singleton.StartClient();
    }

    private void SetRelayHost(Allocation a) =>
        NetworkManager.Singleton.GetComponent<UnityTransport>()
            .SetRelayServerData(new RelayServerData(a, "dtls"));

    private void SetRelayClient(JoinAllocation j) =>
        NetworkManager.Singleton.GetComponent<UnityTransport>()
            .SetRelayServerData(new RelayServerData(j, "dtls"));

    private void StopHeartbeat()
    {
        if (heartbeat != null) StopCoroutine(heartbeat);
    }

    private IEnumerator HeartbeatLoop()
    {
        var wait = new WaitForSeconds(25f);
        while (CurrentLobby != null)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
            yield return wait;
        }
    }
}
