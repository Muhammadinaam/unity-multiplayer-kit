// ============================================================
// NetworkedPlayerMovement  –  Generic server-auth movement
//                             with naive client prediction
// ============================================================
// SETUP
//   Add to any Player prefab alongside:
//     - NetworkObject
//     - NetworkTransform  (set authority to Server)
//     - PlayerData        (optional – not required)
//
// INSPECTOR
//   Move Speed          – units per second
//   Movement Mode       – pick the axis layout for your game
//   Input Send Threshold– min axis delta before sending an RPC
//                         (reduces bandwidth, keep at ~0.01)
//
// HOW PREDICTION WORKS
//   Owner applies movement locally the same frame input is read
//   (zero perceived latency). The server also moves authoritatively
//   and NetworkTransform pushes corrections back; NGO's built-in
//   interpolation smooths any tiny drift invisibly.
//   Hosts (IsServer + IsOwner) skip prediction since they have
//   no network latency.
// ============================================================

using Unity.Netcode;
using UnityEngine;

public enum MovementMode
{
    Horizontal,  // X axis only            (side-scroller lane / pong horizontal)
    Vertical,    // Y axis only            (pong vertical)
    TopDown2D,   // X + Y axes             (2D overhead — RPG, shooter)
    TopDown3D,   // X + Z axes (Horizontal/Vertical mapped to XZ)
}

public class NetworkedPlayerMovement : NetworkBehaviour
{
    [SerializeField] private float        moveSpeed          = 5f;
    [SerializeField] private MovementMode movementMode       = MovementMode.TopDown2D;
    [SerializeField] private float        inputSendThreshold = 0.01f;

    private Vector2 lastSentInput;
    private Vector2 serverInput;

    // ── Owner: read input, predict locally, send RPC on change ───────────────

    private void Update()
    {
        if (!IsOwner) return;

        Vector2 raw = ReadInput();

        // Predict on the client — skip if we ARE the server (host has no latency).
        if (!IsServer)
            ApplyMovement(raw, Time.deltaTime);

        if (Vector2.Distance(raw, lastSentInput) >= inputSendThreshold)
        {
            lastSentInput = raw;
            MoveServerRpc(raw);
        }
    }

    // ── Server: apply authoritatively every physics tick ─────────────────────

    private void FixedUpdate()
    {
        if (!IsServer) return;
        ApplyMovement(serverInput, Time.fixedDeltaTime);
    }

    // ── Shared movement math ─────────────────────────────────────────────────

    private void ApplyMovement(Vector2 input, float dt)
    {
        Vector3 move = movementMode switch
        {
            MovementMode.Horizontal => new Vector3(input.x, 0f, 0f),
            MovementMode.Vertical   => new Vector3(0f, input.y, 0f),
            MovementMode.TopDown2D  => new Vector3(input.x, input.y, 0f),
            MovementMode.TopDown3D  => new Vector3(input.x, 0f, input.y),
            _                       => Vector3.zero,
        };

        transform.position += move * moveSpeed * dt;
    }

    private Vector2 ReadInput() => movementMode switch
    {
        MovementMode.Horizontal => new Vector2(Input.GetAxis("Horizontal"), 0f),
        MovementMode.Vertical   => new Vector2(0f, Input.GetAxis("Vertical")),
        MovementMode.TopDown2D  => new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")),
        MovementMode.TopDown3D  => new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")),
        _                       => Vector2.zero,
    };

    // ── RPC ──────────────────────────────────────────────────────────────────

    [ServerRpc]
    private void MoveServerRpc(Vector2 input) => serverInput = input;
}
