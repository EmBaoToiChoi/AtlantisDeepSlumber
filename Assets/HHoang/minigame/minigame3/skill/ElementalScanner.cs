using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ElementalScanner : NetworkBehaviour 
{
    public bool hasElementalSight = false; 

    [Header("Cấu hình Quét Map")]
    public float scanSpeed = 30f;       
    public float maxScanRadius = 50f;   
    public float highlightDuration = 4f;

    [Header("Hiệu ứng Sóng quét (Visual Wave)")]
    [Tooltip("Prefab hiệu ứng sóng quét (ví dụ: Effect_09_HoloShield)")]
    public GameObject scanWavePrefab; 

    [Tooltip("Hệ số nhân kích thước sóng quét (mặc định 1.0)")]
    public float waveScaleMultiplier = 1.0f;

    [Tooltip("Độ cao tâm sóng quét tính từ chân nhân vật (1.0m tương đương ngang ngực)")]
    public float waveHeightOffset = 1.0f;

    [Tooltip("Tự động kích hoạt chế độ Loop và Hierarchy Scaling cho toàn bộ Particle Systems trong Prefab")]
    public bool autoLoopParticles = true;

    [Tooltip("Tự động tắt các hiệu ứng tia đạn va chạm (Hit/Shot) phụ kèm theo trong Prefab")]
    public bool hideHitSubEffects = true;

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
        // 1. BỘ LỌC PHÍM Z: Chỉ Owner (máy của người chơi) có kỹ năng (Maya/Elemental Sight) mới kích hoạt
        if (IsOwner && hasElementalSight)
        {
            if (Input.GetKeyDown(KeyCode.Z) && !isScanning)
            {
                // Báo lên Server để Server đồng bộ hiệu ứng cho tất cả người chơi
                TriggerScanServerRpc();
            }
        }

        // 2. BỘ XỬ LÝ HIỆU ỨNG QUÉT: Chạy mượt mà trên tất cả Client
        if (isScanning)
        {
            currentRadius += scanSpeed * Time.deltaTime;

            // Phình to quả cầu sóng quét theo bán kính thực tế
            if (currentWave != null)
            {
                float diameter = currentRadius * 2f * waveScaleMultiplier;
                currentWave.transform.localScale = new Vector3(diameter, diameter, diameter);
            }

            // Quét và làm sáng các mục tiêu nằm trong tầm quét
            for (int i = 0; i < allTargets.Count; i++)
            {
                var target = allTargets[i];
                if (target == null) continue;

                float distance = Vector3.Distance(scanCenter, target.transform.position);
                if (distance <= currentRadius)
                {
                    target.SetHighlight(true);
                }
            }

            // Hoàn tất quét khi đạt bán kính tối đa
            if (currentRadius >= maxScanRadius)
            {
                isScanning = false;
                
                // Huỷ quả cầu sóng quét khi hoàn thành
                if (currentWave != null)
                {
                    Destroy(currentWave);
                    currentWave = null;
                }
                
                CancelInvoke(nameof(TurnOffAllHighlights));
                Invoke(nameof(TurnOffAllHighlights), highlightDuration);
            }
        }
    }

    // --- ĐỒNG BỘ MẠNG (RPC) ---

    [ServerRpc]
    private void TriggerScanServerRpc()
    {
        TriggerScanClientRpc();
    }

    [ClientRpc]
    private void TriggerScanClientRpc()
    {
        isScanning = true;
        currentRadius = 0f;
        scanCenter = transform.position + Vector3.up * waveHeightOffset; 

        // Khởi tạo quả cầu hiệu ứng tại vị trí quét
        if (scanWavePrefab != null)
        {
            currentWave = Instantiate(scanWavePrefab, scanCenter, Quaternion.identity);
            currentWave.transform.localScale = Vector3.zero;

            // 1. Reset localPosition của toàn bộ GameObject con về gốc (0, 0, 0)
            // Tránh trường hợp GameObject con có offset y=1 khiến khi scale x140 bị bay lên trời
            Transform[] allChildren = currentWave.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i] != currentWave.transform)
                {
                    allChildren[i].localPosition = Vector3.zero;
                }
            }

            // 2. Tự động bật Loop và Scale Mode Hierarchy cho toàn bộ hạt Particle System
            if (autoLoopParticles)
            {
                ParticleSystem[] particles = currentWave.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    var ps = particles[i];
                    var main = ps.main;
                    main.loop = true; // Cho phép lặp liên tục trong suốt thời gian quét
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy; // Phình to theo Scale của Transform cha
                    ps.Play();
                }
            }

            // 3. Tắt các hiệu ứng đạn phụ va chạm nếu có trong Prefab (ví dụ MultipleObjectsMake / Hit sparks)
            if (hideHitSubEffects)
            {
                MultipleObjectsMake[] subMakers = currentWave.GetComponentsInChildren<MultipleObjectsMake>(true);
                for (int i = 0; i < subMakers.Length; i++)
                {
                    subMakers[i].gameObject.SetActive(false);
                }

                for (int i = 0; i < allChildren.Length; i++)
                {
                    string childName = allChildren[i].name.ToLower();
                    if (childName.Contains("hit") || childName.Contains("multipleshot"))
                    {
                        allChildren[i].gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    private void TurnOffAllHighlights()
    {
        for (int i = 0; i < allTargets.Count; i++)
        {
            if (allTargets[i] != null)
            {
                allTargets[i].SetHighlight(false);
            }
        }
    }
}