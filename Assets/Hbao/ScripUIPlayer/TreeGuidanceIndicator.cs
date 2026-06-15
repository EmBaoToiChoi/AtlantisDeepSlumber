using UnityEngine;
using System.Collections.Generic;

public class TreeGuidanceIndicator : MonoBehaviour
{
    [Tooltip("Cây gỗ mục tiêu cần chỉ hướng tới")]
    public ChoppableTree targetTree;
    
    [Tooltip("Khoảng cách kích hoạt đến gần cây để xóa checkpoint (mét)")]
    public float reachDistance = 4f;

    [Tooltip("Khoảng cách giữa các mũi tên trên đường đi")]
    public float spacing = 1.8f;

    [Tooltip("Tốc độ chạy dòng mũi tên chỉ đường")]
    public float scrollSpeed = 2.5f;

    private List<GameObject> chevronPool = new List<GameObject>();
    private const int MAX_CHEVRONS = 30; // Giới hạn số lượng mũi tên tối đa hiển thị
    private float scrollOffset = 0f;
    private PlayerHUDController hud;
    private bool hasAlerted = false;

    private int layerMask;
    private GameObject containerObject;
    private Mesh chevronMesh;
    private float[] cachedHeights;

    private void Start()
    {
        // Script này chỉ hoạt động trên máy khách cục bộ (local client) của người chơi sở hữu nhân vật
        if (!IsLocalPlayer())
        {
            Destroy(this);
            return;
        }

        hud = FindAnyObjectByType<PlayerHUDController>();
        layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast", "UI", "Water");
        
        // Tạo container tĩnh ngoài phân cấp của player để tối ưu hóa hiệu năng transform của Unity
        containerObject = new GameObject("TreeGuidanceContainer");
        
        InitializeChevronPool();
    }

    private bool IsLocalPlayer()
    {
        // 0. Nếu không có NetworkManager (chế độ standalone / Play in Editor trực tiếp)
        //    thì bất kỳ IPlayerHUDTarget nào trên GameObject này đều là local player
        bool isNetworkActive = Unity.Netcode.NetworkManager.Singleton != null 
                            && Unity.Netcode.NetworkManager.Singleton.IsListening;
        if (!isNetworkActive)
        {
            return GetComponent<IPlayerHUDTarget>() != null
                || GetComponent<LeoPlayer>() != null
                || GetComponent<ArthurPlayer>() != null
                || GetComponent<ElenaPlayer>() != null
                || GetComponent<MayaPlayer>() != null
                || GetComponent<SimplePlayerTest>() != null;
        }

        // 1. Kiểm tra trực tiếp qua static target của HUD
        if (PlayerHUDController.LocalPlayerTarget != null && PlayerHUDController.LocalPlayerTarget.gameObject == gameObject)
        {
            return true;
        }

        // 2. Kiểm tra thông thường qua interface
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

    private Mesh CreateChevronMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ChevronArrow";

        // Tọa độ đỉnh tạo hình chữ V kép (>) nằm ngang
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(0f, 0f, 0.4f),      // 0: Tip
            new Vector3(0.25f, 0f, -0.2f),  // 1: Right Outer
            new Vector3(0.12f, 0f, -0.2f),  // 2: Right Inner
            new Vector3(0f, 0f, 0.1f),      // 3: Inner Corner
            new Vector3(-0.12f, 0f, -0.2f), // 4: Left Inner
            new Vector3(-0.25f, 0f, -0.2f)  // 5: Left Outer
        };

        // Sử dụng thứ tự quay kim đồng hồ (Clockwise) để tránh bị Cull ẩn đi khi nhìn từ trên xuống
        int[] triangles = new int[]
        {
            // Cánh trái (Clockwise)
            0, 3, 4,
            0, 4, 5,
            // Cánh phải (Clockwise)
            0, 2, 3,
            0, 1, 2
        };

        Vector3[] normals = new Vector3[]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };

        Vector2[] uv = new Vector2[]
        {
            new Vector2(0.5f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0.74f, 0f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.26f, 0f),
            new Vector2(0f, 0f)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.uv = uv;

        return mesh;
    }

    private Material FindURPLitMaterial()
    {
        var allRends = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in allRends)
        {
            if (r != null && r.sharedMaterial != null)
            {
                Shader s = r.sharedMaterial.shader;
                if (s != null)
                {
                    string shaderName = s.name.ToLower();
                    if (shaderName.Contains("universal render pipeline/lit") || 
                        shaderName.Contains("urp/lit") || 
                        (shaderName.Contains("lit") && !shaderName.Contains("speedtree") && !shaderName.Contains("nature") && !shaderName.Contains("terrain")))
                    {
                        return new Material(r.sharedMaterial);
                    }
                }
            }
        }
        
        // Fallback to any valid material from the player or scene
        var playerRends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in playerRends)
        {
            if (r != null && r.sharedMaterial != null)
            {
                return new Material(r.sharedMaterial);
            }
        }

        foreach (var r in allRends)
        {
            if (r != null && r.sharedMaterial != null)
            {
                return new Material(r.sharedMaterial);
            }
        }
        return null;
    }

    private void InitializeChevronPool()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material arrowMat = new Material(shader);
        Color chevronColor = new Color(1f, 1f, 1f, 0.9f);

        if (shader.name == "Sprites/Default")
        {
            arrowMat.SetColor("_Color", chevronColor);
        }
        else if (shader.name == "Standard")
        {
            arrowMat.SetFloat("_Mode", 3f); // Transparent
            arrowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            arrowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            arrowMat.SetInt("_ZWrite", 0);
            arrowMat.DisableKeyword("_ALPHATEST_ON");
            arrowMat.EnableKeyword("_ALPHABLEND_ON");
            arrowMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            arrowMat.SetColor("_Color", chevronColor);
            arrowMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            arrowMat.color = chevronColor;
        }

        chevronMesh = CreateChevronMesh();
        cachedHeights = new float[MAX_CHEVRONS];

        for (int i = 0; i < MAX_CHEVRONS; i++)
        {
            GameObject chevron = new GameObject($"GuidanceChevron_{i}");
            if (containerObject != null)
            {
                chevron.transform.SetParent(containerObject.transform, false);
            }
            
            MeshFilter mf = chevron.AddComponent<MeshFilter>();
            mf.sharedMesh = chevronMesh;

            MeshRenderer mr = chevron.AddComponent<MeshRenderer>();
            if (arrowMat != null)
            {
                mr.sharedMaterial = arrowMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            chevron.SetActive(false);
            chevronPool.Add(chevron);
            cachedHeights[i] = 0f;
        }
    }

    private void Update()
    {
        bool isNetworkActive = Unity.Netcode.NetworkManager.Singleton != null 
                            && Unity.Netcode.NetworkManager.Singleton.IsListening;
        bool isTreeCut = targetTree == null || (isNetworkActive ? targetTree.isCutDown.Value : !targetTree.gameObject.activeInHierarchy);

        if (isTreeCut)
        {
            DestroyChevronsAndSelf();
            return;
        }

        Vector3 start = transform.position;
        Vector3 end = targetTree.transform.position;
        
        // Kiểm tra xem đã đến đích chưa
        float distance = Vector3.Distance(start, end);
        if (distance <= reachDistance)
        {
            // Ẩn tất cả các chevrons khi người chơi đứng gần cây
            for (int i = 0; i < chevronPool.Count; i++)
            {
                if (chevronPool[i] != null)
                {
                    chevronPool[i].SetActive(false);
                }
            }

            if (!hasAlerted)
            {
                hasAlerted = true;
                Debug.Log($"[TreeGuidanceIndicator] Player đã đến Checkpoint cây gỗ: {targetTree.name}");
                if (hud != null)
                {
                    hud.ShowMissionAlert("ĐÃ ĐẾN VỊ TRÍ CÂY GỖ! HÃY CHÉM VÀO CÂY ĐỂ THU THẬP GỖ!", 4.0f);
                }
            }
            return;
        }

        // Reset trạng thái báo tin nếu người chơi di chuyển ra xa cây để khi quay lại có thể hiện thông báo tiếp
        if (distance > reachDistance + 1.5f)
        {
            hasAlerted = false;
        }

        // Tính toán hướng phẳng trên mặt đất
        Vector3 direction = (end - start).normalized;
        direction.y = 0f;
        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // Cuộn dịch chuyển các mũi tên
        scrollOffset += Time.deltaTime * scrollSpeed;
        if (scrollOffset >= spacing)
        {
            scrollOffset -= spacing;
        }

        // Phân bổ và sắp xếp các chevrons dọc đường đi
        for (int i = 0; i < chevronPool.Count; i++)
        {
            float d = (i * spacing) + scrollOffset;
            
            if (d < distance - 1.5f)
            {
                GameObject chevron = chevronPool[i];
                chevron.SetActive(true);
                Vector3 pos = start + direction * d;
                
                // Tối ưu hóa: Chỉ chạy raycast 1 lần mỗi 5 frame cho mỗi chevron để bảo vệ CPU
                if ((Time.frameCount + i) % 5 == 0 || cachedHeights[i] == 0f)
                {
                    if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, layerMask))
                    {
                        cachedHeights[i] = hit.point.y + 0.08f; // Tránh bị chìm nhẹ vào địa hình (z-fighting)
                    }
                    else
                    {
                        cachedHeights[i] = start.y + 0.08f;
                    }
                }
                
                pos.y = cachedHeights[i];
                chevron.transform.position = pos;
                chevron.transform.rotation = lookRotation;
            }
            else
            {
                chevronPool[i].SetActive(false);
            }
        }
    }

    private void DestroyChevronsAndSelf()
    {
        if (containerObject != null)
        {
            Destroy(containerObject);
        }
        if (chevronMesh != null)
        {
            Destroy(chevronMesh);
        }
        chevronPool.Clear();
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (containerObject != null)
        {
            Destroy(containerObject);
        }
        if (chevronMesh != null)
        {
            Destroy(chevronMesh);
        }
        chevronPool.Clear();
    }
}
