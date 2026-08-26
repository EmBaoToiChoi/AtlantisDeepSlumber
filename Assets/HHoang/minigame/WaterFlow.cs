using UnityEngine;

public class WaterFlow : MonoBehaviour
{
    [SerializeField] private float speedX = 0.05f;
    [SerializeField] private float speedY = 0.05f;
    
    private Renderer rend;
    private Material mat;

    void Start()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material;
    }

    void Update()
    {
        float offsetX = Time.time * speedX;
        float offsetY = Time.time * speedY;
        
        // Trượt texture chính và normal map
        mat.SetTextureOffset("_MainTex", new Vector2(offsetX, offsetY));
        if (mat.HasProperty("_BumpMap"))
        {
            mat.SetTextureOffset("_BumpMap", new Vector2(offsetX, offsetY));
        }
    }
}