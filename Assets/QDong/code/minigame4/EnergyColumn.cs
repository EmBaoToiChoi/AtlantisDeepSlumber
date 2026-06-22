using Unity.Netcode;
using UnityEngine;

public class EnergyColumn : NetworkBehaviour
{
    public NetworkVariable<float> charge =
        new NetworkVariable<float>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public float maxCharge = 100;

    public Transform centerPoint;

    public LineRenderer beam;

    public ParticleSystem completeEffect;

    private bool effectPlayed = false;

    private Renderer[] renderers;
    public Transform beamStart;

    public bool IsCompleted()
    {
        return charge.Value >= maxCharge;
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddChargeServerRpc(float amount)
    {
        charge.Value =
            Mathf.Clamp(
                charge.Value + amount,
                0,
                maxCharge
            );

        Debug.Log(
            gameObject.name +
            " Charge = " +
            charge.Value
        );
    }

    void Start()
    {
        renderers =
            GetComponentsInChildren<Renderer>();

        Debug.Log(
            "Found Renderers: " +
            renderers.Length
        );

        if(beam != null)
            beam.enabled = false;
    }

    void Update()
    {
        float percent =
            charge.Value / maxCharge;

        // Đổi màu cột theo năng lượng
        foreach(Renderer r in renderers)
        {
            if(r == null)
                continue;

            r.material.color =
                Color.Lerp(
                    Color.white,
                    Color.cyan,
                    percent
                );
        }

        // Beam
        if(
            beam != null &&
            centerPoint != null
        )
        {
            beam.SetPosition(
                0,
                transform.position
            );

            beam.SetPosition(
                1,
                centerPoint.position
            );

            beam.enabled =
                charge.Value > 0;

            float width =
                Mathf.Lerp(
                    0.05f,
                    0.25f,
                    percent
                );

            beam.startWidth = width;
            beam.endWidth = width;
        }

        beam.SetPosition(
            0,
            beamStart.position
        );

        // Effect khi đầy
        if(
            charge.Value >= maxCharge &&
            !effectPlayed
        )
        {
            effectPlayed = true;

            Debug.Log(
                gameObject.name +
                " COMPLETED!"
            );

            if(completeEffect != null)
            {
                completeEffect.Play();
            }
        }
    }
}