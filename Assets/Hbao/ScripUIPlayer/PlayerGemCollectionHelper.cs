using UnityEngine;

public static class PlayerGemCollectionHelper
{
    public static bool HasCollected(Transform player, string dropGroupId)
    {
        if (player == null) return false;

        var simple = player.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) return simple.HasCollectedFromDropGroup(dropGroupId);

        var elena = player.GetComponentInParent<ElenaPlayer>();
        if (elena != null) return elena.HasCollectedFromDropGroup(dropGroupId);

        var leo = player.GetComponentInParent<LeoPlayer>();
        if (leo != null) return leo.HasCollectedFromDropGroup(dropGroupId);

        return false;
    }

    public static void AddCollected(Transform player, string dropGroupId)
    {
        if (player == null) return;

        var simple = player.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) simple.AddCollectedDropGroup(dropGroupId);

        var elena = player.GetComponentInParent<ElenaPlayer>();
        if (elena != null) elena.AddCollectedDropGroup(dropGroupId);

        var leo = player.GetComponentInParent<LeoPlayer>();
        if (leo != null) leo.AddCollectedDropGroup(dropGroupId);
    }

    public static void AddExperience(Transform player, float amount)
    {
        if (player == null) return;

        var simple = player.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) simple.AddExperience(amount);

        var elena = player.GetComponentInParent<ElenaPlayer>();
        if (elena != null) elena.AddExperience(amount);

        var leo = player.GetComponentInParent<LeoPlayer>();
        if (leo != null) leo.AddExperience(amount);
    }

    public static void TriggerOnCollectGemClientRpc(Transform player, string dropGroupId)
    {
        if (player == null) return;

        var simple = player.GetComponentInParent<SimplePlayerTest>();
        if (simple != null) simple.OnCollectGemClientRpc(dropGroupId);

        var elena = player.GetComponentInParent<ElenaPlayer>();
        if (elena != null) elena.OnCollectGemClientRpc(dropGroupId);

        var leo = player.GetComponentInParent<LeoPlayer>();
        if (leo != null) leo.OnCollectGemClientRpc(dropGroupId);
    }
}
