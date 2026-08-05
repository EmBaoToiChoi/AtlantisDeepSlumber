using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Script vẽ trực tiếp lên một bức tường cụ thể (dành cho Minigame / Puzzle).
/// Tự động tạo Material Instance riêng cho bức tường đó nên KHÔNG ảnh hưởng đến các bức tường khác cùng Prefab.
/// </summary>
public class WallDrawingPuzzle : MonoBehaviour
{
    [Header("Cấu Hình Bức Tường Mục Tiêu")]
    [Tooltip("Kéo bức tường Prefab_Wall_01 (17) vào đây (hoặc gắn script này trực tiếp lên bức tường đó).")]
    [SerializeField] private Collider targetWallCollider;

    [Header("Cấu Hình Cọ Vẽ")]
    [SerializeField] private Color brushColor = new Color(1f, 0.35f, 0.05f, 1f); // Màu lửa cam đỏ rực rỡ
    [Range(5, 50)]
    [SerializeField] private int brushRadius = 15; // Độ dầy nét vẽ
    [SerializeField] private int textureResolution = 1024; // Độ phân giải hình vẽ

    [Header("Cấu Hình Giải Đố (Puzzle Validation)")]
    [Tooltip("Phần trăm diện tích nét vẽ cần hoàn thành để kích hoạt mở giải đố (0 - 100%)")]
    [Range(1, 100)]
    [SerializeField] private float requiredPaintedPercentage = 15f; 
    public UnityEvent onPuzzleSolved;

    private Texture2D drawableTexture;
    private Material wallMaterialInstance;
    private Vector2? lastUV = null;
    private int totalPixelsCount;
    private int paintedPixelsCount = 0;
    private bool isSolved = false;

    private void Start()
    {
        if (targetWallCollider == null)
        {
            targetWallCollider = GetComponent<Collider>();
        }

        if (targetWallCollider == null)
        {
            Debug.LogError("[WallDrawingPuzzle] Chưa gán Collider của bức tường mục tiêu!");
            return;
        }

        InitializeTexture();
    }

    private void InitializeTexture()
    {
        // Tạo Texture2D động trong RAM có hỗ trợ Kênh Alpha (trong suốt)
        drawableTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);
        drawableTexture.filterMode = FilterMode.Bilinear;

        // Xóa sạch texture về nền trong suốt
        Color[] clearColors = new Color[textureResolution * textureResolution];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = Color.clear;
        }
        drawableTexture.SetPixels(clearColors);
        drawableTexture.Apply();

        totalPixelsCount = textureResolution * textureResolution;

        // Lấy Renderer của bức tường (Unity sẽ TỰ ĐỘNG tạo Instance riêng cho bức tường này!)
        Renderer wallRenderer = targetWallCollider.GetComponent<Renderer>();
        if (wallRenderer != null)
        {
            wallMaterialInstance = wallRenderer.material; // Tạo Material Instance duy nhất cho object này!
            
            // Gán texture vừa vẽ làm MainTexture (hoặc Detail Map tùy shader)
            wallMaterialInstance.mainTexture = drawableTexture;
            
            Debug.Log($"[WallDrawingPuzzle] Đã khởi tạo vùng vẽ cho bức tường: {targetWallCollider.gameObject.name}");
        }
    }

    private void Update()
    {
        if (isSolved) return;

        // Người chơi nhấn giữ chuột trái (hoặc chạm cảm ứng)
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // CHỈ VẼ KHI RAYCAST CHẠM ĐÚNG COLLIDER BỨC TƯỜNG NÀY!
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider == targetWallCollider)
                {
                    Vector2 currentUV = hit.textureCoord;

                    if (lastUV.HasValue)
                    {
                        // Nối nét giữa 2 vị trí chuột liên tiếp để đường vẽ mượt mà không bị ngắt nét
                        DrawLineBetweenUVs(lastUV.Value, currentUV);
                    }
                    else
                    {
                        DrawBrushAtUV(currentUV);
                    }

                    lastUV = currentUV;
                    CheckPuzzleProgress();
                }
                else
                {
                    lastUV = null; // Chuột rà sang object khác
                }
            }
            else
            {
                lastUV = null;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            lastUV = null; // Thả chuột ra
        }
    }

    /// <summary>
    /// Vẽ một hình tròn cọ vẽ tại tọa độ UV của bề mặt tường
    /// </summary>
    private void DrawBrushAtUV(Vector2 uv)
    {
        int pixelX = (int)(uv.x * textureResolution);
        int pixelY = (int)(uv.y * textureResolution);

        for (int x = -brushRadius; x <= brushRadius; x++)
        {
            for (int y = -brushRadius; y <= brushRadius; y++)
            {
                if (x * x + y * y <= brushRadius * brushRadius)
                {
                    int px = pixelX + x;
                    int py = pixelY + y;

                    if (px >= 0 && px < textureResolution && py >= 0 && py < textureResolution)
                    {
                        Color oldColor = drawableTexture.GetPixel(px, py);
                        if (oldColor.a == 0) // Nếu pixel này chưa tô
                        {
                            paintedPixelsCount++;
                        }
                        drawableTexture.SetPixel(px, py, brushColor);
                    }
                }
            }
        }
        drawableTexture.Apply();
    }

    /// <summary>
    /// Vẽ đường thẳng mượt giữa 2 điểm chuột di chuyển nhanh
    /// </summary>
    private void DrawLineBetweenUVs(Vector2 startUV, Vector2 endUV)
    {
        float distance = Vector2.Distance(startUV, endUV);
        int steps = Mathf.CeilToInt(distance * textureResolution / (brushRadius * 0.5f));
        steps = Mathf.Max(steps, 1);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 interpolatedUV = Vector2.Lerp(startUV, endUV, t);
            DrawBrushAtUV(interpolatedUV);
        }
    }

    /// <summary>
    /// Kiểm tra tỉ lệ đã vẽ để kích hoạt giải đố
    /// </summary>
    private void CheckPuzzleProgress()
    {
        float currentPercentage = ((float)paintedPixelsCount / totalPixelsCount) * 100f;
        
        if (currentPercentage >= requiredPaintedPercentage && !isSolved)
        {
            isSolved = true;
            Debug.Log($"<color=green>[WallDrawingPuzzle] GIẢI ĐỐ HOÀN THÀNH! Tỉ lệ vẽ đạt: {currentPercentage:F1}%</color>");
            onPuzzleSolved?.Invoke();
        }
    }

    /// <summary>
    /// Hàm xóa sạch nét vẽ (Dùng khi muốn Reset puzzle)
    /// </summary>
    public void ResetDrawing()
    {
        if (drawableTexture == null) return;
        Color[] clearColors = new Color[textureResolution * textureResolution];
        for (int i = 0; i < clearColors.Length; i++) clearColors[i] = Color.clear;
        drawableTexture.SetPixels(clearColors);
        drawableTexture.Apply();
        paintedPixelsCount = 0;
        isSolved = false;
    }
}
