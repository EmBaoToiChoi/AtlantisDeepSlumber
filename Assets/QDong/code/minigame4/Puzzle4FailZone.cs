using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class Puzzle4FailZone : NetworkBehaviour
{
    [Header("Respawn")]
    public Transform defaultSpawnPoint;

    public float respawnDelay = 3f;

    [Header("Effect")]
    public GameObject hitEffectPrefab;

    private HashSet<ulong> respawningPlayers =
        new HashSet<ulong>();

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(
            "[FAIL ZONE] Touch = " +
            other.name
        );

        Debug.Log(
            "[FAIL ZONE] Player Entered"
        );

        if (!IsServer)
            return;

        if (!other.CompareTag("Player"))
            return;

        Debug.Log("Player entered fail zone: " + other.name);

        NetworkObject netObj =
            other.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            netObj =
                other.GetComponentInParent<NetworkObject>();
        }

        if (netObj == null)
        {
            Debug.LogWarning("No NetworkObject found");
            return;
        }

        ulong clientId =
            netObj.OwnerClientId;

        if (respawningPlayers.Contains(clientId))
            return;

        respawningPlayers.Add(clientId);

        StartCoroutine(
            RespawnPlayer(
                netObj,
                clientId
            )
        );
    }

    IEnumerator RespawnPlayer(
        NetworkObject playerObj,
        ulong clientId
    )
    {
        Vector3 hitPos =
            playerObj.transform.position;

        if (hitEffectPrefab != null)
        {
            SpawnHitEffectClientRpc(hitPos);
        }

        SetPlayerVisibleClientRpc(
            playerObj.NetworkObjectId,
            false
        );

        yield return new WaitForSeconds(
            respawnDelay
        );

        Vector3 spawnPos =
            GetRespawnPosition();

        playerObj.transform.position =
            spawnPos;

        Rigidbody rb =
            playerObj.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;

            rb.Sleep();
        }

        TeleportPlayerClientRpc(
            playerObj.NetworkObjectId,
            spawnPos
        );

        yield return new WaitForSeconds(
            0.1f
        );

        SetPlayerVisibleClientRpc(
            playerObj.NetworkObjectId,
            true
        );

        respawningPlayers.Remove(
            clientId
        );

        Debug.Log(
            "Respawn Complete: " +
            playerObj.name
        );
    }

    Vector3 GetRespawnPosition()
    {
        Puzzle4Checkpoint checkpoint =
            FindAnyObjectByType<Puzzle4Checkpoint>();

        if (
            checkpoint != null &&
            checkpoint.IsActivated
        )
        {
            return checkpoint.transform.position;
        }

        if (defaultSpawnPoint != null)
        {
            return defaultSpawnPoint.position;
        }

        Debug.LogWarning(
            "No SpawnPoint Found"
        );

        return Vector3.zero;
    }

    [ClientRpc]
    void TeleportPlayerClientRpc(
        ulong objectId,
        Vector3 pos
    )
    {
        if (
            !NetworkManager.Singleton
                .SpawnManager
                .SpawnedObjects
                .TryGetValue(
                    objectId,
                    out NetworkObject netObj
                )
        )
            return;

        CharacterController cc = netObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        netObj.transform.position = pos;

        if (netObj.IsOwner)
        {
            Unity.Netcode.Components.NetworkTransform netTransform = netObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (netTransform != null)
            {
                netTransform.Teleport(pos, netObj.transform.rotation, netObj.transform.localScale);
            }
        }

        if (cc != null) cc.enabled = true;

        Rigidbody rb =
            netObj.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;

            rb.Sleep();
        }
    }

    [ClientRpc]
    void SetPlayerVisibleClientRpc(
        ulong objectId,
        bool visible
    )
    {
        if (
            !NetworkManager.Singleton
                .SpawnManager
                .SpawnedObjects
                .TryGetValue(
                    objectId,
                    out NetworkObject netObj
                )
        )
            return;

        Renderer[] renderers =
            netObj.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }

        Collider[] colliders =
            netObj.GetComponentsInChildren<Collider>(
                true
            );

        foreach (Collider c in colliders)
        {
            c.enabled = visible;
        }
    }

    [ClientRpc]
    void SpawnHitEffectClientRpc(
        Vector3 position
    )
    {
        if (hitEffectPrefab == null)
            return;

        GameObject fx =
            Instantiate(
                hitEffectPrefab,
                position,
                Quaternion.identity
            );

        Destroy(fx, 3f);
    }
}