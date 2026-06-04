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
    
    // --- KHAI BÁO 4 BIẾN ĐỂ TÓM 4 SCRIPT ---
    private MonoBehaviour playerScript1;
    private MonoBehaviour playerScript2;
    private MonoBehaviour playerScript3;
    private MonoBehaviour playerScript4;

    void Update()
    {
        if (Application.isBatchMode) return;

        if (isPlayerInside && localPlayerInteraction != null)
        {
            if (localPlayerInteraction.IsOwner && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                bool isHoldingCore = localPlayerInteraction.isCarryingCore.Value && localPlayerInteraction.currentHeldCore != null;

                if (isHoldingCore && (stationIndex == 2 || stationIndex == 3) && !isCrystalLocked.Value)
                {
                    SnapAndLockCrystalServerRpc(stationIndex);
                }
                else if (gameManager != null)
                {
                    if (!isUsingStation) OpenStation();
                    else ExitStation();
                }
            }
        }
    }

    private void OpenStation()
    {
        if (gameManager == null) return;
        isUsingStation = true;
        gameManager.ToggleMiniGame(stationIndex, true);

        // KHOÁ GIÒ TẤT CẢ NHỮNG SCRIPT NÀO TỒN TẠI
        if (playerScript1 != null) playerScript1.enabled = false;
        if (playerScript2 != null) playerScript2.enabled = false;
        if (playerScript3 != null) playerScript3.enabled = false;
        if (playerScript4 != null) playerScript4.enabled = false;
    }

    private void ExitStation()
    {
        if (gameManager == null) return;
        isUsingStation = false;
        gameManager.ToggleMiniGame(stationIndex, false);

        // MỞ KHOÁ LẠI CHO CHÚNG NÓ HOẠT ĐỘNG
        if (playerScript1 != null) playerScript1.enabled = true;
        if (playerScript2 != null) playerScript2.enabled = true;
        if (playerScript3 != null) playerScript3.enabled = true;
        if (playerScript4 != null) playerScript4.enabled = true;
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SnapAndLockCrystalServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        if (isCrystalLocked.Value) return; 

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client) && client.PlayerObject != null)
        {
            var playerInt = client.PlayerObject.GetComponent<PlayerInteraction>();
            if (playerInt != null && playerInt.currentHeldCore != null)
            {
                var core = playerInt.currentHeldCore;
                playerInt.ForceDropFromStation(); 
                core.LockToStation();
                
                var snapFollow = core.GetComponent<CrystalSnapFollow>();
                if (snapFollow != null)
                {
                    snapFollow.targetSnapPoint = crystalSnapPoint;
                }
                
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

            // --- ĐIỀN TÊN 4 SCRIPT CỦA MÀY VÀO TRONG DẤU <> ---
            playerScript1 = other.GetComponent<LeoPlayer>(); 
            //playerScript2 = other.GetComponent<Ten_Script_So_2>(); 
            //playerScript3 = other.GetComponent<Ten_Script_So_3>(); 
            //playerScript4 = other.GetComponent<Ten_Script_So_4>(); 
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

            // XÓA DỮ LIỆU ĐỂ GIẢI PHÓNG BỘ NHỚ
            playerScript1 = null;
            playerScript2 = null;
            playerScript3 = null;
            playerScript4 = null;
        }
    }
}