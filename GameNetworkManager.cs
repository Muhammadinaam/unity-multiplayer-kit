// ============================================================
// GameNetworkManager  –  Player spawning + scene management
// ============================================================
// SETUP
//   1. Add to the same GameObject as NetworkManager
//      (replaces MyNetworkManager.cs).
//   2. Assign spawn points in the Inspector (as many as you need).
//   3. Add your player prefab to NetworkManager's Network Prefabs list.
//      The prefab MUST have a PlayerData component on it.
//   4. Enable "Scene Management" in the NetworkManager component.
//
// USAGE  (server / host only)
//   GameNetworkManager.Instance.LoadScene("GameScene");
//   GameNetworkManager.Instance.ReturnToLobby("LobbyScene");
// ============================================================

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    [SerializeField] private List<Transform> spawnPoints = new();

    private NetworkManager nm;
    private NetworkObject  playerPrefab;
    private int            spawnCount;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        nm = GetComponent<NetworkManager>();
        ResolvePlayerPrefab();
    }

    private void OnEnable()  => nm.OnClientConnectedCallback    += SpawnPlayer;
    private void OnDisable() => nm.OnClientConnectedCallback    -= SpawnPlayer;

    // ── Server API ───────────────────────────────────────────────────────────

    public void LoadScene(string sceneName)
    {
        if (!nm.IsServer) return;
        nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void ReturnToLobby(string lobbyScene = "Lobby")
    {
        if (!nm.IsServer) return;
        spawnCount = 0;
        nm.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single);
    }

    // ── Spawn ────────────────────────────────────────────────────────────────

    private void SpawnPlayer(ulong clientId)
    {
        if (!nm.IsServer || playerPrefab == null) return;

        Transform spawn = spawnPoints.Count > 0
            ? spawnPoints[spawnCount % spawnPoints.Count]
            : transform;

        var obj = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        obj.SpawnAsPlayerObject(clientId, destroyWithScene: true);

        // Let PlayerData know which player number this is (1-based).
        if (obj.TryGetComponent<PlayerData>(out var data))
            data.PlayerNumber.Value = spawnCount + 1;

        spawnCount++;
    }

    // ── Prefab resolution ────────────────────────────────────────────────────

    private void ResolvePlayerPrefab()
    {
        foreach (var list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
            foreach (var entry in list.PrefabList)
                if (entry.Prefab != null && entry.Prefab.TryGetComponent<PlayerData>(out _))
                {
                    playerPrefab = entry.Prefab.GetComponent<NetworkObject>();
                    return;
                }

        Debug.LogError("[GameNetworkManager] No prefab with PlayerData found in Network Prefabs list.");
    }
}
