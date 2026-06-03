using UnityEngine;
using Unity.Netcode;

public class CrystalSpawner : NetworkBehaviour
{
    public GameObject crystalPrefab;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Kiểm tra xem prefab đã có NetworkObject chưa
            GameObject coreInstance = Instantiate(crystalPrefab, transform.position, Quaternion.identity);
            NetworkObject netObj = coreInstance.GetComponent<NetworkObject>();
            
            // Spawn vật thể và cho phép Client thấy
            netObj.Spawn(true); 
            Debug.Log("[SERVER] Đã spawn ngọc!");
        }
    }
}