using UnityEngine;

public class TreeGuidanceIndicator : MonoBehaviour
{
    [Tooltip("Cây gỗ mục tiêu cần chỉ hướng tới")]
    public ChoppableTree targetTree;
    
    [Tooltip("Khoảng cách kích hoạt đến gần cây để xóa checkpoint (mét)")]
    public float reachDistance = 4f;

    [Tooltip("Khoảng cách giữa các mũi tên chỉ hướng")]
    public float spacing = 1.8f;

    [Tooltip("Tốc độ chạy dòng mũi tên chỉ đường")]
    public float scrollSpeed = 2.5f;

    private LineRenderer lineRenderer;
    private Material lineMat;
    private Texture2D dashTexture;
    private float scrollOffset = 0f;
    private PlayerHUDController hud;

    private int layerMask;
    private Vector3 lastPlayerPosition;
    private Vector3[] cachedPositions;
    private const int positionCount = 10;

    private void Start()
    {
        hud = FindAnyObjectByType<PlayerHUDController>();
        layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast", "UI", "Water");
        
        // Tạo GameObject phụ làm GuidanceLineRenderer
        GameObject lineObj = new GameObject("GuidanceLineRenderer");
        lineObj.transform.SetParent(transform, false);
        
        lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 0.5f;
        lineRenderer.positionCount = positionCount;
        lineRenderer.textureMode = LineTextureMode.Tile;

        // Tạo texture chứa các hình mũi tên chỉ hướng '>' hướng sang phải (về phía cây mục tiêu)
        dashTexture = new Texture2D(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                dashTexture.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
            }
        }

        // Vẽ hình chevron '>' hướng sang phải
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                // Điểm đỉnh (tip) nằm ở x = 24, y = 16
                // Hai nhánh xéo kéo về x = 8, y = 0 và y = 32
                float targetX = 24f - Mathf.Abs(y - 16f) * 1.25f;
                float dist = Mathf.Abs(x - targetX);
                
                if (dist <= 3.5f) // Độ dày đường vẽ + viền mờ
                {
                    float alpha = Mathf.Clamp01(1f - (dist / 3.5f));
                    // Làm mờ dần phần đuôi mũi tên ở biên trái để tạo cảm giác trôi chảy đẹp mắt
                    alpha *= Mathf.Clamp01((x - 2f) / 6f);
                    dashTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        dashTexture.wrapMode = TextureWrapMode.Repeat;
        dashTexture.filterMode = FilterMode.Bilinear;
        dashTexture.Apply();

        Shader lineShader = Shader.Find("Unlit/Transparent");
        if (lineShader == null)
        {
            lineShader = Shader.Find("Sprites/Default");
        }

        if (lineShader != null)
        {
            lineMat = new Material(lineShader);
            lineMat.mainTexture = dashTexture;
            lineMat.color = Color.white; // Để màu sắc được kiểm soát hoàn toàn bởi Gradient của LineRenderer
            lineRenderer.sharedMaterial = lineMat;
        }

        // Áp dụng gradient màu riêng biệt cho từng lớp nhân vật
        int classIndex = GetCharacterClassIndex();
        lineRenderer.colorGradient = GetGradientForClass(classIndex);

        cachedPositions = new Vector3[positionCount];
        lastPlayerPosition = transform.position;

        UpdateLinePositions();
    }

    private int GetCharacterClassIndex()
    {
        var hudTarget = GetComponent<IPlayerHUDTarget>();
        if (hudTarget != null)
        {
            return hudTarget.CharacterClassIndex;
        }

        // Fallbacks cho các lớp cụ thể nếu không qua interface
        if (GetComponent<LeoPlayer>() != null) return 0;
        if (GetComponent<MayaPlayer>() != null) return 1;
        if (GetComponent<ElenaPlayer>() != null) return 2;
        if (GetComponent<ArthurPlayer>() != null) return 3;

        return 4; // Mặc định / SimplePlayerTest
    }

    private Gradient GetGradientForClass(int classIndex)
    {
        Gradient gradient = new Gradient();
        Color startCol;
        Color endCol;

        switch (classIndex)
        {
            case 0: // Leo: Neon Cyan/Blue
                startCol = new Color(0f, 0.8f, 1f);
                endCol = new Color(0f, 0.2f, 1f);
                break;
            case 1: // Maya: Neon Pink/Purple
                startCol = new Color(1f, 0f, 0.8f);
                endCol = new Color(0.5f, 0f, 1f);
                break;
            case 2: // Elena: Neon Green/Yellow
                startCol = new Color(0.1f, 1f, 0.1f);
                endCol = new Color(0.9f, 1f, 0f);
                break;
            case 3: // Arthur: Neon Orange/Red
                startCol = new Color(1f, 0.5f, 0f);
                endCol = new Color(1f, 0f, 0f);
                break;
            default: // Default (SimplePlayerTest/Khác): White/Cyan
                startCol = new Color(1f, 1f, 1f);
                endCol = new Color(0f, 0.8f, 1f);
                break;
        }

        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startCol, 0.0f), new GradientColorKey(endCol, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0.0f), new GradientAlphaKey(0.85f, 1.0f) }
        );

        return gradient;
    }

    private bool IsLocalPlayer()
    {
        var hudTarget = GetComponent<IPlayerHUDTarget>();
        if (hudTarget != null)
        {
            return hudTarget.IsOwner || hudTarget.IsStandaloneMode || hudTarget.isStandaloneMode;
        }

        // Fallbacks
        var arthur = GetComponent<ArthurPlayer>();
        if (arthur != null) return arthur.IsOwner || arthur.isStandaloneMode;

        var leo = GetComponent<LeoPlayer>();
        if (leo != null) return leo.IsOwner || leo.isStandaloneMode;

        var elena = GetComponent<ElenaPlayer>();
        if (elena != null) return elena.IsOwner || elena.isStandaloneMode;

        var maya = GetComponent<MayaPlayer>();
        if (maya != null) return maya.IsOwner || maya.isStandaloneMode;

        var spt = GetComponent<SimplePlayerTest>();
        if (spt != null) return spt.IsOwner || spt.isStandaloneMode;

        return false;
    }

    private void Update()
    {
        if (targetTree == null)
        {
            DestroyLineAndSelf();
            return;
        }

        Vector3 start = transform.position;
        Vector3 end = targetTree.transform.position;
        
        // Tính toán khoảng cách tới checkpoint cây gỗ
        float distance = Vector3.Distance(start, end);
        if (distance <= reachDistance)
        {
            OnReachedCheckpoint();
            return;
        }

        // Cuộn offset texture để tạo hiệu ứng luồng năng lượng hình '>' chạy dọc đường đi
        if (lineMat != null)
        {
            // Điều chỉnh tỷ lệ lặp texture theo khoảng cách thực tế để các dấu '>' không bị kéo giãn hay co cụm
            lineMat.mainTextureScale = new Vector2(distance / spacing, 1f);

            // Cuộn dấu mũi tên di chuyển hướng về phía cây gỗ mục tiêu
            scrollOffset -= Time.deltaTime * scrollSpeed;
            lineMat.mainTextureOffset = new Vector2(scrollOffset, 0f);
        }

        // Tối ưu hóa: Chỉ cập nhật tọa độ các điểm snapping mặt đất khi di chuyển > 0.3m hoặc mỗi 5 frame
        bool shouldUpdate = (Time.frameCount % 5 == 0) || (Vector3.Distance(start, lastPlayerPosition) > 0.3f);
        if (shouldUpdate)
        {
            UpdateLinePositions();
            lastPlayerPosition = start;
        }
    }

    private void UpdateLinePositions()
    {
        if (targetTree == null || lineRenderer == null) return;

        Vector3 start = transform.position;
        Vector3 end = targetTree.transform.position;

        for (int i = 0; i < positionCount; i++)
        {
            float t = (float)i / (positionCount - 1);
            Vector3 pos = Vector3.Lerp(start, end, t);

            // Bắn raycast từ trên cao xuống để bám sát mặt đất địa hình
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f, layerMask))
            {
                pos.y = hit.point.y + 0.1f; // nâng nhẹ 10cm chống z-fighting
            }
            else
            {
                pos.y = start.y + 0.1f;
            }

            cachedPositions[i] = pos;
        }

        lineRenderer.SetPositions(cachedPositions);
    }

    private void OnReachedCheckpoint()
    {
        Debug.Log($"[TreeGuidanceIndicator] Player {gameObject.name} đã đến Checkpoint cây gỗ: {targetTree.name}");
        
        // Chỉ hiển thị cảnh báo nhiệm vụ lên HUD của người chơi local sở hữu nhân vật này
        if (IsLocalPlayer() && hud != null)
        {
            hud.ShowMissionAlert("ĐÃ ĐẾN VỊ TRÍ CÂY GỖ! HÃY CHÉM VÀO CÂY ĐỂ THU THẬP GỖ!", 4.0f);
        }

        DestroyLineAndSelf();
    }

    private void DestroyLineAndSelf()
    {
        if (lineRenderer != null && lineRenderer.gameObject != null)
        {
            Destroy(lineRenderer.gameObject);
        }
        if (lineMat != null)
        {
            Destroy(lineMat);
        }
        if (dashTexture != null)
        {
            Destroy(dashTexture);
        }
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (lineRenderer != null && lineRenderer.gameObject != null)
        {
            Destroy(lineRenderer.gameObject);
        }
        if (lineMat != null)
        {
            Destroy(lineMat);
        }
        if (dashTexture != null)
        {
            Destroy(dashTexture);
        }
    }
}
