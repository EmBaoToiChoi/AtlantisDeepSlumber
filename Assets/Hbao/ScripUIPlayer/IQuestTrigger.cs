using UnityEngine;

public interface IQuestTrigger
{
    bool IsQuestCompleted { get; }
    bool IsQuestActive { get; }
}
