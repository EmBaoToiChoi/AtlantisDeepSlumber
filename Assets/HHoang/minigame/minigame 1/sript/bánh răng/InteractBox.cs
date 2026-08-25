using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class InteractBox : NetworkBehaviour
{
    public int stationIndex = 0; 
    public Transform crystalSnapPoint;
    [SerializeField] private OptimizedNetworkMiniGame gameManager; 
    
    private bool isPlayerInside = false;
    private bool isUsingStation = false;
    public NetworkVariable<bool> isCrystalLocked = new NetworkVariable<bool>(false);
    
    private PlayerInteraction localPlayerInteraction;
    private NetworkBehaviour localPlayerController;

    public void TrySnapCrystal()
    {
        if ((stationIndex == 2 || stationIndex == 3) && !isCrystalLocked.Value)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                SnapAndLockCrystalServerRpc(stationIndex);
            }
        }
    }

    void Update()
    {
        if (Application.isBatchMode || localPlayerInteraction == null) return;

        if (localPlayerInteraction.IsOwner && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            // Trạm 2 và 3 là các bệ đặt ngọc
            if ((stationIndex == 2 || stationIndex == 3) && !isCrystalLocked.Value)
            {
                string ngocItem = GetNgocFromInventory(localPlayerInteraction);
                if (!string.IsNullOrEmpty(ngocItem))
                {
                    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                    {
                        PlaceCrystalOffline(ngocItem);
                    }
                    else
                    {
                        PlaceCrystalServerRpc(stationIndex, ngocItem);
                    }
                    return; // Đặt ngọc thành công, không mở Panel
                }
            }

            if (localPlayerInteraction.isCarryingCore.Value) return;

            if (gameManager != null)
            {
                if (!isUsingStation) OpenStation();
                else ExitStation();
            }
        }
    }

    private void OpenStation()
    {
        if (gameManager == null) return;

        ulong currentOwner = gameManager.GetOwner(stationIndex);
        if (currentOwner != ulong.MaxValue && currentOwner != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"Trạm {stationIndex} đã có người xài, chặn lệnh mở Canvas!");
            return; 
        }

        isUsingStation = true;
        gameManager.ToggleMiniGame(stationIndex, true);
        ShowPromptForStation(false);

        if (localPlayerController != null) 
        {
            var mover = localPlayerController.GetComponent<MovementController>();
            if(mover != null) mover.ToggleMovement(false);
        }
    }

    private void ExitStation()
    {
        if (gameManager == null) return;
        isUsingStation = false;
        gameManager.ToggleMiniGame(stationIndex, false);
        if (isPlayerInside) ShowPromptForStation(true);

        if (localPlayerController != null) 
        {
            var mover = localPlayerController.GetComponent<MovementController>();
            if(mover != null) mover.ToggleMovement(true);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        if (isCrystalLocked.Value) return; 

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client) && client.PlayerObject != null)
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            
            ulong netId = playerInt.heldCoreNetworkId.Value;
            if (netId != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(netId, out var netObj))
            {
                var core = netObj.GetComponent<CrystalCore>();
                
                playerInt.ForceDropFromStation(); 
                core.LockToStation();
                
                core.transform.position = crystalSnapPoint.position;
                core.transform.rotation = crystalSnapPoint.rotation;
                
                var snapFollow = core.GetComponent<CrystalSnapFollow>();
                if (snapFollow != null) snapFollow.targetSnapPoint = crystalSnapPoint;
                
                isCrystalLocked.Value = true;
                gameManager.SetStationCrystalStatus(index, true); 
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.IsOwner)
        {
            isPlayerInside = true;
            localPlayerInteraction = pInt;
            pInt.currentInteractBox = this; 
            localPlayerController = other.GetComponent<NetworkBehaviour>(); 
            ShowPromptForStation(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerInteraction>(out var pInt) && pInt.IsOwner)
        {
            if (isUsingStation) ExitStation();
            isPlayerInside = false;
            
            if (pInt.currentInteractBox == this) pInt.currentInteractBox = null;
            localPlayerInteraction = null;
            localPlayerController = null;
            ShowPromptForStation(false);
        }
    }

    private void ShowPromptForStation(bool show)
    {
        var hud = PlayerHUDController.Instance != null ? PlayerHUDController.Instance : FindAnyObjectByType<PlayerHUDController>();
        if (hud == null) return;

        if (!show)
        {
            hud.ShowInteractionPrompt(false, "");
            return;
        }

        string promptText = "Ấn [F] để vận hành bánh răng";
        if ((stationIndex == 2 || stationIndex == 3) && !isCrystalLocked.Value)
        {
            string ngocItem = (localPlayerInteraction != null) ? GetNgocFromInventory(localPlayerInteraction) : null;
            if (!string.IsNullOrEmpty(ngocItem))
            {
                promptText = "Ấn [F] để đặt ngọc vào trạm";
            }
            else
            {
                promptText = "Cần đặt ngọc | Ấn [F] để kích hoạt";
            }
        }
        hud.ShowInteractionPrompt(true, promptText);
    }

    private string GetNgocFromInventory(PlayerInteraction playerInt)
    {
        var target = playerInt.GetComponent<IPlayerHUDTarget>();
        if (target != null && target.InventorySlots != null)
        {
            foreach (var slot in target.InventorySlots)
            {
                if (string.IsNullOrEmpty(slot)) continue;
                string itemName = slot.Split(':')[0];
                if (itemName == "Ngoc1" || itemName == "Ngoc2")
                {
                    return itemName;
                }
            }
        }
        return null;
    }

    private void PlaceCrystalOffline(string ngocItem)
    {
        ConsumeNgocFromInventory(ngocItem);

        int crystalID = (ngocItem == "Ngoc1") ? 1 : 2;
        GameObject prefab = Resources.Load<GameObject>($"Crystal_Prefab_{crystalID}");
        if (prefab == null) prefab = Resources.Load<GameObject>("Crystal_Default");

        if (prefab != null)
        {
            GameObject spawned = Instantiate(prefab, crystalSnapPoint.position, crystalSnapPoint.rotation);
            var core = spawned.GetComponent<CrystalCore>();
            if (core != null)
            {
                core.crystalID = crystalID;
                core.isSnapped.Value = true;
                core.isSnapping.Value = true;
                var rb = spawned.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                var colliders = spawned.GetComponentsInChildren<Collider>();
                foreach (var col in colliders) col.enabled = false;
            }
            
            var snapFollow = spawned.GetComponent<CrystalSnapFollow>();
            if (snapFollow != null) snapFollow.targetSnapPoint = crystalSnapPoint;
        }

        isCrystalLocked.Value = true;
        if (gameManager != null)
        {
            gameManager.SetStationCrystalStatus(stationIndex, true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceCrystalServerRpc(int index, string ngocItem, ServerRpcParams rpcParams = default)
    {
        if (isCrystalLocked.Value) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            if (playerInt == null) return;

            ConsumeNgocFromInventoryClientRpc(clientId, ngocItem);

            int crystalID = (ngocItem == "Ngoc1") ? 1 : 2;
            GameObject prefab = Resources.Load<GameObject>($"Crystal_Prefab_{crystalID}");
            if (prefab == null) prefab = Resources.Load<GameObject>("Crystal_Default");

            if (prefab != null)
            {
                GameObject spawned = Instantiate(prefab, crystalSnapPoint.position, crystalSnapPoint.rotation);
                var core = spawned.GetComponent<CrystalCore>();
                if (core != null)
                {
                    core.crystalID = crystalID;
                    core.isSnapped.Value = true;
                    core.isSnapping.Value = true;
                    var rb = spawned.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;
                    var colliders = spawned.GetComponentsInChildren<Collider>();
                    foreach (var col in colliders) col.enabled = false;
                }

                var netObj = spawned.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }

                var snapFollow = spawned.GetComponent<CrystalSnapFollow>();
                if (snapFollow != null) snapFollow.targetSnapPoint = crystalSnapPoint;
            }

            isCrystalLocked.Value = true;
            gameManager.SetStationCrystalStatus(index, true);
        }
    }

    [ClientRpc]
    private void ConsumeNgocFromInventoryClientRpc(ulong clientID, string ngocItem)
    {
        if (NetworkManager.Singleton.LocalClientId == clientID)
        {
            ConsumeNgocFromInventory(ngocItem);
        }
    }

    private void ConsumeNgocFromInventory(string ngocItem)
    {
        PlayerInteraction playerInt = null;
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            playerInt = PlayerHUDController.LocalPlayerTarget.gameObject.GetComponent<PlayerInteraction>();
        }
        if (playerInt == null) return;

        var target = playerInt.GetComponent<IPlayerHUDTarget>();
        if (target != null && target.InventorySlots != null)
        {
            string[] slots = target.InventorySlots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (string.IsNullOrEmpty(slots[i])) continue;
                string itemName = slots[i].Split(':')[0];
                int count = 1;
                if (slots[i].Contains(":"))
                {
                    int.TryParse(slots[i].Split(':')[1], out count);
                }

                if (itemName == ngocItem)
                {
                    if (count > 1)
                    {
                        slots[i] = itemName + ":" + (count - 1);
                    }
                    else
                    {
                        slots[i] = "";
                    }
                    break;
                }
            }

            PlayerHUDController hud = FindAnyObjectByType<PlayerHUDController>();
            if (hud != null)
            {
                hud.SetInventorySlots(slots);
            }

            target.SavePlayerStateToDatabase();
        }
    }
}