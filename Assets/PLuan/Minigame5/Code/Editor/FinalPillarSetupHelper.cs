using UnityEngine;
using UnityEditor;

public class FinalPillarSetupHelper : EditorWindow
{
    [MenuItem("Tools/Minigame 5/Setup Final Pillar")]
    public static void SetupFinalPillar()
    {
        // 1. Tìm GameObject TruFinal trong scene
        GameObject truFinal = GameObject.Find("TruFinal");
        if (truFinal == null)
        {
            FinalEnergyPillar pillarComponent = FindFirstObjectByType<FinalEnergyPillar>();
            if (pillarComponent != null)
            {
                truFinal = pillarComponent.gameObject;
            }
        }

        if (truFinal == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy GameObject 'TruFinal' trong Scene hiện tại.\nHãy chắc chắn bạn đã kéo Prefab TruFinal vào cảnh!", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(truFinal, "Setup TruFinal");

        // 2. Lấy hoặc thêm component FinalEnergyPillar
        FinalEnergyPillar finalPillar = truFinal.GetComponent<FinalEnergyPillar>();
        if (finalPillar == null)
        {
            finalPillar = truFinal.AddComponent<FinalEnergyPillar>();
        }

        // 3. Tìm các object con
        Transform crystalGemTr = truFinal.transform.Find("CrystalGem");
        Transform clawHolderTr = truFinal.transform.Find("ClawHolder");
        Transform pedestalTr = truFinal.transform.Find("Pedestal");

        // Tìm kiếm dự phòng nếu tên viết khác
        if (crystalGemTr == null)
        {
            for (int i = 0; i < truFinal.transform.childCount; i++)
            {
                Transform child = truFinal.transform.GetChild(i);
                if (child.name.ToLower().Contains("crystal") && child.name.ToLower().Contains("gem"))
                    crystalGemTr = child;
                else if (child.name.ToLower().Contains("claw") || child.name.ToLower().Contains("holder"))
                    clawHolderTr = child;
                else if (child.name.ToLower().Contains("pedestal"))
                    pedestalTr = child;
            }
        }

        if (crystalGemTr == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy object con 'CrystalGem'. Vui lòng kiểm tra lại file 3D TruFinal!", "OK");
            return;
        }

        // 4. Thiết lập Layer Target cho CrystalGem
        int targetLayer = LayerMask.NameToLayer("Target");
        if (targetLayer == -1)
        {
            EditorUtility.DisplayDialog("Cảnh báo", "Không tìm thấy Layer tên là 'Target' trong project.\nVui lòng tạo Layer tên 'Target' trước trong Edit -> Project Settings -> Tags and Layers!", "OK");
            return;
        }
        crystalGemTr.gameObject.layer = targetLayer;

        // 5. Thiết lập MeshRenderer cho script FinalEnergyPillar
        Renderer gemRenderer = crystalGemTr.GetComponent<Renderer>();
        if (gemRenderer != null)
        {
            SerializedObject so = new SerializedObject(finalPillar);
            so.FindProperty("pillarRenderer").objectReferenceValue = gemRenderer;
            so.ApplyModifiedProperties();
        }

        // 6. Thêm Box Collider cho CrystalGem
        BoxCollider gemCollider = crystalGemTr.GetComponent<BoxCollider>();
        if (gemCollider == null)
        {
            gemCollider = crystalGemTr.gameObject.AddComponent<BoxCollider>();
        }

        // Dọn dẹp Box Collider cũ trên ClawHolder nếu lỡ gán nhầm
        if (clawHolderTr != null)
        {
            BoxCollider clawCollider = clawHolderTr.GetComponent<BoxCollider>();
            if (clawCollider != null)
            {
                DestroyImmediate(clawCollider);
            }
        }

            // 7. Cấu hình các thông số tối ưu cho Material VienNgoc
        if (gemRenderer != null && gemRenderer.sharedMaterial != null)
        {
            Material mat = gemRenderer.sharedMaterial;
            Undo.RegisterCompleteObjectUndo(mat, "Setup Material VienNgoc");
            
            // Thiết lập thông số Remap chiều cao phát sáng tối ưu
            if (mat.HasProperty("_DisolveRemapMin")) mat.SetFloat("_DisolveRemapMin", -10.0f); // Tối ưu chiều cao
            if (mat.HasProperty("_DisolveRemapMax")) mat.SetFloat("_DisolveRemapMax", 120.0f); // Tối ưu chiều cao
            if (mat.HasProperty("_DisolveSmooth")) mat.SetFloat("_DisolveSmooth", 0.15f);
            
            // Đồng bộ màu phát sáng ngọc thành màu lửa HDR ấm áp
            if (mat.HasProperty("_GlowColor")) mat.SetColor("_GlowColor", new Color(2.0f, 0.45f, 0.05f, 1.0f));
            
            // Xóa ảnh mask cũ để ngọc phát sáng toàn bộ
            if (mat.HasProperty("_CharacterMask")) mat.SetTexture("_CharacterMask", null);
            
            EditorUtility.SetDirty(mat);
        }

        // Tự động đồng bộ biến màu Active Color của script FinalEnergyPillar sang màu lửa HDR
        SerializedObject soPillar = new SerializedObject(finalPillar);
        SerializedProperty activeColorProp = soPillar.FindProperty("activeColor");
        if (activeColorProp != null)
        {
            activeColorProp.colorValue = new Color(2.0f, 0.45f, 0.05f, 1.0f);
            soPillar.ApplyModifiedProperties();
        }

        // 8. Tự động đồng bộ màu phát sáng của Trụ Đá Ban Đầu (MaterialTruDa)
        Material stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PLuan/Minigame5/Material/MaterialTruDa.mat");
        if (stoneMat != null)
        {
            if (stoneMat.HasProperty("_GlowColor"))
            {
                Undo.RegisterCompleteObjectUndo(stoneMat, "Setup MaterialTruDa Color");
                stoneMat.SetColor("_GlowColor", new Color(2.0f, 0.45f, 0.05f, 1.0f));
                EditorUtility.SetDirty(stoneMat);
            }
        }

        // 9. Tự động nâng cấp Material tia laser thành shader ngọn lửa cuộn siêu đẹp
        Material laserMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PLuan/Minigame5/Material/Laser_Material.mat");
        if (laserMat != null)
        {
            Shader animatedShader = Shader.Find("Custom/AnimatedLaserShader");
            if (animatedShader != null)
            {
                Undo.RegisterCompleteObjectUndo(laserMat, "Update Laser Material Shader");
                laserMat.shader = animatedShader;
                
                // Thiết lập các thông số mặc định của tia lửa cuộn chảy cực kỳ đẹp mắt
                if (laserMat.HasProperty("_GlowColor")) laserMat.SetColor("_GlowColor", new Color(2.0f, 0.45f, 0.05f, 1.0f)); // Đồng bộ màu lửa
                if (laserMat.HasProperty("_ScrollSpeed")) laserMat.SetFloat("_ScrollSpeed", 4.0f);
                if (laserMat.HasProperty("_NoiseScale1")) laserMat.SetFloat("_NoiseScale1", 12.0f);
                if (laserMat.HasProperty("_NoiseScale2")) laserMat.SetFloat("_NoiseScale2", 24.0f);
                if (laserMat.HasProperty("_FlameTurbulence")) laserMat.SetFloat("_FlameTurbulence", 0.08f);
                
                EditorUtility.SetDirty(laserMat);
            }
        }

        EditorUtility.SetDirty(truFinal);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Thành công", "Đã tự động cấu hình xong Trụ Final và Tia Laser!\n\n1. Đổi Layer của 'CrystalGem' thành 'Target'\n2. Tự động thêm Box Collider vừa vặn cho 'CrystalGem'\n3. Gán Renderer ngọc vào script\n4. Cấu hình thông số quét sáng cực đẹp cho Material VienNgoc.\n5. Tự động nâng cấp Material tia laser sang Shader Plasma mới siêu đẹp!", "Tuyệt vời");
    }
}
