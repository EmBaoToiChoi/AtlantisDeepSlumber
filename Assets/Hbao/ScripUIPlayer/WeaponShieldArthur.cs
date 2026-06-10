using UnityEngine;
using Unity.Netcode;

public class WeaponShieldArthur : NetworkBehaviour
{
    [Header("Item Settings")]
    public string itemName = "Khiên & Kiếm của Arthur";
    public float interactRadius = 2.5f;
    public int targetClassIndex = 3; // Tanker (Arthur)

    [Header("Floating Animation Settings")]
    public float rotationSpeed = 60f;
    public float bobSpeed = 2f;
    public float bobRange = 0.12f;

    private IPlayerHUDTarget localPlayer;
    private PlayerHUDController hud;
    private bool isWithinRange = false;
    private float startY;

    private void Start()
    {
        startY = transform.position.y;
    }

    private void Update()
    {
        // Floating visual effect
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        Vector3 currentPos = transform.position;
        currentPos.y = startY + Mathf.Sin(Time.time * bobSpeed) * bobRange;
        transform.position = currentPos;

        // 1. Find local player
        if (localPlayer == null || (localPlayer is UnityEngine.Object obj && obj == null))
        {
            FindLocalPlayer();
        }

        if (localPlayer != null && localPlayer is UnityEngine.Object playerObj && playerObj != null)
        {
            // If local player is dead, clear interaction prompt
            if (localPlayer.CurrentHealth <= 0)
            {
                if (isWithinRange)
                {
                    isWithinRange = false;
                    if (hud != null) hud.ShowInteractionPrompt(false, "");
                }
                return;
            }

            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);
            bool isClosest = IsClosestItem();

            if (distance <= interactRadius && isClosest)
            {
                if (!isWithinRange)
                {
                    isWithinRange = true;
                    if (hud == null)
                    {
                        hud = FindObjectOfType<PlayerHUDController>();
                    }
                    if (hud != null)
                    {
                        hud.ShowInteractionPrompt(true, "Ấn [F] để nhặt");
                    }
                }

                // Press F to interact
                if (Input.GetKeyDown(KeyCode.F))
                {
                    TryCollectWeapon();
                }
            }
            else if (isWithinRange)
            {
                isWithinRange = false;
                if (hud != null)
                {
                    hud.ShowInteractionPrompt(false, "");
                }
            }
        }
    }

    private void FindLocalPlayer()
    {
        var targets = FindObjectsOfType<MonoBehaviour>();
        foreach (var target in targets)
        {
            if (target is IPlayerHUDTarget player)
            {
                if (player.IsStandaloneMode || player.IsOwner)
                {
                    localPlayer = player;
                    break;
                }
            }
        }
    }

    private bool IsClosestItem()
    {
        if (localPlayer == null) return false;

        float myDist = Vector3.Distance(transform.position, localPlayer.transform.position);

        var bows = FindObjectsOfType<WeaponBowElena>();
        foreach (var item in bows)
        {
            if (item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.interactRadius && dist < myDist) return false;
        }

        var blades = FindObjectsOfType<WeaponBladesLeo>();
        foreach (var item in blades)
        {
            if (item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.interactRadius && dist < myDist) return false;
        }

        var arthurs = FindObjectsOfType<WeaponShieldArthur>();
        foreach (var item in arthurs)
        {
            if (item == this || item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.interactRadius && dist < myDist) return false;
        }

        var collectibles = FindObjectsOfType<CollectibleItemDrop>();
        foreach (var item in collectibles)
        {
            if (item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.interactRadius && dist < myDist) return false;
        }

        var repairs = FindObjectsOfType<RepairItemDrop>();
        foreach (var item in repairs)
        {
            if (item == null) continue;
            float dist = Vector3.Distance(item.transform.position, localPlayer.transform.position);
            if (dist <= item.interactRadius && dist < myDist) return false;
        }

        return true;
    }

    private void TryCollectWeapon()
    {
        if (localPlayer == null) return;

        if (hud == null)
        {
            hud = FindObjectOfType<PlayerHUDController>();
        }

        // Check if player class matches Arthur Target (Tanker / index 3)
        if (localPlayer.CharacterClassIndex == targetClassIndex)
        {
            // Success! Unlock weapon 2 and skills, and auto-equip weapon 2
            Debug.Log($"[WeaponShieldArthur] Unlocking shield and sword for player {localPlayer.DisplayName}");

            // Clear interaction prompt
            if (hud != null)
            {
                hud.ShowInteractionPrompt(false, "");
                hud.SetWeapon2Locked(false, true);
                hud.SetSkillsUnlocked(true, true);
                hud.SelectWeapon(2);
            }

            // Sync with Player Controller and database/network
            localPlayer.UpdateStateFromHUD(2, false, true);
            localPlayer.SavePlayerStateToDatabase();

            // Play pickup animation
            var arthurPlayer = localPlayer.gameObject.GetComponent<ArthurPlayer>();
            if (arthurPlayer != null)
            {
                arthurPlayer.PlayAnimation("Idle_Pick", 0.1f);
            }

            // Despawn/Destroy item
            if (localPlayer.IsStandaloneMode)
            {
                Destroy(gameObject);
            }
            else
            {
                RequestDespawnServerRpc();
            }
        }
        else
        {
            // Fail! Mismatched class
            Debug.LogWarning($"[WeaponShieldArthur] Player {localPlayer.DisplayName} (class {localPlayer.CharacterClassIndex}) tried to steal Arthur's Weapon!");
            if (hud != null)
            {
                hud.ShowMissionAlert("bạn quá tham lam", 3.0f);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnServerRpc()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (isWithinRange && hud != null)
        {
            hud.ShowInteractionPrompt(false, "");
        }
    }
}
