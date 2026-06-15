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

        int[] triangles = new int[]
        {
            // Cánh trái
            0, 4, 3,
            0, 5, 4,
            // Cánh phải
            0, 3, 2,
            0, 2, 1
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

    private void InitializeChevronPool()
    {
        Shader arrowShader = Shader.Find("Sprites/Default");
        Material arrowMat = null;
        if (arrowShader != null)
        {
            arrowMat = new Material(arrowShader);
            // Gán màu trắng trong suốt ngọc (màu trắng gốc của game)
            arrowMat.color = new Color(1f, 1f, 1f, 0.75f);
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
        if (targetTree == null)
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
            OnReachedCheckpoint();
            return;
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

    private void OnReachedCheckpoint()
    {
        Debug.Log($"[TreeGuidanceIndicator] Player đã đến Checkpoint cây gỗ: {targetTree.name}");
        
        if (hud != null)
        {
            hud.ShowMissionAlert("ĐÃ ĐẾN VỊ TRÍ CÂY GỖ! HÃY CHÉM VÀO CÂY ĐỂ THU THẬP GỖ!", 4.0f);
        }

        DestroyChevronsAndSelf();
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
