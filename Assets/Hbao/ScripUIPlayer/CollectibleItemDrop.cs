using UnityEngine;
using Unity.Netcode;

public class CollectibleItemDrop : NetworkBehaviour, IInteractableItem
{
    [Header("Item Settings")]
    public string itemName = "Ngoc1";
    public float interactRadius = 2.5f;
    public float InteractRadius => interactRadius;

    [Header("Wood Log Amount")]
    public NetworkVariable<int> woodAmount = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private int _localWoodAmount = 1;
    public int localWoodAmount
    {
        get => _localWoodAmount;
        set
        {
            _localWoodAmount = value;
            UpdateAmountUI();
        }
    }

    private TMPro.TMP_Text uiText;

    [Header("Floating Animation Settings")]
    public float rotationSpeed = 60f;
    public float bobSpeed = 2f;
    public float bobRange = 0.12f;

    private MonoBehaviour localPlayer;
    private PlayerHUDController hud;
    private bool isWithinRange = false;
    private float startY;

    private float GetPlayerHealth()
    {
        if (localPlayer is LeoPlayer leo) return leo.CurrentHealth;
        if (localPlayer is ArthurPlayer arthur) return arthur.CurrentHealth;
        if (localPlayer is ElenaPlayer elena) return elena.CurrentHealth;
        if (localPlayer is MayaPlayer maya) return maya.CurrentHealth;
        return 0f;
    }

    private void SetPendingPickItem(GameObject item)
    {
        if (localPlayer is LeoPlayer leo) leo.pendingPickItem = item;
        else if (localPlayer is ArthurPlayer arthur) arthur.pendingPickItem = item;
        else if (localPlayer is ElenaPlayer elena) elena.pendingPickItem = item;
        else if (localPlayer is MayaPlayer maya) maya.pendingPickItem = item;
        else if (localPlayer is SimplePlayerTest spt) spt.pendingPickItem = item;
    }

    private void PlayPickAnimation()
    {
        if (localPlayer is LeoPlayer leo) leo.PlayAnimation("Pick", 0.1f);
        else if (localPlayer is ArthurPlayer arthur) arthur.PlayAnimation("Idle_Pick", 0.1f);
        else if (localPlayer is ElenaPlayer elena) elena.PlayAnimation("Idle_Pick", 0.1f);
        else if (localPlayer is MayaPlayer maya) maya.PlayAnimation("Idle_Pick", 0.1f);
    }

    private bool TryAddItem(string name)
    {
        if (localPlayer is LeoPlayer leo) return leo.TryAddItem(name, false);
        if (localPlayer is ArthurPlayer arthur) return arthur.TryAddItem(name, false);
        if (localPlayer is ElenaPlayer elena) return elena.TryAddItem(name);
        if (localPlayer is MayaPlayer maya) return maya.TryAddItem(name);
        return false;
    }

    private bool IsPlayerStandalone()
    {
        if (localPlayer is LeoPlayer leo) return leo.isStandaloneMode;
        if (localPlayer is ArthurPlayer arthur) return arthur.isStandaloneMode;
        if (localPlayer is ElenaPlayer elena) return elena.isStandaloneMode;
        if (localPlayer is MayaPlayer maya) return maya.isStandaloneMode;
        if (localPlayer is SimplePlayerTest spt) return spt.isStandaloneMode;
        return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        woodAmount.OnValueChanged += OnWoodAmountChanged;
        UpdateAmountUI();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        woodAmount.OnValueChanged -= OnWoodAmountChanged;
    }

    private void OnWoodAmountChanged(int oldVal, int newVal)
    {
        UpdateAmountUI();
    }

    private void UpdateAmountUI()
    {
        if (!itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) && 
            !itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int amount = IsPlayerStandalone() ? localWoodAmount : woodAmount.Value;

        if (uiText == null)
        {
            Transform existingTextObj = transform.Find("WoodCountUI");
            if (existingTextObj != null)
            {
                uiText = existingTextObj.GetComponent<TMPro.TMP_Text>();
            }
            else
            {
                GameObject uiGo = new GameObject("WoodCountUI");
                uiGo.transform.SetParent(transform, false);
                uiGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);

                var tmpText = uiGo.AddComponent<TMPro.TextMeshPro>();
                tmpText.alignment = TMPro.TextAlignmentOptions.Center;
                tmpText.fontSize = 4f;
                tmpText.color = Color.white;
                
                uiGo.AddComponent<BillboardUI>();

                uiText = tmpText;
            }
        }

        if (uiText != null)
        {
            uiText.text = amount.ToString();
        }
    }

    private void Start()
    {
        startY = transform.position.y;
        UpdateAmountUI();
    }

    private void OnEnable() 
    { 
        InteractionRegistry.Register(this); 
        startY = transform.position.y; 
        UpdateAmountUI();
    }

    private void OnDisable() { InteractionRegistry.Unregister(this); if (isWithinRange && hud != null) hud.ShowInteractionPrompt(false, ""); isWithinRange = false; }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        Vector3 currentPos = transform.position;
        currentPos.y = startY + Mathf.Sin(Time.time * bobSpeed) * bobRange;
        transform.position = currentPos;

        if (localPlayer == null) FindLocalPlayer();

        if (localPlayer != null)
        {
            if (GetPlayerHealth() <= 0)
            {
                if (isWithinRange) { isWithinRange = false; if (hud != null) hud.ShowInteractionPrompt(false, ""); }
                return;
            }

            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);

            if (distance <= interactRadius && IsClosestItem())
            {
                if (!isWithinRange)
                {
                    isWithinRange = true;
                    if (hud == null) hud = FindAnyObjectByType<PlayerHUDController>();
                    if (hud != null) hud.ShowInteractionPrompt(true, "Ấn [F] để nhặt");
                }
                if (Input.GetKeyDown(KeyCode.F)) StartCollecting();
            }
            else if (isWithinRange)
            {
                isWithinRange = false;
                if (hud != null) hud.ShowInteractionPrompt(false, "");
            }
        }
    }

    private bool IsClosestItem()
    {
        if (localPlayer == null) return false;
        float myDist = Vector3.Distance(transform.position, localPlayer.transform.position);
        var activeItems = InteractionRegistry.ActiveItems;
        for (int i = 0; i < activeItems.Count; i++)
        {
            var item = activeItems[i];
            if (ReferenceEquals(item, this) || item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.InteractRadius && dist < myDist) return false;
        }
        return true;
    }

    private void FindLocalPlayer()
    {
        if (PlayerHUDController.LocalPlayerTarget != null) localPlayer = PlayerHUDController.LocalPlayerTarget as MonoBehaviour;
    }

    private void StartCollecting()
    {
        if (localPlayer == null) return;

        if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
        {
            var carrier = localPlayer.GetComponent<PlayerLogCarrier>();
            if (carrier != null && carrier.isCarrying)
            {
                if (hud != null) hud.ShowMissionAlert("Bạn đang bưng một thanh gỗ rồi!", 2.0f);
                return;
            }
            var interaction = localPlayer.GetComponent<PlayerInteraction>();
            if (interaction != null && interaction.isCarryingCore.Value)
            {
                if (hud != null) hud.ShowMissionAlert("Bạn đang cầm ngọc rồi!", 3.0f);
                return;
            }
            var target = localPlayer as IPlayerHUDTarget;
            if (target != null && target.GetActiveWeaponIndex() == 2)
            {
                if (hud != null) hud.ShowMissionAlert("Bạn phải cất vũ khí mới bưng được gỗ!", 3.0f);
                return;
            }
        }

        if (hud != null) hud.ShowInteractionPrompt(false, "");
        SetPendingPickItem(gameObject);
        PlayPickAnimation();
        StartCoroutine(FallbackCollectSequence());
    }

    private System.Collections.IEnumerator FallbackCollectSequence()
    {
        yield return new WaitForSeconds(1.0f);
        if (localPlayer != null)
        {
            GameObject pending = null;
            if (localPlayer is LeoPlayer leo) pending = leo.pendingPickItem;
            else if (localPlayer is ArthurPlayer arthur) pending = arthur.pendingPickItem;
            else if (localPlayer is ElenaPlayer elena) pending = elena.pendingPickItem;
            else if (localPlayer is MayaPlayer maya) pending = maya.pendingPickItem;
            else if (localPlayer is SimplePlayerTest spt) pending = spt.pendingPickItem;

            if (pending == gameObject) { ConfirmCollect(); SetPendingPickItem(null); }
        }
    }

    public void ConfirmCollect()
    {
        if (localPlayer == null) return;

        if (itemName.Equals("WoodLog", System.StringComparison.OrdinalIgnoreCase) || itemName.Equals("ThanhGo", System.StringComparison.OrdinalIgnoreCase))
        {
            int amount = IsPlayerStandalone() ? localWoodAmount : woodAmount.Value;
            if (IsPlayerStandalone())
            {
                var carrier = localPlayer.GetComponent<PlayerLogCarrier>();
                if (carrier == null) carrier = localPlayer.gameObject.AddComponent<PlayerLogCarrier>();
                carrier.CarryLog(amount);
                WoodLogObjectPool.Instance.ReturnToPool(gameObject);
            }
            else
            {
                var netPlayer = localPlayer.GetComponent<NetworkObject>();
                if (netPlayer != null) PickUpWoodLogServerRpc(netPlayer.NetworkObjectId, amount);
            }
            return;
        }

        bool added = TryAddItem(itemName);
        if (added)
        {
            if (IsPlayerStandalone()) Destroy(gameObject);
            else RequestDespawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickUpWoodLogServerRpc(ulong playerNetObjectId, int amount)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNetObj))
        {
            var pInt = playerNetObj.GetComponent<PlayerInteraction>();
            if (pInt != null && pInt.isCarryingCore.Value) return;
        }
        PickUpWoodLogClientRpc(playerNetObjectId, amount);
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned) netObj.Despawn();
        else WoodLogObjectPool.Instance.ReturnToPool(gameObject);
    }

    [ClientRpc]
    private void PickUpWoodLogClientRpc(ulong playerNetObjectId, int amount)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjectId, out var playerNetObj)) return;

        var playerObj = playerNetObj.gameObject;
        var carrier = playerObj.GetComponent<PlayerLogCarrier>();
        if (carrier == null) carrier = playerObj.AddComponent<PlayerLogCarrier>();
        carrier.CarryLog(amount, true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnServerRpc()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned) netObj.Despawn();
        else Destroy(gameObject);
    }
}

public class BillboardUI : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (mainCameraTransform == null)
        {
            if (Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }
            else
            {
                return;
            }
        }

        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
            mainCameraTransform.rotation * Vector3.up);
    }
}