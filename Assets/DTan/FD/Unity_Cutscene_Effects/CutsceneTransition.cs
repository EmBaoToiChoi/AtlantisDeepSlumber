using UnityEngine;

public class CutsceneTransition : MonoBehaviour
{
    public CanvasGroup canvasGroup;

    public void SetTransition(float value)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = Mathf.Clamp01(value);
    }
}
