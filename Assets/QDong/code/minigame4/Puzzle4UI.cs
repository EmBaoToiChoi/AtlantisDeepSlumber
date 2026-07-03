using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class Puzzle4UI : NetworkBehaviour
{
    public EnergyColumn A;
    public EnergyColumn B;
    public EnergyColumn C;
    public EnergyColumn D;

    public TMP_Text aText;
    public TMP_Text bText;
    public TMP_Text cText;
    public TMP_Text dText;

    [Header("Background Images")]
    public Image aBg;   // kéo image nền cột A vào đây
    public Image bBg;   // kéo image nền cột B vào đây
    public Image cBg;   // kéo image nền cột C vào đây

    // Các biến dùng để làm mượt số hiển thị (Lerp) thay vì nhảy số tức thì
    private float displayValueA = 0f;
    private float displayValueB = 0f;
    private float displayValueC = 0f;
    private float displayValueD = 0f;

    [Header("UI Settings")]
    public float lerpSpeed = 5f; // Tốc độ chạy số

    // Animation Scale (Hiệu ứng đập nhịp khi nạp)
    private Vector3 baseScale = Vector3.one;
    public float popScaleMultiplier = 1.3f;
    public float popSpeed = 10f;

    [Header("Color Gradient")]
    // Đổi màu Đỏ (0%) -> Vàng (50%) -> Xanh lá (100%)
    public Color color0   = new Color(1f, 0.3f, 0.3f);
    public Color color50  = new Color(1f, 0.8f, 0.2f);
    public Color color100 = new Color(0.3f, 1f, 0.4f);

    void Start()
    {
        // Lưu lại kích thước ban đầu của text để làm chuẩn cho hiệu ứng Pop
        if (aText != null) baseScale = aText.transform.localScale;
        else if (bText != null) baseScale = bText.transform.localScale;
        else if (cText != null) baseScale = cText.transform.localScale;
    }

    void Update()
    {
        if (A != null && aText != null) UpdateUIColumn(A, aText, ref displayValueA, "A");
        if (B != null && bText != null) UpdateUIColumn(B, bText, ref displayValueB, "B");
        if (C != null && cText != null) UpdateUIColumn(C, cText, ref displayValueC, "C");
        if (D != null && dText != null) UpdateUIColumn(D, dText, ref displayValueD, "D");
    }

    private void UpdateUIColumn(EnergyColumn column, TMP_Text textMesh, ref float displayValue, string prefix)
    {
        float targetValue = column.charge.Value;
        float maxCharge = column.maxCharge > 0 ? column.maxCharge : 100f;

        // 1. CHẠY SỐ MƯỢT MÀ VÀ HIỆU ỨNG POP SCALE
        if (Mathf.Abs(displayValue - targetValue) > 0.1f)
        {
            displayValue = Mathf.Lerp(displayValue, targetValue, Time.deltaTime * lerpSpeed);
            textMesh.transform.localScale = Vector3.Lerp(textMesh.transform.localScale, baseScale * popScaleMultiplier, Time.deltaTime * popSpeed);
        }
        else
        {
            displayValue = targetValue;
            textMesh.transform.localScale = Vector3.Lerp(textMesh.transform.localScale, baseScale, Time.deltaTime * popSpeed);
        }

        // Cập nhật giá trị lên Text
        int intValue = Mathf.RoundToInt(displayValue);
        textMesh.text = $"{prefix} : {intValue}%";

        // 2. ĐỔI MÀU TEXT THEO % (Đỏ -> Vàng -> Xanh lá)
        float pct = Mathf.Clamp01(displayValue / maxCharge);
        if (pct < 0.5f)
            textMesh.color = Color.Lerp(color0, color50, pct * 2f);
        else
            textMesh.color = Color.Lerp(color50, color100, (pct - 0.5f) * 2f);
    }
}