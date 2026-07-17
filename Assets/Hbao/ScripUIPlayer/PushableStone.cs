using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PushableStone : NetworkBehaviour
{
    [Header("Push Slots Configuration")]
    [Tooltip("Danh sách 4 vị trí đẩy ở phía sau cục đá (các Child GameObject)")]
    public Transform[] pushSlots = new Transform[4];

    [Header("Stone Movement Configuration")]
    [Tooltip("Tốc độ đẩy đá di chuyển")]
    public float pushSpeed = 1.5f;
    [Tooltip("Số lượng player cần thiết cùng đẩy để đá di chuyển (1 để test, 4 cho game chính)")]
    public int requiredPushers = 1;
    [Tooltip("Khoảng cách tối đa để tương tác hiển thị gợi ý")]
    public float interactRadius = 2.5f;

    [Header("Effects Configuration")]
    [Tooltip("Hệ thống hạt bụi dưới chân đá khi đẩy")]
    public ParticleSystem dustParticleEffect;

    [Header("Network Synchronization")]
    public NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isMovingNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isFinishedNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Slots occupancy synchronized across the network
    public NetworkVariable<ulong> slot0PlayerNetId = new NetworkVariable<ulong>(0);
    public NetworkVariable<ulong> slot1PlayerNetId = new NetworkVariable<ulong>(0);
    public NetworkVariable<ulong> slot2PlayerNetId = new NetworkVariable<ulong>(0);
    public NetworkVariable<ulong> slot3PlayerNetId = new NetworkVariable<ulong>(0);

    // Standalone / Local Mode variables
    private GameObject[] localPushers = new GameObject[4];
    private bool localFinished = false;
    private bool localMoving = false;

    // Track locked player scripts for lookup and cleaning
    private Dictionary<ulong, GameObject> activePusherObjects = new Dictionary<ulong, GameObject>();

    // Inputs tracked on server
    private bool[] pusherInputs = new bool[4];

    private MonoBehaviour localPlayer;
    private PlayerHUDController hud;
    private bool wasInRangeOfAnySlot = false;
    private int activeSlotIndex = -1;
    private bool lastInputW = false;

    private void Start()
    {
        // Kiểm tra xem vật thể có bị đánh dấu là Static trong Inspector không
        if (gameObject.isStatic)
        {
            Debug.LogError($"[PushableStone] LỖI CỰC KỲ NGHIÊM TRỌNG: Vật thể '{gameObject.name}' đang bị đánh dấu là STATIC trong Inspector! " +
                           $"Điều này kích hoạt Static Batching khiến hình ảnh của đá bị khóa cứng tại chỗ trong khi Collider vật lý di chuyển đi nơi khác, làm người chơi đi xuyên qua đá. " +
                           $"Hãy BỎ TÍCH CHỌN ô 'Static' ở góc trên bên phải của vật thể này và tất cả các đối tượng con của nó trong Unity Inspector.");
        }

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            netPosition.Value = transform.position;
        }

        // Tự động tìm ParticleSystem con nếu chưa được gán
        if (dustParticleEffect == null)
        {
            ParticleSystem[] childSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in childSystems)
            {
                string nameLower = ps.name.ToLower();
                if (nameLower.Contains("smoke") || nameLower.Contains("dust") || nameLower.Contains("particle"))
                {
                    dustParticleEffect = ps;
                    Debug.Log($"[PushableStone] Tự động gán ParticleSystem con làm hiệu ứng bụi: '{ps.name}' trên '{gameObject.name}'");
                    break;
                }
            }

            if (dustParticleEffect == null && childSystems.Length > 0)
            {
                dustParticleEffect = childSystems[0];
                Debug.Log($"[PushableStone] Tự động gán ParticleSystem con đầu tiên: '{dustParticleEffect.name}' trên '{gameObject.name}'");
            }

            if (dustParticleEffect == null)
            {
                // Fallback: Tự động tạo một hệ thống hạt bụi mịn nhẹ sát đất nếu hoàn toàn không có ParticleSystem nào
                GameObject fallbackDustGo = new GameObject("FallbackDustEffect");
                fallbackDustGo.transform.SetParent(transform);
                fallbackDustGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                fallbackDustGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                dustParticleEffect = fallbackDustGo.AddComponent<ParticleSystem>();

                var main = dustParticleEffect.main;
                main.duration = 1.0f;
                main.loop = true;
                main.startLifetime = 1.5f;
                main.startSpeed = 0.5f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
                main.startColor = new Color(0.7f, 0.65f, 0.55f, 0.05f); // Bụi màu đất mịn mỏng mờ
                main.gravityModifier = -0.05f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 50;

                var emission = dustParticleEffect.emission;
                emission.enabled = false;
                emission.rateOverTime = 15f;

                var shape = dustParticleEffect.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(2.5f, 2.5f, 0.5f);

                var colorOverLifetime = dustParticleEffect.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.7f, 0.65f, 0.55f), 0.0f), new GradientColorKey(new Color(0.6f, 0.55f, 0.5f), 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.05f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) }
                );
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

                var sizeOverLifetime = dustParticleEffect.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0.0f, 0.6f);
                sizeCurve.AddKey(1.0f, 1.4f);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

                var renderer = fallbackDustGo.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    Shader defaultShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (defaultShader == null)
                    {
                        defaultShader = Shader.Find("Sprites/Default");
                    }
                    if (defaultShader != null)
                    {
                        Material mat = new Material(defaultShader);
                        Texture2D builtinTex = Resources.GetBuiltinResource<Texture2D>("Default-Particle.png");
                        if (builtinTex != null)
                        {
                            mat.mainTexture = builtinTex;
                        }
                        renderer.material = mat;
                    }
                }
                Debug.Log($"[PushableStone] Đã khởi tạo fallback bụi mịn cho '{gameObject.name}' thành công.");
            }
        }

        // Tắt bụi lúc bắt đầu game
        if (dustParticleEffect != null)
        {
            var emission = dustParticleEffect.emission;
            emission.enabled = false;
            dustParticleEffect.Stop();
        }

        // Đảm bảo reset trạng thái khi khởi chạy (đặc biệt khi duplicate)
        localFinished = false;
        localMoving = false;
        for (int i = 0; i < localPushers.Length; i++)
        {
            localPushers[i] = null;
        }

        // Tự động kiểm tra và sửa đổi các slot bị lỗi hoặc trỏ ra ngoài khi duplicate
        bool needsReassign = false;
        for (int i = 0; i < pushSlots.Length; i++)
        {
            if (pushSlots[i] == null || !pushSlots[i].IsChildOf(transform))
            {
                needsReassign = true;
                break;
            }
        }

        if (needsReassign)
        {
            Debug.Log($"[PushableStone] Phát hiện pushSlots bị null hoặc trỏ sai đối tượng trên '{gameObject.name}'. Đang tự động tìm lại các slot con...");
            List<Transform> foundSlots = new List<Transform>();
            FindSlotsInChildren(transform, foundSlots);

            if (foundSlots.Count == 0)
            {
                foreach (Transform child in transform)
                {
                    foundSlots.Add(child);
                }
            }

            for (int i = 0; i < pushSlots.Length; i++)
            {
                if (i < foundSlots.Count)
                {
                    pushSlots[i] = foundSlots[i];
                    Debug.Log($"[PushableStone] Đã gán tự động slot {i}: {pushSlots[i].name} trên '{gameObject.name}'");
                }
                else
                {
                    pushSlots[i] = null;
                }
            }
        }
    }

    private void FindSlotsInChildren(Transform current, List<Transform> results)
    {
        foreach (Transform child in current)
        {
            string nameLower = child.name.ToLower();
            if (nameLower.Contains("slot") || nameLower.Contains("push"))
            {
                results.Add(child);
            }
            else
            {
                FindSlotsInChildren(child, results);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        slot0PlayerNetId.OnValueChanged += (oldVal, newVal) => OnSlotChanged(0, oldVal, newVal);
        slot1PlayerNetId.OnValueChanged += (oldVal, newVal) => OnSlotChanged(1, oldVal, newVal);
        slot2PlayerNetId.OnValueChanged += (oldVal, newVal) => OnSlotChanged(2, oldVal, newVal);
        slot3PlayerNetId.OnValueChanged += (oldVal, newVal) => OnSlotChanged(3, oldVal, newVal);

        isFinishedNet.OnValueChanged += (oldVal, newVal) => {
            if (newVal) ReleaseAllPushers();
        };

        if (IsServer)
        {
            netPosition.Value = transform.position;
            isFinishedNet.Value = false;
            isMovingNet.Value = false;
            slot0PlayerNetId.Value = 0;
            slot1PlayerNetId.Value = 0;
            slot2PlayerNetId.Value = 0;
            slot3PlayerNetId.Value = 0;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            }
        }

        // Synchronize initial occupancies
        SyncSlotLocal(0, slot0PlayerNetId.Value);
        SyncSlotLocal(1, slot1PlayerNetId.Value);
        SyncSlotLocal(2, slot2PlayerNetId.Value);
        SyncSlotLocal(3, slot3PlayerNetId.Value);
    }

    public override void OnNetworkDespawn()
    {
        slot0PlayerNetId.OnValueChanged -= (oldVal, newVal) => OnSlotChanged(0, oldVal, newVal);
        slot1PlayerNetId.OnValueChanged -= (oldVal, newVal) => OnSlotChanged(1, oldVal, newVal);
        slot2PlayerNetId.OnValueChanged -= (oldVal, newVal) => OnSlotChanged(2, oldVal, newVal);
        slot3PlayerNetId.OnValueChanged -= (oldVal, newVal) => OnSlotChanged(3, oldVal, newVal);

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }

        UnlockAllPlayersLocal();
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;

        for (int i = 0; i < 4; i++)
        {
            if (GetSlotOccupantNetId(i) == clientId)
            {
                Debug.Log($"[PushableStone Server] Client {clientId} disconnected. Releasing slot {i}.");
                SetSlotOccupant(i, 0);
            }
        }
    }

    private void Update()
    {
        // 1. Position synchronization for client (only if NetworkTransform is not present)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
        {
            if (GetComponent<Unity.Netcode.Components.NetworkTransform>() == null)
            {
                transform.position = Vector3.MoveTowards(transform.position, netPosition.Value, pushSpeed * Time.deltaTime * 1.5f);
            }
        }

        // 2. Keep pushing players snapped and set animation speeds
        bool isMoving = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isMovingNet.Value : localMoving;

        // Cập nhật hiệu ứng bụi khi đẩy đá
        if (dustParticleEffect != null)
        {
            var emission = dustParticleEffect.emission;
            if (isMoving)
            {
                if (!emission.enabled)
                {
                    emission.enabled = true;
                    dustParticleEffect.Play();
                }
            }
            else
            {
                if (emission.enabled)
                {
                    emission.enabled = false;
                    dustParticleEffect.Stop();
                }
            }
        }

        float animSpeed = isMoving ? 1f : 0f;

        for (int i = 0; i < pushSlots.Length; i++)
        {
            GameObject pusherObj = GetPusherGameObject(i);
            if (pusherObj != null)
            {
                // Verify life safety (release if player dies)
                var healthTarget = pusherObj.GetComponent<IPlayerHUDTarget>();
                if (healthTarget != null && healthTarget.CurrentHealth <= 0)
                {
                    if (IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                    {
                        ReleasePlayer(i);
                    }
                    continue;
                }

                // Snap transform to current slot position & rotation
                if (pushSlots[i] != null)
                {
                    pusherObj.transform.position = pushSlots[i].position;
                    pusherObj.transform.rotation = pushSlots[i].rotation;
                }

                // Animate
                var anim = pusherObj.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetBool("IsPushing", true);
                    anim.SetFloat("PushSpeed", animSpeed);
                    
                    // Gán di chuyển chân của layer 0 locomotion
                    anim.SetFloat("MoveX", 0f);
                    anim.SetFloat("MoveZ", isMoving ? 0.5f : 0f);

                    if (anim.layerCount > 1)
                    {
                        anim.SetLayerWeight(1, 1f);
                    }
                }
            }
        }

        // 3. Handle Local Player Interactions (Lock/Unlock)
        bool finished = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? isFinishedNet.Value : localFinished;
        if (finished)
        {
            if (wasInRangeOfAnySlot && hud != null)
            {
                hud.ShowInteractionPrompt(false, "");
                wasInRangeOfAnySlot = false;
            }
            return;
        }

        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        if (localPlayer != null)
        {
            int closestSlot = FindClosestAvailableSlot(localPlayer.gameObject);
            bool isAlreadyPushing = IsPlayerPushing(localPlayer.gameObject, out int currentSlotIndex);

            if (isAlreadyPushing)
            {
                // Local player is currently pushing - show prompt to release
                if (hud == null) hud = FindAnyObjectByType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.ShowInteractionPrompt(true, "Ấn [F] để dừng đẩy đá");
                }
                wasInRangeOfAnySlot = true;

                if (Input.GetKeyDown(KeyCode.F))
                {
                    RequestExitPushSlot(currentSlotIndex);
                }

                // Listen to W input to push
                bool pressingW = Input.GetKey(KeyCode.W);
                if (pressingW != lastInputW)
                {
                    lastInputW = pressingW;
                    SendPushInput(currentSlotIndex, pressingW);
                }

                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[PushableStone Client] localPlayer is pushing slot {currentSlotIndex}, pressing W={pressingW}");
                }
            }
            else if (closestSlot != -1 && !IsPlayerPushingAnyStone(localPlayer.gameObject))
            {
                // Local player is close to an available slot - show prompt to grab
                if (hud == null) hud = FindAnyObjectByType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.ShowInteractionPrompt(true, "Ấn [F] để đẩy đá");
                }
                wasInRangeOfAnySlot = true;
                activeSlotIndex = closestSlot;

                if (Input.GetKeyDown(KeyCode.F))
                {
                    // Check if carrying wood
                    var carrier = localPlayer.GetComponent<PlayerLogCarrier>();
                    if (carrier != null && carrier.isCarrying)
                    {
                        if (hud != null)
                        {
                            hud.ShowMissionAlert("Bạn đang bưng gỗ, không thể đẩy đá!", 3.0f);
                        }
                        return;
                    }

                    RequestEnterPushSlot(closestSlot);
                }
            }
            else
            {
                // Not in range of any slot
                if (wasInRangeOfAnySlot)
                {
                    if (hud != null) hud.ShowInteractionPrompt(false, "");
                    wasInRangeOfAnySlot = false;
                    activeSlotIndex = -1;
                }
            }
        }

        // 4. Update Movement Loop (Server or Standalone)
        if (IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            UpdateStoneMovement();
        }
    }

    private void UpdateStoneMovement()
    {
        int pushersCount = 0;
        bool allPressingW = true;
        string debugStr = "";

        for (int i = 0; i < 4; i++)
        {
            GameObject pusher = GetPusherGameObject(i);
            if (pusher != null)
            {
                pushersCount++;
                bool inputW = false;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    inputW = pusherInputs[i];
                }
                else
                {
                    inputW = Input.GetKey(KeyCode.W);
                }
                debugStr += $"Slot {i}: W={inputW} | ";

                if (!inputW)
                {
                    allPressingW = false;
                }
            }
        }

        bool shouldMove = (pushersCount >= requiredPushers) && allPressingW;
        if (pushersCount > 0 && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[PushableStone Server] pushersCount={pushersCount}/{requiredPushers}, allPressingW={allPressingW}, shouldMove={shouldMove}, details: {debugStr}");
        }

        if (shouldMove)
        {
            transform.position += transform.forward * pushSpeed * Time.deltaTime;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                netPosition.Value = transform.position;
                isMovingNet.Value = true;
            }
            else
            {
                localMoving = true;
            }
        }
        else
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                isMovingNet.Value = false;
            }
            else
            {
                localMoving = false;
            }
        }
    }

    private int FindClosestAvailableSlot(GameObject player)
    {
        float minDist = interactRadius;
        int bestSlot = -1;

        for (int i = 0; i < pushSlots.Length; i++)
        {
            if (pushSlots[i] == null) continue;

            // Check if slot is occupied
            if (IsSlotOccupied(i)) continue;

            float dist = Vector3.Distance(player.transform.position, pushSlots[i].position);
            if (dist < minDist)
            {
                minDist = dist;
                bestSlot = i;
            }
        }

        return bestSlot;
    }

    private bool IsSlotOccupied(int slotIndex)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ulong occupantNetId = GetSlotOccupantNetId(slotIndex);
            return occupantNetId != 0;
        }
        else
        {
            return localPushers[slotIndex] != null;
        }
    }

    private ulong GetSlotOccupantNetId(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return slot0PlayerNetId.Value;
            case 1: return slot1PlayerNetId.Value;
            case 2: return slot2PlayerNetId.Value;
            case 3: return slot3PlayerNetId.Value;
            default: return 0;
        }
    }

    private bool IsPlayerPushing(GameObject player, out int slotIndex)
    {
        slotIndex = -1;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj == null) return false;

            ulong myId = netObj.NetworkObjectId;
            for (int i = 0; i < 4; i++)
            {
                if (GetSlotOccupantNetId(i) == myId)
                {
                    slotIndex = i;
                    return true;
                }
            }
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                if (localPushers[i] == player)
                {
                    slotIndex = i;
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsPlayerPushingAnyStone(GameObject player)
    {
        PushableStone[] allStones = FindObjectsOfType<PushableStone>();
        foreach (var stone in allStones)
        {
            if (stone != null && stone != this)
            {
                if (stone.IsPlayerPushing(player, out _))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private GameObject GetPusherGameObject(int slotIndex)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ulong occupantNetId = GetSlotOccupantNetId(slotIndex);
            if (occupantNetId == 0) return null;

            if (activePusherObjects.TryGetValue(occupantNetId, out GameObject cachedObj))
            {
                return cachedObj;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(occupantNetId, out var netObj))
            {
                activePusherObjects[occupantNetId] = netObj.gameObject;
                // Lock the player to the slot if they weren't locked yet (e.g. late join or spawn lag)
                LockPlayer(netObj.gameObject, slotIndex);
                return netObj.gameObject;
            }
            return null;
        }
        else
        {
            return localPushers[slotIndex];
        }
    }

    #region Entering Slots (F Press)
    private void RequestEnterPushSlot(int slotIndex)
    {
        if (localPlayer == null) return;
        lastInputW = false;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            var netObj = localPlayer.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                EnterPushSlotServerRpc(slotIndex, netObj.NetworkObjectId);
            }
        }
        else
        {
            // Standalone
            localPushers[slotIndex] = localPlayer.gameObject;
            LockPlayer(localPlayer.gameObject, slotIndex);
        }
    }

    private void RequestExitPushSlot(int slotIndex)
    {
        lastInputW = false;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ExitPushSlotServerRpc(slotIndex);
        }
        else
        {
            // Standalone
            if (localPushers[slotIndex] != null)
            {
                UnlockPlayer(localPushers[slotIndex]);
                localPushers[slotIndex] = null;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void EnterPushSlotServerRpc(int slotIndex, ulong playerNetId)
    {
        if (!IsServer) return;

        // Check if slot is empty
        if (GetSlotOccupantNetId(slotIndex) == 0)
        {
            SetSlotOccupant(slotIndex, playerNetId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ExitPushSlotServerRpc(int slotIndex)
    {
        if (!IsServer) return;
        SetSlotOccupant(slotIndex, 0);
    }

    private void SetSlotOccupant(int slotIndex, ulong playerNetId)
    {
        switch (slotIndex)
        {
            case 0: slot0PlayerNetId.Value = playerNetId; break;
            case 1: slot1PlayerNetId.Value = playerNetId; break;
            case 2: slot2PlayerNetId.Value = playerNetId; break;
            case 3: slot3PlayerNetId.Value = playerNetId; break;
        }
        pusherInputs[slotIndex] = false;
    }
    #endregion

    #region Input Sync (W Press)
    private void SendPushInput(int slotIndex, bool isPressingW)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            UpdatePushInputServerRpc(slotIndex, isPressingW);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdatePushInputServerRpc(int slotIndex, bool isPressingW)
    {
        if (!IsServer) return;
        if (slotIndex >= 0 && slotIndex < 4)
        {
            pusherInputs[slotIndex] = isPressingW;
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[PushableStone Server] Slot {slotIndex} input W set to {isPressingW}");
            }
        }
    }
    #endregion

    #region Lock & Unlock Players
    private void LockPlayer(GameObject player, int slotIndex)
    {
        if (player == null) return;

        // Hide active weapons
        var carrier = player.GetComponent<PlayerLogCarrier>();
        if (carrier != null)
        {
            carrier.TogglePlayerWeapons(false);
        }

        // Disable standard player updates (locomotion inputs, combo attacks, movement updates)
        MonoBehaviour locomotion = GetPlayerLocomotionScript(player);
        if (locomotion != null)
        {
            locomotion.enabled = false;
        }

        // Freeze physics on push slot
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Tắt CharacterController nếu có để tránh xung đột di chuyển khi khóa cứng vị trí
        var cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.GetComponentInChildren<CharacterController>();
        if (cc == null) cc = player.GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // Snap to pusher slot
        if (slotIndex >= 0 && slotIndex < pushSlots.Length && pushSlots[slotIndex] != null)
        {
            player.transform.position = pushSlots[slotIndex].position;
            player.transform.rotation = pushSlots[slotIndex].rotation;
        }

        // Start Pushing Animations
        var anim = player.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.SetBool("IsPushing", true);
            anim.SetFloat("PushSpeed", 0f);
            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, 1f);
            }
        }
    }

    private void UnlockPlayer(GameObject player)
    {
        if (player == null) return;

        // Re-enable player locomotion script
        MonoBehaviour locomotion = GetPlayerLocomotionScript(player);
        if (locomotion != null)
        {
            locomotion.enabled = true;
        }

        // Restore weapons
        var carrier = player.GetComponent<PlayerLogCarrier>();
        if (carrier != null)
        {
            carrier.TogglePlayerWeapons(true);
        }

        // Restore player physics
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // Bật lại CharacterController nếu có
        var cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.GetComponentInChildren<CharacterController>();
        if (cc == null) cc = player.GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = true;
        }

        // Stop Pushing Animations
        var anim = player.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.SetBool("IsPushing", false);
            anim.SetFloat("PushSpeed", 0f);
            anim.SetFloat("MoveX", 0f);
            anim.SetFloat("MoveZ", 0f);
            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, 0f);
            }
        }
    }

    private void OnSlotChanged(int slotIndex, ulong oldVal, ulong newVal)
    {
        SyncSlotLocal(slotIndex, newVal);
    }

    private void SyncSlotLocal(int slotIndex, ulong playerNetId)
    {
        // 1. Try to unlock previous owner if exists
        for (int i = 0; i < 4; i++)
        {
            if (i == slotIndex) continue;
        }

        // Find game object for the ID
        if (playerNetId != 0)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var netObj))
            {
                activePusherObjects[playerNetId] = netObj.gameObject;
                LockPlayer(netObj.gameObject, slotIndex);
            }
        }
        else
        {
            // If newVal is 0, find who was occupying the slot previously and unlock them
            foreach (var kvp in activePusherObjects)
            {
                if (kvp.Key != slot0PlayerNetId.Value &&
                    kvp.Key != slot1PlayerNetId.Value &&
                    kvp.Key != slot2PlayerNetId.Value &&
                    kvp.Key != slot3PlayerNetId.Value)
                {
                    UnlockPlayer(kvp.Value);
                }
            }

            // Cleanup dictionary keys not currently occupied
            List<ulong> toRemove = new List<ulong>();
            foreach (var key in activePusherObjects.Keys)
            {
                if (key != slot0PlayerNetId.Value &&
                    key != slot1PlayerNetId.Value &&
                    key != slot2PlayerNetId.Value &&
                    key != slot3PlayerNetId.Value)
                {
                    toRemove.Add(key);
                }
            }
            foreach (var key in toRemove)
            {
                activePusherObjects.Remove(key);
            }
        }
    }

    private void UnlockAllPlayersLocal()
    {
        foreach (var obj in activePusherObjects.Values)
        {
            UnlockPlayer(obj);
        }
        activePusherObjects.Clear();

        for (int i = 0; i < 4; i++)
        {
            if (localPushers[i] != null)
            {
                UnlockPlayer(localPushers[i]);
                localPushers[i] = null;
            }
        }
    }

    private MonoBehaviour GetPlayerLocomotionScript(GameObject playerObj)
    {
        if (playerObj == null) return null;

        // Ưu tiên tìm các locomotion script chính thức trước tiên để tránh trả về nhầm helper script như PlayerLogCarrier
        MonoBehaviour comp = playerObj.GetComponent<LeoPlayer>();
        if (comp != null) return comp;

        comp = playerObj.GetComponent<ArthurPlayer>();
        if (comp != null) return comp;

        comp = playerObj.GetComponent<ElenaPlayer>();
        if (comp != null) return comp;

        comp = playerObj.GetComponent<MayaPlayer>();
        if (comp != null) return comp;
        
        // Duyệt tìm tất cả các script Monobehaviour trên người chơi để tắt các locomotion script tương ứng (kể cả LeoAssassin, ArthurTanker, v.v.)
        MonoBehaviour[] scripts = playerObj.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;
            string typeName = script.GetType().Name;
            
            if (typeName.Contains("Player") || typeName.Contains("Locomotion") || typeName.Contains("Movement") || 
                typeName.Contains("Controller") || typeName.Contains("Test") || typeName.Contains("Assassin") || 
                typeName.Contains("Tanker") || typeName.Contains("Archer") || typeName.Contains("Support"))
            {
                // Bỏ qua các class hệ thống của Unity hoặc Netcode
                if (script is NetworkBehaviour || script.GetType().Namespace == "Unity.Netcode" || script.GetType().Namespace == "UnityEngine")
                {
                    continue;
                }
                return script;
            }
        }
        
        if (comp == null) comp = playerObj.GetComponentInChildren<MayaPlayer>();
        if (comp == null) comp = playerObj.GetComponentInParent<MayaPlayer>();

        if (comp == null) comp = playerObj.GetComponent<SimplePlayerTest>();
        if (comp == null) comp = playerObj.GetComponentInChildren<SimplePlayerTest>();
        if (comp == null) comp = playerObj.GetComponentInParent<SimplePlayerTest>();

        return comp;
    }
    #endregion

    #region Public Release Trigger (On Plate Success)
    public void ReleaseAllPushers()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                isFinishedNet.Value = true;
                isMovingNet.Value = false;
                slot0PlayerNetId.Value = 0;
                slot1PlayerNetId.Value = 0;
                slot2PlayerNetId.Value = 0;
                slot3PlayerNetId.Value = 0;
            }
        }
        else
        {
            localFinished = true;
            localMoving = false;
            UnlockAllPlayersLocal();
        }

        if (hud != null)
        {
            hud.ShowInteractionPrompt(false, "");
        }
    }

    private void ReleasePlayer(int slotIndex)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                SetSlotOccupant(slotIndex, 0);
            }
        }
        else
        {
            if (localPushers[slotIndex] != null)
            {
                UnlockPlayer(localPushers[slotIndex]);
                localPushers[slotIndex] = null;
            }
        }
    }
    #endregion

    private void FindLocalPlayer()
    {
        if (PlayerHUDController.LocalPlayerTarget != null)
        {
            var p = PlayerHUDController.LocalPlayerTarget;
            if (p != null && (p.IsStandaloneMode || p.IsOwner))
            {
                localPlayer = p as MonoBehaviour;
            }
        }
    }
}
