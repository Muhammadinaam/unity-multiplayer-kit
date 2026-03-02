// ============================================================
// PlayerData  –  Per-player synced data
// ============================================================
// SETUP
//   Add to any Player or NPC prefab alongside NetworkObject.
//   Server sets PlayerNumber automatically via GameNetworkManager.
//   Owner sets their own name (loaded from PlayerPrefs) and ready state.
//
// USAGE  (from UI scripts)
//   var data = GetComponent<PlayerData>();
//   data.SetName("Alice");          // owner only
//   data.SetReady(true);            // owner only
//   data.PlayerNumber.Value         // read anywhere
//   data.PlayerName.Value           // read anywhere
//   data.IsReady.Value              // read anywhere
//   data.OnReadyChanged += ...      // subscribe for UI updates
//
// Save player name to PlayerPrefs so it persists across sessions:
//   PlayerPrefs.SetString("PlayerName", "Alice");
// ============================================================

using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    // Server writes after spawn.
    public NetworkVariable<int> PlayerNumber = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Owner writes from UI / prefs.
    public NetworkVariable<FixedString64Bytes> PlayerName = new(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public NetworkVariable<bool> IsReady = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // Subscribe in UI scripts; fires on every client when the value changes.
    public event Action<FixedString64Bytes> OnNameChanged;
    public event Action<bool>               OnReadyChanged;
    public event Action<int>                OnPlayerNumberChanged;

    public override void OnNetworkSpawn()
    {
        PlayerName.OnValueChanged   += (_, v) => OnNameChanged?.Invoke(v);
        IsReady.OnValueChanged      += (_, v) => OnReadyChanged?.Invoke(v);
        PlayerNumber.OnValueChanged += (_, v) => OnPlayerNumberChanged?.Invoke(v);

        // Owner: load saved name from prefs.
        if (IsOwner)
            PlayerName.Value = PlayerPrefs.GetString("PlayerName", $"Player {OwnerClientId + 1}");
    }

    // ── Owner API ─────────────────────────────────────────────────────────────

    public void SetName(string name)
    {
        if (!IsOwner) return;
        PlayerPrefs.SetString("PlayerName", name);
        PlayerName.Value = name;
    }

    public void SetReady(bool ready) { if (IsOwner) IsReady.Value = ready; }

    public void ToggleReady()        { if (IsOwner) IsReady.Value = !IsReady.Value; }

    // ── Display ──────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"P{PlayerNumber.Value} | {PlayerName.Value} | {(IsReady.Value ? "Ready" : "Not Ready")}";
}
