using UnityEngine;
using Unity.Netcode;

public class CrystalSpawner : NetworkBehaviour
{
    public GameObject crystalPrefab; // Kéo Prefab CrystalCore vào đây

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Spawn ngọc ngay khi Server khởi động
            GameObject coreInstance = Instantiate(crystalPrefab, transform.position, Quaternion.identity);
            coreInstance.GetComponent<NetworkObject>().Spawn();
            Debug.Log("[SERVER] Đã spawn ngọc thành công!");
        }
    }
}