using UnityEngine;

public interface IPlayerHUDTarget
{
    bool isStandaloneMode { get; }
    bool IsStandaloneMode { get; }
    bool IsOwner { get; }
    bool IsSpawned { get; }
    int CharacterClassIndex { get; }
    
    // Weapon & Skills
    bool IsSwitchingWeapon { get; }
    int GetActiveWeaponIndex();
    void PlayWeaponSwitchAnimation(int oldWeapon, int newWeapon);
    void UpdateStateFromHUD(int weaponIndex, bool weapon2Locked, bool skillsUnlocked);
    
    // Durability & Inventory
    float Weapon1Durability { get; set; }
    float Weapon2Durability { get; set; }
    float Weapon1MaxDurability { get; }
    float Weapon2MaxDurability { get; }
    string[] InventorySlots { get; }
    void SavePlayerStateToDatabase();
    
    // Upgrades
    void UpgradeStatFromHUD(int statType);
    void StandaloneUpgradeStat(int statType);
    
    // Controls
    void SetCursorLock(bool locked);
    
    // Health & Stats
    float CurrentHealth { get; }
    float MaxHealth { get; }
    
    // GameObject properties
    Transform transform { get; }
    GameObject gameObject { get; }
}
