using System.Collections.Generic;
using UnityEngine;

public interface IInteractableItem
{
    float InteractRadius { get; }
    Transform transform { get; }
}

public static class InteractionRegistry
{
    public static readonly List<IInteractableItem> ActiveItems = new List<IInteractableItem>(32);

    public static void Register(IInteractableItem item)
    {
        if (item != null && !ActiveItems.Contains(item))
        {
            ActiveItems.Add(item);
        }
    }

    public static void Unregister(IInteractableItem item)
    {
        if (item != null)
        {
            ActiveItems.Remove(item);
        }
    }
}
