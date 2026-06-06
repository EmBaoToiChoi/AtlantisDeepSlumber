using UnityEngine;

public interface IPlayerHUDTarget
{
    bool isStandaloneMode { get; }
    bool IsStandaloneMode { get; }
    bool IsOwner { get; }
    bool IsSpawned { get; }
    int CharacterClassIndex { get; }
    ulong OwnerClientId { get; }

    // Info & Stats
    string DisplayName { get; }
    int PlayerLevel { get; }
    float PlayerExp { get; }
    float MaxExp { get; }
    
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
    void RepairWeaponFromHUD(int weaponSlotIndex);
    
    // Upgrades
    void UpgradeStatFromHUD(int statType);
    void StandaloneUpgradeStat(int statType);
    
    // Controls
    void SetCursorLock(bool locked);
    
    // Health & Stats
    float CurrentHealth { get; }
    float MaxHealth { get; }
    
    // Invisibility Skill R
    bool IsInvisible { get; }
    float InvisibilityTimeRemaining { get; }
    void TriggerInvisibilitySkill();

    // Attack Speed Boost Skill E
    bool IsAttackSpeedBoosted { get; }
    float AttackSpeedBoostTimeRemaining { get; }
    void TriggerAttackSpeedBoostSkill();

    // Q Skill support
    bool IsQSkillActive { get; }
    float QSkillTimeRemaining { get; }
    bool TriggerQSkill();
    /// <summary>Kích hoạt khi server xác nhận không có enemy → HUD cần reset cooldown Q.</summary>
    event System.Action OnQSkillCancelled;
    
    // GameObject properties
    Transform transform { get; }
    GameObject gameObject { get; }
}
