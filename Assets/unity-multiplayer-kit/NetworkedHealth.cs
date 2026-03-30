using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkedHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;

    public NetworkVariable<int> CurrentHealth = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Fires on every client when health changes. Args: (currentHealth, maxHealth)
    public event Action<int, int> OnHealthChanged;
    public event Action           OnDied;

    public bool IsDead     => CurrentHealth.Value <= 0;
    public int  MaxHealth  => maxHealth;

    public override void OnNetworkSpawn()
    {
        CurrentHealth.OnValueChanged += (_, current) =>
        {
            OnHealthChanged?.Invoke(current, maxHealth);
            if (current <= 0) OnDied?.Invoke();
        };

        if (IsServer) CurrentHealth.Value = maxHealth;
    }

    // ── Anyone can request damage ─────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int amount)
    {
        if (IsDead) return;
        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - amount);
    }

    // ── Server-only ───────────────────────────────────────────────────────────

    public void Heal(int amount)
    {
        if (!IsServer) return;
        CurrentHealth.Value = Mathf.Min(maxHealth, CurrentHealth.Value + amount);
    }

    public void Revive(int withHealth = -1)
    {
        if (!IsServer) return;
        CurrentHealth.Value = withHealth < 0 ? maxHealth : Mathf.Clamp(withHealth, 1, maxHealth);
    }

    public void Kill()
    {
        if (!IsServer) return;
        CurrentHealth.Value = 0;
    }
}
