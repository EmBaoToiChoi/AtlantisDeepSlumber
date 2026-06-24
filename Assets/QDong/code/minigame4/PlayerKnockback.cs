using UnityEngine;
using Unity.Netcode;

public class PlayerKnockback : NetworkBehaviour
{
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Gọi từ Server: gửi lực về đúng Client sở hữu nhân vật này
    public void Launch(Vector3 force)
    {
        if (!IsSpawned) return;

        LaunchClientRpc(force);
    }

    [ClientRpc]
    private void LaunchClientRpc(Vector3 force)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();

        rb.AddForce(
            force,
            ForceMode.Impulse
        );
    }
}