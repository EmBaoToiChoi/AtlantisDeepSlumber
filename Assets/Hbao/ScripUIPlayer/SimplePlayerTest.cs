using Unity.Netcode;
using UnityEngine;

public class SimplePlayerTest : NetworkBehaviour
{
    [Header("Movement & Attack Settings")]
    public float moveSpeed = 5f;
    public float damageAmount = 20f;
    public float attackRange = 3f;

    [Header("Player Health Settings")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Network Sync Variables")]
    public NetworkVariable<int> activeWeaponIndex = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isWeapon2Locked = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isSkillsUnlocked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Upgrade Sync Variables")]
    public NetworkVariable<int> upgradePoints = new NetworkVariable<int>(
        5,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> hpLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> mpLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> cooldownLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> damageLevel = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Local State & Inventory")]
    public string[] inventorySlots = new string[10] { "", "", "", "", "", "", "", "", "", "" };
    
    // Biến lưu nâng cấp cho chế độ chơi đơn (Standalone)
    private int localUpgradePoints = 5;
    private int localHpLevel = 0;
    private int localMpLevel = 0;
    private int localCooldownLevel = 0;
    private int localDamageLevel = 0;

    [Header("Player Class Settings")]
    [Tooltip("0 = Sát Thủ, 1 = Hỏa Thuật, 2 = Cung Thủ, 3 = Tanker")]
    public int characterClassIndex = 0;

    [Header("Knockback Settings")]
    private Vector3 knockbackVelocity;

    [Header("Camera Follow Settings")]
    public bool enableCameraFollow = true;
    public Vector3 cameraOffset = new Vector3(0f, 12f, -8f);
    public float cameraSmoothSpeed = 5f;
    public bool cameraLookAtPlayer = true;
    private Camera targetCamera;

    // ------------------------------------------------------------------
    //  Biến nội bộ cho chế độ Standalone (không có Netcode)
    // ------------------------------------------------------------------
    private float localHealth;
    public bool isStandaloneMode = false; // true khi chạy đơn lẻ không qua NetworkManager
    private bool isSyncingFromDb = false; // true khi đang đồng bộ dữ liệu ban đầu từ DB tránh hồi máu ảo

    /// <summary>
    /// Trả về true nếu NetworkManager đang hoạt động và đã kết nối/host.
    /// </summary>
    private bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    /// <summary>
    /// Máu hiện tại: đọc từ NetworkVariable khi online, đọc từ biến local khi standalone.
    /// </summary>
    public float CurrentHealth =>
        isStandaloneMode ? localHealth : currentHealth.Value;

    // ------------------------------------------------------------------
    //  Khởi tạo Standalone (không qua Netcode)
    // ------------------------------------------------------------------
    private void Start()
    {
        // Nếu không có NetworkManager hoặc chưa listen → chạy đơn lẻ
        if (!IsNetworkActive)
        {
            isStandaloneMode = true;
            localHealth = maxHealth;
            InitStandaloneMode();
        }
        // Nếu có Netcode nhưng chưa spawn (vd: đang chờ) thì không làm gì thêm
        // OnNetworkSpawn() sẽ lo phần còn lại
    }

    /// <summary>
    /// Khởi tạo tất cả chức năng khi chơi đơn lẻ trong Editor mà không cần Host/Server.
    /// </summary>
    private void InitStandaloneMode()
    {
        Debug.Log("[SimplePlayerTest] Chạy ở chế độ STANDALONE (không có NetworkManager). " +
                  "Di chuyển và tấn công hoạt động cục bộ.");

        // Tìm camera
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        // Khởi tạo HUD với profile nhân vật
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
        {
            hud.SetupPlayerProfile(characterClassIndex);
            ApplyUpgradedStats();
            UpdateUpgradeHUD();
        }
    }

    // ------------------------------------------------------------------
    //  Netcode Spawn / Despawn
    // ------------------------------------------------------------------
    public override void OnNetworkSpawn()
    {
        isStandaloneMode = false;

        // Đăng ký sự kiện đồng bộ Netcode
        activeWeaponIndex.OnValueChanged += OnWeaponIndexChanged;
        isWeapon2Locked.OnValueChanged += OnWeapon2LockedChanged;
        isSkillsUnlocked.OnValueChanged += OnSkillsUnlockedChanged;

        // Đăng ký các sự kiện nâng cấp chỉ số
        upgradePoints.OnValueChanged += OnUpgradePointsChanged;
        hpLevel.OnValueChanged += OnHpLevelChanged;
        mpLevel.OnValueChanged += OnMpLevelChanged;
        cooldownLevel.OnValueChanged += OnCooldownLevelChanged;
        damageLevel.OnValueChanged += OnDamageLevelChanged;

        if (IsOwner)
        {
            currentHealth.OnValueChanged += OnHealthChanged;
            UpdateHealthHUD(currentHealth.Value);

            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null)
                hud.SetupPlayerProfile(characterClassIndex);

            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();

            // Tải dữ liệu từ MongoDB Atlas
            LoadPlayerStateFromDatabase();

            // Áp dụng và cập nhật UI chỉ số ban đầu cục bộ
            ApplyUpgradedStats();
            UpdateUpgradeHUD();
        }
    }

    public override void OnNetworkDespawn()
    {
        activeWeaponIndex.OnValueChanged -= OnWeaponIndexChanged;
        isWeapon2Locked.OnValueChanged -= OnWeapon2LockedChanged;
        isSkillsUnlocked.OnValueChanged -= OnSkillsUnlockedChanged;

        // Hủy đăng ký các sự kiện nâng cấp chỉ số
        upgradePoints.OnValueChanged -= OnUpgradePointsChanged;
        hpLevel.OnValueChanged -= OnHpLevelChanged;
        mpLevel.OnValueChanged -= OnMpLevelChanged;
        cooldownLevel.OnValueChanged -= OnCooldownLevelChanged;
        damageLevel.OnValueChanged -= OnDamageLevelChanged;

        if (IsOwner)
            currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnWeaponIndexChanged(int oldVal, int newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) hud.SelectWeapon(newVal);
        }
    }

    private void OnWeapon2LockedChanged(bool oldVal, bool newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) hud.SetWeapon2Locked(newVal, true);
        }
    }

    private void OnSkillsUnlockedChanged(bool oldVal, bool newVal)
    {
        if (IsOwner)
        {
            PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
            if (hud != null) hud.SetSkillsUnlocked(newVal, true);
        }
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        UpdateHealthHUD(newHealth);
        
        // Tự động lưu lên DB khi máu thay đổi
        if (IsOwner)
        {
            SavePlayerStateToDatabase();
        }
    }

    private void UpdateHealthHUD(float health)
    {
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
            hud.SetHealth(health / maxHealth);
    }

    // ------------------------------------------------------------------
    //  Bộ lắng nghe sự kiện thay đổi của các NetworkVariable nâng cấp chỉ số
    // ------------------------------------------------------------------
    private void OnUpgradePointsChanged(int oldVal, int newVal)
    {
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnHpLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnMpLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnCooldownLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    private void OnDamageLevelChanged(int oldVal, int newVal)
    {
        if (IsServer || IsOwner)
        {
            ApplyUpgradedStats();
        }
        if (IsOwner)
        {
            UpdateUpgradeHUD();
        }
    }

    // ------------------------------------------------------------------
    //  Áp dụng chỉ số nâng cấp vào các giá trị thực tế
    // ------------------------------------------------------------------
    private void ApplyUpgradedStats()
    {
        if (isSyncingFromDb) return;

        int hpLv = isStandaloneMode ? localHpLevel : hpLevel.Value;
        int dmgLv = isStandaloneMode ? localDamageLevel : damageLevel.Value;

        float oldMaxHealth = maxHealth;
        maxHealth = 100f + hpLv * 20f;
        damageAmount = 20f + dmgLv * 5f;

        if (isStandaloneMode)
        {
            if (maxHealth > oldMaxHealth)
            {
                localHealth += (maxHealth - oldMaxHealth);
            }
            UpdateHealthHUD(localHealth);
        }
        else if (IsServer)
        {
            if (maxHealth > oldMaxHealth)
            {
                currentHealth.Value += (maxHealth - oldMaxHealth);
            }
        }
    }

    // ------------------------------------------------------------------
    //  Cập nhật UI nâng cấp chỉ số
    // ------------------------------------------------------------------
    private void UpdateUpgradeHUD()
    {
        PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
        if (hud != null)
        {
            int pts = isStandaloneMode ? localUpgradePoints : upgradePoints.Value;
            int hp = isStandaloneMode ? localHpLevel : hpLevel.Value;
            int mp = isStandaloneMode ? localMpLevel : mpLevel.Value;
            int cd = isStandaloneMode ? localCooldownLevel : cooldownLevel.Value;
            int dmg = isStandaloneMode ? localDamageLevel : damageLevel.Value;

            hud.UpdateUpgradeUI(pts, hp, mp, cd, dmg);
            hud.SetHealth(CurrentHealth / maxHealth);
        }
    }

    // ------------------------------------------------------------------
    //  Nâng cấp chỉ số cho chơi đơn (Standalone Mode)
    // ------------------------------------------------------------------
    public void StandaloneUpgradeStat(int statType)
    {
        if (localUpgradePoints <= 0)
        {
            Debug.LogWarning("[Standalone] Hết điểm nâng cấp!");
            return;
        }

        localUpgradePoints--;
        switch (statType)
        {
            case 0: localHpLevel++; break;
            case 1: localMpLevel++; break;
            case 2: localCooldownLevel++; break;
            case 3: localDamageLevel++; break;
        }

        ApplyUpgradedStats();
        UpdateUpgradeHUD();
        Debug.Log($"[Standalone] Đã nâng cấp Stat {statType}. Cấp độ mới: HP={localHpLevel}, MP={localMpLevel}, Cooldown={localCooldownLevel}, Damage={localDamageLevel}. Điểm còn: {localUpgradePoints}");
    }

    // ------------------------------------------------------------------
    //  Nâng cấp chỉ số online qua Server RPC (Netcode Mode)
    // ------------------------------------------------------------------
    public void UpgradeStatFromHUD(int statType)
    {
        if (!IsSpawned || !IsOwner) return;
        UpgradeStatServerRpc(statType);
    }

    [ServerRpc]
    private void UpgradeStatServerRpc(int statType)
    {
        if (upgradePoints.Value <= 0)
        {
            Debug.LogWarning("[Server] Người chơi không còn điểm nâng cấp!");
            return;
        }

        upgradePoints.Value--;
        switch (statType)
        {
            case 0: hpLevel.Value++; break;
            case 1: mpLevel.Value++; break;
            case 2: cooldownLevel.Value++; break;
            case 3: damageLevel.Value++; break;
        }

        ApplyUpgradedStats();
        SavePlayerStateClientRpc();
        Debug.Log($"[Server] Đã nâng cấp Stat {statType} cho {gameObject.name}. HP Lv={hpLevel.Value}, MP Lv={mpLevel.Value}, CD Lv={cooldownLevel.Value}, DMG Lv={damageLevel.Value}. Điểm còn lại: {upgradePoints.Value}");
    }

    // ------------------------------------------------------------------
    //  Update (hoạt động cả Standalone lẫn Netcode)
    // ------------------------------------------------------------------
    void Update()
    {
        // Standalone: xử lý hoàn toàn cục bộ
        if (isStandaloneMode)
        {
            HandleStandaloneUpdate();
            return;
        }

        // Netcode: chỉ chủ sở hữu mới điều khiển
        if (!IsOwner) return;
        HandleOwnerUpdate();
    }

    private void HandleStandaloneUpdate()
    {
        // Knockback
        if (knockbackVelocity.magnitude > 0.01f)
        {
            transform.Translate(knockbackVelocity * Time.deltaTime, Space.World);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
        }

        // Di chuyển
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);
        if (move != Vector3.zero)
            transform.forward = move;

        // Tấn công đơn lẻ
        if (Input.GetMouseButtonDown(0))
            StandaloneAttack();
    }

    private void HandleOwnerUpdate()
    {
        // Knockback
        if (knockbackVelocity.magnitude > 0.01f)
        {
            transform.Translate(knockbackVelocity * Time.deltaTime, Space.World);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 8f);
        }

        // Di chuyển
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = new Vector3(moveX, 0, moveZ);
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);
        if (move != Vector3.zero)
            transform.forward = move;

        // Tấn công qua RPC (chỉ khi đã spawn trên mạng)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsSpawned)
                AttackServerRpc();
        }
    }

    void LateUpdate()
    {
        // Camera follow hoạt động cho cả standalone lẫn Netcode owner
        bool shouldFollow = isStandaloneMode || (IsSpawned && IsOwner);
        if (!shouldFollow || !enableCameraFollow) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindObjectOfType<Camera>();
        }

        if (targetCamera != null)
        {
            Vector3 targetPosition = transform.position + cameraOffset;
            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                targetPosition,
                Time.deltaTime * cameraSmoothSpeed
            );

            if (cameraLookAtPlayer)
            {
                Quaternion targetRotation = Quaternion.LookRotation(
                    (transform.position + Vector3.up * 1f) - targetCamera.transform.position
                );
                targetCamera.transform.rotation = Quaternion.Slerp(
                    targetCamera.transform.rotation,
                    targetRotation,
                    Time.deltaTime * cameraSmoothSpeed
                );
            }
        }
    }

    // ------------------------------------------------------------------
    //  Tấn công Standalone (không cần Server RPC)
    // ------------------------------------------------------------------
    private void StandaloneAttack()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 0.5f);

        if (!Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange)) return;

        TryDamageEnemy(hit.collider);
    }

    private void TryDamageEnemy(Collider col)
    {
        var e1 = col.GetComponentInParent<Enemy1_DapBua>();
        if (e1 != null) { e1.TakeDamage(damageAmount); return; }

        var e2 = col.GetComponentInParent<Enemy2_Zombie>();
        if (e2 != null) { e2.TakeDamage(damageAmount); return; }

        var e3 = col.GetComponentInParent<Enemy3_Buaa>();
        if (e3 != null) { e3.TakeDamage(damageAmount); return; }

        var e4 = col.GetComponentInParent<Enemy4_Bongtoi>();
        if (e4 != null) { e4.TakeDamage(damageAmount); return; }

        var e5 = col.GetComponentInParent<Enemy5_PhuThuy>();
        if (e5 != null) { e5.TakeDamage(damageAmount); return; }
    }

    // ------------------------------------------------------------------
    //  Server RPC Tấn công (chỉ dùng khi Netcode online)
    // ------------------------------------------------------------------
    [ServerRpc]
    void AttackServerRpc()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 0.5f);

        if (!Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, attackRange)) return;

        var enemy1 = hit.collider.GetComponentInParent<Enemy1_DapBua>();
        if (enemy1 != null) { enemy1.TakeDamage(damageAmount); return; }

        var enemy2 = hit.collider.GetComponentInParent<Enemy2_Zombie>();
        if (enemy2 != null) { enemy2.TakeDamage(damageAmount); return; }

        var enemy3 = hit.collider.GetComponentInParent<Enemy3_Buaa>();
        if (enemy3 != null) { enemy3.TakeDamage(damageAmount); return; }

        var enemy4 = hit.collider.GetComponentInParent<Enemy4_Bongtoi>();
        if (enemy4 != null) { enemy4.TakeDamage(damageAmount); return; }

        var enemy5 = hit.collider.GetComponentInParent<Enemy5_PhuThuy>();
        if (enemy5 != null) { enemy5.TakeDamage(damageAmount); return; }
    }

    // ------------------------------------------------------------------
    //  Nhận sát thương
    // ------------------------------------------------------------------
    /// <summary>
    /// Nhận sát thương. Chỉ Server xử lý khi online; cục bộ xử lý khi standalone.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isStandaloneMode)
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            UpdateHealthHUD(localHealth);
            Debug.Log($"[Standalone] {gameObject.name} nhận {damage} sát thương. Máu còn: {localHealth}");
            if (localHealth <= 0)
                Debug.LogWarning($"[Standalone] {gameObject.name} đã chết!");
            return;
        }

        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(currentHealth.Value - damage, 0f);
        Debug.Log($"[Server] {gameObject.name} nhận {damage} sát thương. Máu còn lại: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
            Debug.LogWarning($"[Server] {gameObject.name} đã chết!");
    }

    // ------------------------------------------------------------------
    //  Knockback
    // ------------------------------------------------------------------
    /// <summary>
    /// Giao diện áp dụng Knockback (Server-authoritative khi online, cục bộ khi standalone).
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        if (isStandaloneMode)
        {
            knockbackVelocity = force;
            return;
        }

        if (!IsServer) return;
        ApplyKnockbackClientRpc(force);
    }

    [ClientRpc]
    public void ApplyKnockbackClientRpc(Vector3 force)
    {
        if (IsOwner)
            knockbackVelocity = force;
    }

    // ==================================================================
    //  MongoDB & Netcode Integration Logic
    // ==================================================================

    public void UpdateStateFromHUD(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
        if (!IsSpawned || !IsOwner) return;
        UpdateStateServerRpc(weaponIndex, weapon2Locked, skillsUnlocked);
    }

    [ServerRpc]
    private void UpdateStateServerRpc(int weaponIndex, bool weapon2Locked, bool skillsUnlocked)
    {
        activeWeaponIndex.Value = weaponIndex;
        isWeapon2Locked.Value = weapon2Locked;
        isSkillsUnlocked.Value = skillsUnlocked;
        SavePlayerStateClientRpc();
    }

    private async void LoadPlayerStateFromDatabase()
    {
        Debug.Log("[DB] Bắt đầu tải trạng thái người chơi từ MongoDB Atlas...");
        try
        {
            var res = await AuthService.GetPlayerState();
            if (res != null && res.success && res.playerState != null)
            {
                var state = res.playerState;
                Debug.Log($"[DB] Đã tải thành công trạng thái người chơi từ MongoDB! Máu: {state.health}");
                
                SyncPlayerStateServerRpc(
                    state.health, 
                    state.activeWeaponIndex, 
                    state.isWeapon2Locked, 
                    state.isSkillsUnlocked,
                    state.upgradePoints,
                    state.hpLevel,
                    state.mpLevel,
                    state.cooldownLevel,
                    state.damageLevel
                );

                if (state.inventorySlots != null)
                {
                    for (int i = 0; i < inventorySlots.Length && i < state.inventorySlots.Length; i++)
                    {
                        inventorySlots[i] = state.inventorySlots[i];
                    }
                }

                PlayerHUDController hud = FindObjectOfType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.SetInventorySlots(inventorySlots);
                    hud.SetSkillsUnlocked(state.isSkillsUnlocked, false);
                    hud.SetWeapon2Locked(state.isWeapon2Locked, false);
                    hud.SelectWeapon(state.activeWeaponIndex);
                    // Cập nhật lại UI sau khi các NetworkVariables được đồng bộ
                    hud.UpdateUpgradeUI(state.upgradePoints, state.hpLevel, state.mpLevel, state.cooldownLevel, state.damageLevel);
                    hud.SetHealth(state.health / (100f + state.hpLevel * 20f));
                }
            }
            else
            {
                Debug.LogWarning("[DB] Không có dữ liệu cũ hoặc lỗi kết nối. Đồng bộ dữ liệu ban đầu.");
                SyncPlayerStateServerRpc(maxHealth, 1, true, false, 5, 0, 0, 0, 0);
                SavePlayerStateToDatabase();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Lỗi khi kết nối API tải dữ liệu MongoDB: {ex.Message}");
            SyncPlayerStateServerRpc(maxHealth, 1, true, false, 5, 0, 0, 0, 0);
        }
    }

    [ServerRpc]
    private void SyncPlayerStateServerRpc(
        float health, 
        int weaponIndex, 
        bool weapon2Locked, 
        bool skillsUnlocked,
        int pts,
        int hp,
        int mp,
        int cd,
        int dmg
    )
    {
        isSyncingFromDb = true;

        upgradePoints.Value = pts;
        hpLevel.Value = hp;
        mpLevel.Value = mp;
        cooldownLevel.Value = cd;
        damageLevel.Value = dmg;

        // Đồng bộ trước maxHealth tác động của chỉ số lên server
        maxHealth = 100f + hp * 20f;
        damageAmount = 20f + dmg * 5f;

        currentHealth.Value = health;
        activeWeaponIndex.Value = weaponIndex;
        isWeapon2Locked.Value = weapon2Locked;
        isSkillsUnlocked.Value = skillsUnlocked;

        isSyncingFromDb = false;
    }

    public async void SavePlayerStateToDatabase()
    {
        if (!IsSpawned || !IsOwner) return;

        Debug.Log("[DB] Đang tự động lưu trạng thái nhân vật lên MongoDB...");
        try
        {
            var stateData = new PlayerStateData
            {
                health = CurrentHealth,
                activeWeaponIndex = activeWeaponIndex.Value,
                isWeapon2Locked = isWeapon2Locked.Value,
                isSkillsUnlocked = isSkillsUnlocked.Value,
                inventorySlots = inventorySlots,
                upgradePoints = upgradePoints.Value,
                hpLevel = hpLevel.Value,
                mpLevel = mpLevel.Value,
                cooldownLevel = cooldownLevel.Value,
                damageLevel = damageLevel.Value
            };

            var res = await AuthService.SavePlayerState(stateData);
            if (res != null && res.success)
            {
                Debug.Log("[DB] Đã lưu trạng thái nhân vật lên MongoDB Atlas thành công!");
            }
            else
            {
                Debug.LogError($"[DB] Lỗi lưu trữ MongoDB: {res?.message}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DB] Lỗi khi gọi API lưu trạng thái MongoDB: {ex.Message}");
        }
    }

    [ClientRpc]
    private void SavePlayerStateClientRpc()
    {
        if (IsOwner)
        {
            SavePlayerStateToDatabase();
        }
    }
}
