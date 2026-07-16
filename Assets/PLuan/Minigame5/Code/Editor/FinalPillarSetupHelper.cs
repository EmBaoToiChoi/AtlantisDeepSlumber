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
            FinalEnergyPillar pillarComponent = FindObjectOfType<FinalEnergyPillar>();
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
            if (mat.HasProperty("_DisolveRemapMin")) mat.SetFloat("_DisolveRemapMin", -1.0f);
            if (mat.HasProperty("_DisolveRemapMax")) mat.SetFloat("_DisolveRemapMax", 15.0f);
            if (mat.HasProperty("_DisolveSmooth")) mat.SetFloat("_DisolveSmooth", 0.15f);
            
            // Xóa ảnh mask cũ để ngọc phát sáng toàn bộ
            if (mat.HasProperty("_CharacterMask")) mat.SetTexture("_CharacterMask", null);
            
            EditorUtility.SetDirty(mat);
        }

        // 8. Tự động nâng cấp Material tia laser thành shader mới siêu đẹp
        Material laserMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PLuan/Minigame5/Material/Laser_Material.mat");
        if (laserMat != null)
        {
            Shader animatedShader = Shader.Find("Custom/AnimatedLaserShader");
            if (animatedShader != null)
            {
                Undo.RegisterCompleteObjectUndo(laserMat, "Update Laser Material Shader");
                laserMat.shader = animatedShader;
                
                // Thiết lập các thông số mặc định siêu đẹp cho tia laser plasma
                if (laserMat.HasProperty("_GlowColor")) laserMat.SetColor("_GlowColor", new Color(1.5f, 0.3f, 0.08f, 1f)); // Màu cam đỏ phát sáng rực rỡ (HDR)
                if (laserMat.HasProperty("_ScrollSpeed")) laserMat.SetFloat("_ScrollSpeed", 6.0f);
                if (laserMat.HasProperty("_WaveFreq")) laserMat.SetFloat("_WaveFreq", 18.0f);
                if (laserMat.HasProperty("_WaveAmp")) laserMat.SetFloat("_WaveAmp", 0.035f);
                if (laserMat.HasProperty("_CoreWidth")) laserMat.SetFloat("_CoreWidth", 0.07f);
                
                EditorUtility.SetDirty(laserMat);
            }
        }

        EditorUtility.SetDirty(truFinal);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Thành công", "Đã tự động cấu hình xong Trụ Final và Tia Laser!\n\n1. Đổi Layer của 'CrystalGem' thành 'Target'\n2. Tự động thêm Box Collider vừa vặn cho 'CrystalGem'\n3. Gán Renderer ngọc vào script\n4. Cấu hình thông số quét sáng cực đẹp cho Material VienNgoc.\n5. Tự động nâng cấp Material tia laser sang Shader Plasma mới siêu đẹp!", "Tuyệt vời");
    }
}
