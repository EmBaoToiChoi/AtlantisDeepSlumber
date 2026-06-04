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
}
