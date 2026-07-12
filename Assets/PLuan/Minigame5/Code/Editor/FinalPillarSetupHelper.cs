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

        EditorUtility.SetDirty(truFinal);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Thành công", "Đã tự động cấu hình xong Trụ Final!\n\n1. Đổi Layer của 'CrystalGem' thành 'Target'\n2. Tự động thêm Box Collider vừa vặn cho 'CrystalGem'\n3. Gán Renderer ngọc vào script\n4. Dọn dẹp collider thừa trên giá đỡ ClawHolder\n5. Cấu hình thông số chiều cao quét sáng cực đẹp cho Material VienNgoc.", "Tuyệt vời");
    }
}
