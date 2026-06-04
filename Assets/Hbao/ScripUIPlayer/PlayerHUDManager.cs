using UnityEngine;

public class PlayerHUDManager : MonoBehaviour
{
    public static PlayerHUDManager Instance { get; private set; }
    
    // Danh sách lưu trữ tất cả người chơi đang hoạt động để làm HUD đồng đội
    public static System.Collections.Generic.List<IPlayerHUDTarget> ActivePlayers = new System.Collections.Generic.List<IPlayerHUDTarget>();

    [Header("Player HUD GameObjects")]
    [SerializeField] private GameObject leoHUD;
    [SerializeField] private GameObject mayaHUD;
    [SerializeField] private GameObject elenaHUD;
    [SerializeField] private GameObject arthurHUD;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Hide all initially
        DeactivateAllHUDs();
    }

    private void Start()
    {
        // Tự động đọc nhân vật đã chọn từ Lobby để kích hoạt đúng HUD khi bắt đầu game
        int selectedChar = PlayerPrefs.GetInt("SelectedCharacterId", 0);
        Debug.Log($"[PlayerHUDManager] Start: Tự động kích hoạt HUD cho nhân vật index {selectedChar}");
        ActivateHUD(selectedChar);
    }

    public void DeactivateAllHUDs()
    {
        if (leoHUD != null) leoHUD.SetActive(false);
        if (mayaHUD != null) mayaHUD.SetActive(false);
        if (elenaHUD != null) elenaHUD.SetActive(false);
        if (arthurHUD != null) arthurHUD.SetActive(false);
    }

    public PlayerHUDController ActivateHUD(int characterId)
    {
        DeactivateAllHUDs();

        GameObject activeHUDObject = null;
        switch (characterId)
        {
            case 0:
                activeHUDObject = leoHUD;
                break;
            case 1:
                activeHUDObject = mayaHUD;
                break;
            case 2:
                activeHUDObject = elenaHUD;
                break;
            case 3:
                activeHUDObject = arthurHUD;
                break;
        }

        if (activeHUDObject != null)
        {
            activeHUDObject.SetActive(true);
            var controller = activeHUDObject.GetComponent<PlayerHUDController>();
            if (controller != null)
            {
                controller.InitializeUI();
            }
            Debug.Log($"[PlayerHUDManager] Activated HUD for character class index: {characterId} ({activeHUDObject.name})");
            return controller;
        }

        Debug.LogWarning($"[PlayerHUDManager] No HUD GameObject assigned for character class index: {characterId}");
        return null;
    }

    /// <summary>
    /// Tìm kiếm avatar tương ứng với classIdx từ tất cả các HUD được gán trong manager
    /// (Hỗ trợ trường hợp người dùng tách nhỏ HUD và mỗi HUD chỉ kéo thả 1 profile của nhân vật đó)
    /// </summary>
    public Sprite GetTeammateAvatar(int classIdx)
    {
        GameObject[] huds = new GameObject[] { leoHUD, mayaHUD, elenaHUD, arthurHUD };
        foreach (var hud in huds)
        {
            if (hud == null) continue;
            var controller = hud.GetComponent<PlayerHUDController>();
            if (controller == null || controller.hudProfiles == null) continue;

            foreach (var profile in controller.hudProfiles)
            {
                if (profile.avatarSprite == null) continue;
                string lowerName = profile.className != null ? profile.className.ToLower() : "";

                if (classIdx == 0 && (lowerName.Contains("leo") || lowerName.Contains("assassin")))
                    return profile.avatarSprite;
                if (classIdx == 1 && (lowerName.Contains("maya") || lowerName.Contains("support")))
                    return profile.avatarSprite;
                if (classIdx == 2 && (lowerName.Contains("elena") || lowerName.Contains("archer")))
                    return profile.avatarSprite;
                if (classIdx == 3 && (lowerName.Contains("arthur") || lowerName.Contains("athurt") || lowerName.Contains("tanker")))
                    return profile.avatarSprite;
            }
        }
        return null;
    }
}
