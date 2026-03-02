// ============================================================
// NetworkedActions  –  Server-authoritative discrete input
//                      (fire, interact, click, abilities, etc.)
// ============================================================
// SETUP
//   Add alongside NetworkedPlayerMovement on your Player prefab.
//   Override the virtual methods you need, or subscribe to the events.
//
// THREE PATTERNS ARE SHOWN HERE:
//
//   A) FIRE / ATTACK
//      Owner plays cosmetic immediately (sound, muzzle flash).
//      ServerRpc does the real work (spawn bullet, raycast hit).
//      ClientRpc broadcasts a shared visual (hit spark, blood).
//
//   B) WORLD-SPACE CLICK  (click-to-move, skill targeting, RTS)
//      Owner raycasts locally to get the world position.
//      Sends that position to server. Server validates and acts.
//
//   C) TOGGLE / STATE  (reload, interact, open door)
//      Owner requests. Server updates a NetworkVariable.
//      All clients react via OnValueChanged – no ClientRpc needed.
//
// HOW TO USE
//   Option 1 – Subclass:
//     public class SoldierActions : NetworkedActions
//     {
//         protected override void OnFireServer(ulong shooterClientId)
//         {
//             // Spawn a bullet NetworkObject here
//             var bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
//             bullet.GetComponent<NetworkObject>().Spawn();
//         }
//     }
//
//   Option 2 – Subscribe from another script:
//     actions.OnFireLocal     += PlayMuzzleFlash;
//     actions.OnClickWorld    += MoveAgentToPoint;
//     actions.OnInteractLocal += OpenInventory;
// ============================================================

using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkedActions : NetworkBehaviour
{
    [Header("Input Bindings")]
    [SerializeField] private KeyCode fireKey      = KeyCode.Mouse0;
    [SerializeField] private KeyCode interactKey  = KeyCode.E;
    [SerializeField] private bool    enableClickToWorld = false; // for RTS / click-to-move

    // ── AMMO example (Pattern C: NetworkVariable) ─────────────────────────────
    [Header("Ammo (example state)")]
    [SerializeField] private int maxAmmo = 30;

    public NetworkVariable<int> Ammo = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Events – subscribe from UI or other components ────────────────────────

    // Fire on LOCAL owner only (instant cosmetic: sound, flash, camera shake)
    public event Action         OnFireLocal;
    // Interact on LOCAL owner only (open inventory, play click sound)
    public event Action         OnInteractLocal;
    // World-space click position on LOCAL owner (show move marker, aim preview)
    public event Action<Vector3> OnClickWorld;
    // Ammo changed – fires on every client (update ammo UI)
    public event Action<int, int> OnAmmoChanged;  // (current, max)

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        Ammo.OnValueChanged += (_, cur) => OnAmmoChanged?.Invoke(cur, maxAmmo);
        if (IsServer) Ammo.Value = maxAmmo;
    }

    // ── Input (owner only) ────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsOwner) return;

        // Pattern A: Fire
        if (Input.GetKeyDown(fireKey) && Ammo.Value > 0)
        {
            OnFireLocal?.Invoke();        // cosmetic: play locally right away
            FireServerRpc();              // server: spawn bullet / check hit
        }

        // Pattern B: World-space click
        if (enableClickToWorld && Input.GetKeyDown(interactKey))
        {
            if (TryGetWorldClick(out Vector3 worldPos))
            {
                OnClickWorld?.Invoke(worldPos);   // local preview
                WorldClickServerRpc(worldPos);    // server acts on it
            }
        }

        // Pattern C: Interact / toggle (no world position needed)
        if (!enableClickToWorld && Input.GetKeyDown(interactKey))
        {
            OnInteractLocal?.Invoke();    // local sound / animation trigger
            InteractServerRpc();
        }
    }

    // ── RPCs ──────────────────────────────────────────────────────────────────

    // Pattern A
    [ServerRpc]
    private void FireServerRpc()
    {
        if (Ammo.Value <= 0) return;
        Ammo.Value--;
        OnFireServer(OwnerClientId);
        // Tell all clients to play the shared fire effect (muzzle on their screen)
        FireEffectClientRpc();
    }

    // Pattern B
    [ServerRpc]
    private void WorldClickServerRpc(Vector3 worldPosition)
    {
        // Validate: check if position is reachable, inside bounds, etc.
        OnWorldClickServer(OwnerClientId, worldPosition);
    }

    // Pattern C
    [ServerRpc]
    private void InteractServerRpc()
    {
        OnInteractServer(OwnerClientId);
    }

    // Shared visual effect – plays on ALL clients (e.g. muzzle flash everyone sees)
    [ClientRpc]
    private void FireEffectClientRpc()
    {
        // Don't double-play on owner (they already played it in OnFireLocal).
        if (!IsOwner) OnFireEffectAllClients();
    }

    // ── Override these in subclasses ──────────────────────────────────────────

    // Server: spawn bullet, do raycast damage, etc.
    protected virtual void OnFireServer(ulong shooterClientId) { }

    // Server: move NPC, apply ability at position, etc.
    protected virtual void OnWorldClickServer(ulong clientId, Vector3 worldPosition) { }

    // Server: open door, pick up item, etc.
    protected virtual void OnInteractServer(ulong clientId) { }

    // All non-owner clients: play shared fire effect (muzzle seen by others).
    protected virtual void OnFireEffectAllClients() { }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Reload (call from UI button or a key press)
    public void RequestReload()
    {
        if (IsOwner) ReloadServerRpc();
    }

    [ServerRpc]
    private void ReloadServerRpc() => Ammo.Value = maxAmmo;

    // Raycast from camera through mouse position to get world point.
    private static bool TryGetWorldClick(out Vector3 worldPos)
    {
        worldPos = default;
        if (Camera.main == null) return false;

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // 3D world
        if (Physics.Raycast(ray, out var hit3D))    { worldPos = hit3D.point; return true; }

        // 2D world (uncomment if you use Physics2D)
        // var hit2D = Physics2D.GetRayIntersection(ray);
        // if (hit2D.collider != null) { worldPos = hit2D.point; return true; }

        return false;
    }
}
