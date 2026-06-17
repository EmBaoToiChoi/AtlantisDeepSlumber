using UnityEngine;
using UnityEngine.UI;

public class GlitchEffect : MonoBehaviour
{
    public RawImage noiseImage;

    public void SetIntensity(float intensity)
    {
        if (noiseImage == null) return;

        Color c = noiseImage.color;
        c.a = Mathf.Clamp01(intensity);
        noiseImage.color = c;

        noiseImage.uvRect = new Rect(
            Random.value,
            Random.value,
            1f,
            1f
        );
    }
}
