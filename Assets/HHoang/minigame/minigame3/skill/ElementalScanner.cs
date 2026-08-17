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
    [Tooltip("Prefab hiệu ứng sóng quét (ví dụ: Effect_09_HoloShield hoặc Effect_09_HoloShield(IncludeHit))")]
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
        // 1. BỘ LỌC PHÍM Z: Hỗ trợ cả Online (IsOwner) lẫn Standalone / Test đơn lẻ
        bool canTrigger = false;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
        {
            canTrigger = IsOwner && hasElementalSight;
        }
        else
        {
            canTrigger = hasElementalSight;
        }

        if (canTrigger)
        {
            if (Input.GetKeyDown(KeyCode.Z) && !isScanning)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
                {
                    TriggerScanServerRpc();
                }
                else
                {
                    // Chế độ chơi đơn / Offline
                    StartScanLocal();
                }
            }
        }

        // 2. BỘ XỬ LÝ HIỆU ỨNG QUÉT
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

    // --- KHỞI CHẠY QUÉT & TẠO HIỆU ỨNG ---

    private void StartScanLocal()
    {
        isScanning = true;
        currentRadius = 0f;
        scanCenter = transform.position + Vector3.up * waveHeightOffset; 

        if (scanWavePrefab != null)
        {
            currentWave = Instantiate(scanWavePrefab, scanCenter, Quaternion.identity);
            currentWave.transform.localScale = Vector3.zero;

            // 1. Reset localPosition của các GameObject con về (0,0,0) để tránh bị offset nhân lên không trung
            Transform[] allChildren = currentWave.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i] != currentWave.transform)
                {
                    allChildren[i].localPosition = Vector3.zero;
                }
            }

            // 2. Tắt TẤT CẢ Collider trên quả cầu sóng để không chặn/đẩy Player hay va chạm vật lý
            Collider[] colliders = currentWave.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            // 3. Tắt các script tự huỷ / mờ dần của Asset pack (NewMaterialChange, ShieldActivate)
            MonoBehaviour[] allScripts = currentWave.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < allScripts.Length; i++)
            {
                string scriptName = allScripts[i].GetType().Name;
                if (scriptName == "NewMaterialChange" || scriptName == "ShieldActivate")
                {
                    allScripts[i].enabled = false;
                }
            }

            // Đảm bảo material luôn hiển thị rõ (MaskCutOut = 1.0f)
            Renderer[] renderers = currentWave.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material != null && renderers[i].material.HasProperty("_MaskCutOut"))
                {
                    renderers[i].material.SetFloat("_MaskCutOut", 1.0f);
                }
            }

            // 4. Tắt riêng hiệu ứng đạn phụ va chạm (MultipleObjectsMake / Effect_15_MultipleShot)
            // CHÚ Ý: Tuyệt đối không tắt nhầm Root hoặc effect khiên chính (HoloShield)
            if (hideHitSubEffects)
            {
                for (int i = 0; i < allChildren.Length; i++)
                {
                    Transform t = allChildren[i];
                    if (t != currentWave.transform)
                    {
                        string childName = t.name.ToLower();
                        if (childName.Contains("multipleshot") || childName.Contains("shieldhit") || childName.Contains("forhit"))
                        {
                            t.gameObject.SetActive(false);
                        }
                    }
                }
            }

            // 5. Tự động bật Loop và Scale Mode Hierarchy cho toàn bộ hạt Particle System
            if (autoLoopParticles)
            {
                ParticleSystem[] particles = currentWave.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particles.Length; i++)
                {
                    var ps = particles[i];
                    var main = ps.main;
                    main.loop = true; 
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy; 
                    ps.Play();
                }
            }

            // Đảm bảo Root luôn Active và hiển thị
            currentWave.SetActive(true);
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
        StartScanLocal();
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