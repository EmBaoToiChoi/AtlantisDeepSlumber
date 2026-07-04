/*using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ElementalScanner : NetworkBehaviour 
{
    public bool hasElementalSight = false; 

    [Header("Cấu hình Quét Map")]
    public float scanSpeed = 30f;       
    public float maxScanRadius = 50f;   
    public float highlightDuration = 4f;

    [Header("Hiệu ứng Sóng quét (Visual Wave)")]
    public GameObject scanWavePrefab; // MỚI THÊM: Kéo Prefab quả cầu sóng vào đây
    private GameObject currentWave;   // MỚI THÊM: Lưu trữ quả cầu đang quét

    private bool isScanning = false;
    private float currentRadius = 0f;
    private Vector3 scanCenter;
    
    private static List<ElementalTarget> allTargets = new List<ElementalTarget>();

    public static void RegisterTarget(ElementalTarget target)
    {
        if (!allTargets.Contains(target)) allTargets.Add(target);
    }

    public static void UnregisterTarget(ElementalTarget target)
    {
        if (allTargets.Contains(target)) allTargets.Remove(target); 
    }

    void Update()
    {
        if (!IsOwner || !hasElementalSight) return;

        if (Input.GetKeyDown(KeyCode.Z) && !isScanning)
        {
            StartScan();
        }

        if (isScanning)
        {
            currentRadius += scanSpeed * Time.deltaTime;

            // MỚI THÊM: Phình to quả cầu sóng quét ra xung quanh
            if (currentWave != null)
            {
                // Bán kính = currentRadius, nên Đường kính (Scale) = currentRadius * 2
                float diameter = currentRadius * 2f;
                currentWave.transform.localScale = new Vector3(diameter, diameter, diameter);
            }

            foreach (var target in allTargets)
            {
                if (target == null) continue;
                float distance = Vector3.Distance(scanCenter, target.transform.position);
                if (distance <= currentRadius)
                {
                    target.SetHighlight(true);
                }
            }

            if (currentRadius >= maxScanRadius)
            {
                isScanning = false;
                
                // MỚI THÊM: Huỷ quả cầu khi quét xong
                if (currentWave != null) Destroy(currentWave);
                
                Invoke(nameof(TurnOffAllHighlights), highlightDuration);
            }
        }
    }

    void StartScan()
    {
        isScanning = true;
        currentRadius = 0f;
        scanCenter = transform.position; 

        // MỚI THÊM: Tạo quả cầu ngay vị trí nhân vật lúc bắt đầu quét
        if (scanWavePrefab != null)
        {
            currentWave = Instantiate(scanWavePrefab, scanCenter, Quaternion.identity);
            currentWave.transform.localScale = Vector3.zero; // Bắt đầu từ 0
        }
    }

    void TurnOffAllHighlights()
    {
        foreach (var target in allTargets)
        {
            if (target != null) target.SetHighlight(false);
        }
    }
}*/
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ElementalScanner : NetworkBehaviour 
{
    public bool hasElementalSight = false; 

    [Header("Cấu hình Quét Map")]
    public float scanSpeed = 30f;       
    public float maxScanRadius = 50f;   
    public float highlightDuration = 4f;

    [Header("Hiệu ứng Sóng quét (Visual Wave)")]
    public GameObject scanWavePrefab; 
    private GameObject currentWave;   

    private bool isScanning = false;
    private float currentRadius = 0f;
    private Vector3 scanCenter;
    
    private static List<ElementalTarget> allTargets = new List<ElementalTarget>();

    public static void RegisterTarget(ElementalTarget target)
    {
        if (!allTargets.Contains(target)) allTargets.Add(target);
    }

    public static void UnregisterTarget(ElementalTarget target)
    {
        if (allTargets.Contains(target)) allTargets.Remove(target); 
    }

    void Update()
    {
        // 1. BỘ LỌC PHÍM Z: CHỈ DUY NHẤT Owner (Máy của bạn) và có skill (Maya) mới qua được chốt này
        if (IsOwner && hasElementalSight)
        {
            if (Input.GetKeyDown(KeyCode.Z) && !isScanning)
            {
                // Báo lên Server để Server hô hào tất cả mọi người cùng bật hiệu ứng
                TriggerScanServerRpc();
            }
        }

        // 2. BỘ XỬ LÝ HIỆU ỨNG: Chạy trên màn hình của TẤT CẢ mọi người
        if (isScanning)
        {
            currentRadius += scanSpeed * Time.deltaTime;

            // Phình to quả cầu sóng quét ra xung quanh
            if (currentWave != null)
            {
                float diameter = currentRadius * 2f;
                currentWave.transform.localScale = new Vector3(diameter, diameter, diameter);
            }

            foreach (var target in allTargets)
            {
                if (target == null) continue;
                float distance = Vector3.Distance(scanCenter, target.transform.position);
                if (distance <= currentRadius)
                {
                    // Tự bật sáng trên từng máy
                    target.SetHighlight(true);
                }
            }

            if (currentRadius >= maxScanRadius)
            {
                isScanning = false;
                
                // Huỷ quả cầu khi quét xong
                if (currentWave != null) Destroy(currentWave);
                
                Invoke(nameof(TurnOffAllHighlights), highlightDuration);
            }
        }
    }

    // --- ĐỒNG BỘ MẠNG (RPC) ---

    [ServerRpc]
    void TriggerScanServerRpc()
    {
        // Server nhận lệnh từ con Maya, rồi phát lệnh xuống toàn bộ máy trong phòng
        TriggerScanClientRpc();
    }

    [ClientRpc]
    void TriggerScanClientRpc()
    {
        // Khởi động quá trình quét trên MỌI MÁY
        isScanning = true;
        currentRadius = 0f;
        scanCenter = transform.position; 

        // Tạo quả cầu ngay vị trí nhân vật lúc bắt đầu quét
        if (scanWavePrefab != null)
        {
            currentWave = Instantiate(scanWavePrefab, scanCenter, Quaternion.identity);
            currentWave.transform.localScale = Vector3.zero; 
        }
    }

    void TurnOffAllHighlights()
    {
        foreach (var target in allTargets)
        {
            if (target != null) target.SetHighlight(false);
        }
    }
}