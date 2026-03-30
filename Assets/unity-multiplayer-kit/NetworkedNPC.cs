using Unity.Netcode;
using UnityEngine;

public abstract class NetworkedNPC : NetworkBehaviour
{
    [SerializeField] protected float moveSpeed       = 3f;
    [SerializeField] protected float stoppingDistance = 0.15f;

    // Override with your AI movement logic.
    // Return the desired direction (magnitude is ignored; speed uses moveSpeed).
    // Return Vector3.zero to stand still.
    protected abstract Vector3 GetMoveDirection();

    private void FixedUpdate()
    {
        if (!IsServer) return;

        Vector3 dir = GetMoveDirection();
        if (dir.sqrMagnitude > 0.001f)
            transform.position += dir.normalized * moveSpeed * Time.fixedDeltaTime;
    }

    // ── Server helpers available to subclasses ───────────────────────────────

    // Smoothly move toward a world-space target position.
    protected void MoveToward(Vector3 target)
    {
        if (!IsServer) return;
        Vector3 dir = target - transform.position;
        if (dir.magnitude > stoppingDistance)
            transform.position += dir.normalized * moveSpeed * Time.fixedDeltaTime;
    }

    // Face the direction of movement (2D or 3D).
    protected void FaceDirection(Vector3 dir, bool is2D = false)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        if (is2D)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        else
        {
            transform.forward = dir.normalized;
        }
    }
}
