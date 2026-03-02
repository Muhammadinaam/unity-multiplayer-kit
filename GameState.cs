// ============================================================
// GameState  –  Server-controlled game state synced to all clients
// ============================================================
// SETUP
//   1. Create an empty GameObject in your persistent scene,
//      add NetworkObject + this component to it.
//   2. Optionally set DontDestroyOnLoad on it so it persists
//      through scene loads (tick it on the NetworkObject component).
//
// USAGE
//   GameState.Instance.SetPhase(GamePhase.InGame);   // server only
//   GameState.Instance.Phase.Value                   // read anywhere
//   GameState.Instance.Phase.OnValueChanged += ...   // subscribe in UI
//
// Add more NetworkVariables below as your game needs them.
// ============================================================

using Unity.Netcode;
using UnityEngine;

public enum GamePhase { Lobby, Starting, InGame, GameOver }

public class GameState : NetworkBehaviour
{
    public static GameState Instance { get; private set; }

    public NetworkVariable<GamePhase> Phase = new(
        GamePhase.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> RoundNumber = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Add your own synced variables below ──────────────────────────────────
    // public NetworkVariable<int> Score = new(0, ...);

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Server API ───────────────────────────────────────────────────────────

    public void SetPhase(GamePhase phase)      { if (IsServer) Phase.Value       = phase; }
    public void SetRound(int round)            { if (IsServer) RoundNumber.Value = round; }

    // Convenience: true when all connected clients' PlayerData say IsReady.
    public bool AllPlayersReady()
    {
        if (!IsServer) return false;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null || !obj.TryGetComponent<PlayerData>(out var data) || !data.IsReady.Value)
                return false;
        }
        return NetworkManager.Singleton.ConnectedClientsList.Count > 0;
    }
}
