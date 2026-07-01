using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class PushableStoneSetupEditor
{
    static PushableStoneSetupEditor()
    {
        // Chạy thiết lập tự động sau khi Unity Editor load xong hoặc compile xong
        EditorApplication.delayCall += SetupDustOnPrefab;
    }

    [MenuItem("Tools/Setup Pushable Stone Dust")]
    public static void ManualSetup()
    {
        SetupDustOnPrefab();
        EditorUtility.DisplayDialog("Thành công", "Đã thiết lập hiệu ứng bụi mịn chân thực cho đá đẩy thành công!", "OK");
    }

    private static void SetupDustOnPrefab()
    {
        string[] rockPrefabPaths = new string[] {
            "Assets/Hbao/Prefab/Prefab_rock_small_05_sand.prefab",
            "Assets/HHoang/minigame/refab/Prefab_brick_17 (2).prefab"
        };
        string smokePrefabPath = "Assets/msVFX_Free Smoke Effects Pack/Prefabs/msVFX_Stylized Smoke 1.prefab";

        GameObject smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(smokePrefabPath);
        if (smokePrefab == null)
        {
            Debug.LogWarning($"[PushableStoneSetup] Không tìm thấy prefab khói tại: {smokePrefabPath}. Bỏ qua tự động thiết lập.");
            return;
        }

        foreach (string rockPrefabPath in rockPrefabPaths)
        {
            GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rockPrefabPath);
            if (rockPrefab == null) continue;

            // Tải nội dung Prefab dưới dạng Edit Mode
            GameObject root = PrefabUtility.LoadPrefabContents(rockPrefabPath);
            if (root == null) continue;

            bool isModified = false;
            
            // Kiểm tra xem đã có bụi dưới chân chưa
            Transform dustTransform = root.transform.Find("DynamicDustEffect");
            GameObject dustGo = null;

            if (dustTransform == null)
            {
                // Tạo đối tượng bụi từ prefab khói gốc
                dustGo = (GameObject)GameObject.Instantiate(smokePrefab, root.transform);
                dustGo.name = "DynamicDustEffect";
                
                // Căn chỉnh vị trí sát đất và hướng bụi
                dustGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                dustGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                dustGo.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                
                isModified = true;
                Debug.Log($"[PushableStoneSetup] Khởi tạo đối tượng khói con cho prefab đá: {rockPrefab.name}");
            }
            else
            {
                dustGo = dustTransform.gameObject;
            }

            // Điều chỉnh các ParticleSystem con để tạo bụi nhẹ, chân thực
            ParticleSystem[] systems = dustGo.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                
                // Giảm độ đậm của khói/bụi để giống bụi mịn nhẹ (Alpha cực thấp: 0.03 - 0.07)
                var startColor = main.startColor;
                if (startColor.mode == ParticleSystemGradientMode.Color)
                {
                    Color color = startColor.color;
                    if (color.a > 0.06f)
                    {
                        color.a = 0.05f; // Giảm alpha xuống 0.05 để bụi mờ ảo nhẹ nhàng
                        main.startColor = color;
                        isModified = true;
                    }
                }
                else if (startColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    Color cMin = startColor.colorMin;
                    Color cMax = startColor.colorMax;
                    if (cMax.a > 0.08f)
                    {
                        cMin.a = 0.03f;
                        cMax.a = 0.07f;
                        main.startColor = new ParticleSystem.MinMaxGradient(cMin, cMax);
                        isModified = true;
                    }
                }

                // Đảm bảo chế độ mô phỏng là World để khi đá di chuyển, bụi bay lại phía sau đẹp mắt
                if (main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    isModified = true;
                }
                
                // Giảm lượng phát hạt xuống vừa phải (Giảm 60% để đỡ dày đặc)
                var emission = ps.emission;
                var rate = emission.rateOverTime;
                if (rate.mode == ParticleSystemCurveMode.Constant)
                {
                    if (rate.constant > 10f)
                    {
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate.constant * 0.4f);
                        isModified = true;
                    }
                }
                else if (rate.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    if (rate.constantMax > 10f)
                    {
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate.constantMin * 0.4f, rate.constantMax * 0.4f);
                        isModified = true;
                    }
                }
            }

            // Gán reference tự động vào script PushableStone nếu chưa gán
            PushableStone stoneScript = root.GetComponent<PushableStone>();
            if (stoneScript != null && stoneScript.dustParticleEffect == null)
            {
                stoneScript.dustParticleEffect = dustGo.GetComponent<ParticleSystem>();
                if (stoneScript.dustParticleEffect == null)
                {
                    stoneScript.dustParticleEffect = dustGo.GetComponentInChildren<ParticleSystem>();
                }
                isModified = true;
                Debug.Log($"[PushableStoneSetup] Tự động gán dustParticleEffect vào script PushableStone trên {rockPrefab.name}.");
            }

            if (isModified)
            {
                // Lưu lại prefab
                PrefabUtility.SaveAsPrefabAsset(root, rockPrefabPath);
                Debug.Log($"[PushableStoneSetup] Đã cập nhật TỰ ĐỘNG hiệu ứng bụi mịn nhẹ chân thực cho Prefab {rockPrefab.name} thành công!");
            }

            // Giải phóng bộ nhớ
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
