using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Trigger zone component for the Mini Boss fight.
/// Counts how many players are inside the zone.
/// Starts the fight by activating the Mini Boss once all 4 players enter the zone.
/// Note: To make editor testing easier, it will also activate if all currently active players are inside.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MiniBossTriggerZone : NetworkBehaviour
{
    public MiniBossAI miniBoss;
    
    [Tooltip("Number of players required to start the boss fight")]
    public int playersRequired = 4;

    [Tooltip("If true, the boss will deactivate if players leave the area (typically false for boss fights)")]
    public bool deactivateWhenPlayersLeave = false;

    private List<Transform> playersInZone = new List<Transform>();
    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private void Start()
    {
        // Make sure the collider is set to Trigger
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (miniBoss == null)
        {
            miniBoss = FindFirstObjectByType<MiniBossAI>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only run logic on the server in multiplayer mode
        bool auth = !IsNetworkActive || IsServer;
        if (!auth) return;

        Transform playerRoot = GetPlayerRoot(other.transform);
        if (playerRoot != null && !playersInZone.Contains(playerRoot))
        {
            playersInZone.Add(playerRoot);
            CheckActivation();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        bool auth = !IsNetworkActive || IsServer;
        if (!auth) return;

        Transform playerRoot = GetPlayerRoot(other.transform);
        if (playerRoot != null && playersInZone.Contains(playerRoot))
        {
            playersInZone.Remove(playerRoot);
            if (deactivateWhenPlayersLeave && miniBoss != null && miniBoss.IsBossActive)
            {
                // Simple deactivation logic could be added here if needed
            }
        }
    }

    private void CheckActivation()
    {
        if (miniBoss == null || miniBoss.IsBossActive || miniBoss.IsDead) return;

        int activePlayersCount = GetActivePlayersInSceneCount();
        
        // Target is either playersRequired, or the total count of active players in the scene if it is less than playersRequired
        int targetCount = Mathf.Min(playersRequired, activePlayersCount);

        if (targetCount > 0 && playersInZone.Count >= targetCount)
        {
            miniBoss.ActivateBoss();
            Debug.Log($"[MiniBossTriggerZone] All active players ({playersInZone.Count}/{targetCount}) entered the zone. Activating Mini Boss!");
        }
    }

    private int GetActivePlayersInSceneCount()
    {
        int count = 0;
        
        count += FindObjectsByType<LeoPlayer>(FindObjectsSortMode.None).Length;
        count += FindObjectsByType<ArthurPlayer>(FindObjectsSortMode.None).Length;
        count += FindObjectsByType<ElenaPlayer>(FindObjectsSortMode.None).Length;
        count += FindObjectsByType<ElenaArcher>(FindObjectsSortMode.None).Length;
        count += FindObjectsByType<MayaPlayer>(FindObjectsSortMode.None).Length;
        count += FindObjectsByType<MayaSupport>(FindObjectsSortMode.None).Length;
        count += FindObjectsByType<SimplePlayerTest>(FindObjectsSortMode.None).Length;

        return count;
    }

    private Transform GetPlayerRoot(Transform t)
    {
        if (t.GetComponentInParent<LeoPlayer>() != null) return t.GetComponentInParent<LeoPlayer>().transform;
        if (t.GetComponentInParent<ArthurPlayer>() != null) return t.GetComponentInParent<ArthurPlayer>().transform;
        if (t.GetComponentInParent<ElenaPlayer>() != null) return t.GetComponentInParent<ElenaPlayer>().transform;
        if (t.GetComponentInParent<ElenaArcher>() != null) return t.GetComponentInParent<ElenaArcher>().transform;
        if (t.GetComponentInParent<MayaPlayer>() != null) return t.GetComponentInParent<MayaPlayer>().transform;
        if (t.GetComponentInParent<MayaSupport>() != null) return t.GetComponentInParent<MayaSupport>().transform;
        if (t.GetComponentInParent<SimplePlayerTest>() != null) return t.GetComponentInParent<SimplePlayerTest>().transform;
        if (t.GetComponentInParent<Skeleton>() != null) return t.GetComponentInParent<Skeleton>().transform;
        return null;
    }

    private void OnDrawGizmos()
    {
        // Draw the activation zone in the scene view
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.15f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.position, sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
        }
    }
}
