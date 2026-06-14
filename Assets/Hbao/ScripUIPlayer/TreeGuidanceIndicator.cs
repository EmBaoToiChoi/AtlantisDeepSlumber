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

    private void Start()
    {
        hud = FindObjectOfType<PlayerHUDController>();
        InitializeChevronPool();
    }

    private void InitializeChevronPool()
    {
        // Tạo trước pool các mũi tên để tối ưu hiệu năng (không Instantiate/Destroy liên tục)
        Shader arrowShader = Shader.Find("Sprites/Default");
        Material arrowMat = null;
        if (arrowShader != null)
        {
            arrowMat = new Material(arrowShader);
            arrowMat.color = new Color(1f, 1f, 1f, 0.75f); // Màu trắng trong suốt ngọc
        }

        for (int i = 0; i < MAX_CHEVRONS; i++)
        {
            GameObject chevron = new GameObject($"GuidanceChevron_{i}");
            chevron.transform.SetParent(transform, false);
            
            // Xây dựng hình dạng chữ > bằng 2 hình hộp primitive Cube xoay góc
            GameObject leftBranch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(leftBranch.GetComponent<Collider>());
            leftBranch.transform.SetParent(chevron.transform, false);
            leftBranch.transform.localPosition = new Vector3(-0.12f, 0f, 0f);
            leftBranch.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            leftBranch.transform.localScale = new Vector3(0.05f, 0.02f, 0.3f);

            GameObject rightBranch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(rightBranch.GetComponent<Collider>());
            rightBranch.transform.SetParent(chevron.transform, false);
            rightBranch.transform.localPosition = new Vector3(0.12f, 0f, 0f);
            rightBranch.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            rightBranch.transform.localScale = new Vector3(0.05f, 0.02f, 0.3f);

            if (arrowMat != null)
            {
                leftBranch.GetComponent<Renderer>().material = arrowMat;
                rightBranch.GetComponent<Renderer>().material = arrowMat;
            }

            chevron.SetActive(false);
            chevronPool.Add(chevron);
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
        
        // Tính toán khoảng cách
        float distance = Vector3.Distance(start, end);
        if (distance <= reachDistance)
        {
            OnReachedCheckpoint();
            return;
        }

        // Tính hướng đi dạng phẳng trên mặt đất
        Vector3 direction = (end - start).normalized;
        direction.y = 0f;
        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // Cập nhật vị trí cuộn chạy của chuỗi mũi tên chỉ đường
        scrollOffset += Time.deltaTime * scrollSpeed;
        if (scrollOffset >= spacing)
        {
            scrollOffset -= spacing;
        }

        // Phân bổ các mũi tên trong pool dọc theo đường từ chân player tới cây
        for (int i = 0; i < chevronPool.Count; i++)
        {
            float d = (i * spacing) + scrollOffset;
            
            // Chỉ hiển thị mũi tên nếu nó nằm trong khoảng cách tới cây
            if (d < distance - 1.5f && i < chevronPool.Count)
            {
                chevronPool[i].SetActive(true);
                Vector3 pos = start + direction * d;
                
                // Snap xuống mặt đất bằng raycast để mũi tên uốn lượn theo địa hình đất đai
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
                {
                    pos.y = hit.point.y + 0.08f; // Cao hơn mặt đất 8cm để tránh bị chìm (z-fighting)
                }
                else
                {
                    pos.y = start.y + 0.08f;
                }

                chevronPool[i].transform.position = pos;
                chevronPool[i].transform.rotation = lookRotation;
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
        foreach (var chevron in chevronPool)
        {
            if (chevron != null) Destroy(chevron);
        }
        chevronPool.Clear();
        Destroy(this);
    }

    private void OnDestroy()
    {
        foreach (var chevron in chevronPool)
        {
            if (chevron != null) Destroy(chevron);
        }
        chevronPool.Clear();
    }
}
