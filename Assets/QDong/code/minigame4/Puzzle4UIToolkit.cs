using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI Toolkit đẹp cho Puzzle 4 - hiển thị tiến độ nạp năng lượng từng cột A/B/C
/// 
/// SETUP: Gắn script này vào một Canvas/Panel trong scene.
/// Mỗi cột EnergyColumn (A, B, C) sẽ có:
///   - radialFill: Image dạng Filled (fillMethod = Radial360) làm vòng tròn tiến độ
///   - glowImage: Image đặt đằng sau radialFill để tạo hiệu ứng glow
///   - labelText: TMP_Text hiển thị tên cột (A / B / C)
///   - percentText: TMP_Text hiển thị phần trăm (0% → 100%)
///   - completedCheckmark: GameObject icon ✓ hiện ra khi hoàn thành
/// </summary>
public class Puzzle4UIToolkit : MonoBehaviour
{
    [System.Serializable]
    public class EnergyColumnUI
    {
        [Header("Data")]
        public EnergyColumn column;
        
        [Header("UI Elements")]
        [Tooltip("Image dạng Filled Radial360 - vòng tròn tiến độ")]
        public Image radialFill;
        
        [Tooltip("Image đặt sau radialFill tạo hiệu ứng glow pulse")]
        public Image glowImage;
        
        [Tooltip("Text hiển thị tên: A, B, C")]
        public TMP_Text labelText;
        
        [Tooltip("Text hiển thị phần trăm")]
        public TMP_Text percentText;

        [Tooltip("Image nền chung phía sau label & percent text")]
        public Image bgImage;
        
        [Tooltip("Icon checkmark ✓, ẩn khi chưa xong, hiện khi hoàn thành")]
        public GameObject completedCheckmark;
        
        // Gradient màu nội bộ theo tiến độ
        [Header("Colors")]
        public Color colorEmpty   = new Color(0.3f, 0.1f, 0.5f, 0.8f);  // Tím tối
        public Color colorCharging = new Color(0.0f, 0.7f, 1.0f, 1.0f);  // Xanh dương sáng
        public Color colorFull    = new Color(0.2f, 1.0f, 0.6f, 1.0f);  // Xanh lá neon

        // Trạng thái nội bộ
        [HideInInspector] public float displayPct = 0f;
        [HideInInspector] public bool wasCompleted = false;
        [HideInInspector] public float glowPhase = 0f;
    }

    [Header("Column UIs")]
    public EnergyColumnUI columnA;
    public EnergyColumnUI columnB;
    public EnergyColumnUI columnC;

    [Header("Balance Meter")]
    public BalanceManager balanceManager;
    
    [Tooltip("Image dạng Filled Radial360 hoặc Horizontal cho thanh cân bằng")]
    public Image balanceFill;
    
    [Tooltip("Icon indicator mũi tên trên thanh cân bằng (tuỳ chọn)")]
    public RectTransform balanceNeedle;
    
    [Tooltip("Text hiển thị góc nghiêng")]
    public TMP_Text balanceAngleText;
    
    [Tooltip("Text thông báo trạng thái (SAFE / WARNING / DANGER)")]
    public TMP_Text balanceStatusText;

    [Header("Overall Panel FX")]
    [Tooltip("Toàn bộ panel UI (dùng CanvasGroup để fade in/out)")]
    public CanvasGroup panelGroup;
    
    [Tooltip("GameObject nhấp nháy / pulse để báo hiệu đang nguy hiểm")]
    public Image dangerOverlay;

    [Header("Animation Settings")]
    public float lerpSpeed   = 6f;
    public float glowSpeed   = 2.5f;
    public float glowMinAlpha = 0.25f;
    public float glowMaxAlpha = 0.9f;
    public float needleMaxAngle = 45f;  // góc tối đa kim nghiêng (độ)

    [Header("Background Image Alpha")]
    [Tooltip("Độ đục của image nền phía sau label & percent (0 = trong suốt, 1 = đục hoàn toàn)")]
    [Range(0f, 1f)]
    public float bgAlpha = 0.35f;

    [Header("Danger Flash")]
    public Color dangerColor = new Color(1f, 0.1f, 0.1f, 0.0f);
    public float dangerFlashSpeed = 3f;

    // ─────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────

    void Start()
    {
        // Khởi tạo các vòng tròn về 0
        InitColumn(columnA, "A");
        InitColumn(columnB, "B");
        InitColumn(columnC, "C");

        if (dangerOverlay != null)
        {
            var c = dangerOverlay.color;
            c.a = 0f;
            dangerOverlay.color = c;
        }

        // Fade in panel
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            StartCoroutine(FadeInPanel());
        }
    }

    void Update()
    {
        UpdateColumn(columnA);
        UpdateColumn(columnB);
        UpdateColumn(columnC);
        UpdateBalanceMeter();
    }

    // ─────────────────────────────────────────────────────────
    // Init
    // ─────────────────────────────────────────────────────────

    void InitColumn(EnergyColumnUI ui, string defaultLabel)
    {
        if (ui == null) return;
        if (ui.radialFill != null)
        {
            ui.radialFill.fillAmount = 0f;
            ui.radialFill.color = ui.colorEmpty;
        }
        if (ui.glowImage  != null)
        {
            var c = ui.glowImage.color;
            c.a = glowMinAlpha;
            ui.glowImage.color = c;
        }
        if (ui.labelText   != null) ui.labelText.text   = defaultLabel;
        if (ui.percentText != null)
        {
            ui.percentText.text  = "0%";
            ui.percentText.color = ui.colorEmpty;
        }
        // Khởi tạo bgImage với màu rõ ràng ngay từ đầu
        if (ui.bgImage != null)
        {
            Color initBg = ui.colorEmpty;
            initBg.a = bgAlpha;
            ui.bgImage.color = initBg;
            ui.bgImage.enabled = true; // Đảm bảo component không bị disable
        }
        if (ui.completedCheckmark != null) ui.completedCheckmark.SetActive(false);
        ui.glowPhase = Random.Range(0f, Mathf.PI * 2f); // lệch phase để không đồng bộ
    }

    // ─────────────────────────────────────────────────────────
    // Update mỗi cột
    // ─────────────────────────────────────────────────────────

    void UpdateColumn(EnergyColumnUI ui)
    {
        if (ui == null || ui.column == null) return;

        float maxCharge = ui.column.maxCharge > 0 ? ui.column.maxCharge : 100f;
        float targetPct = Mathf.Clamp01(ui.column.charge.Value / maxCharge);

        // ── Lerp tiến độ ──
        ui.displayPct = Mathf.Lerp(ui.displayPct, targetPct, Time.deltaTime * lerpSpeed);

        // ── Radial fill ──
        if (ui.radialFill != null)
            ui.radialFill.fillAmount = ui.displayPct;

        // ── Màu gradient theo tiến độ ──
        Color targetColor = GetColumnColor(ui, ui.displayPct);
        if (ui.radialFill != null)
            ui.radialFill.color = Color.Lerp(ui.radialFill.color, targetColor, Time.deltaTime * lerpSpeed);

        // ── Glow pulse ──
        ui.glowPhase += Time.deltaTime * glowSpeed;
        float glowAlpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, (Mathf.Sin(ui.glowPhase) + 1f) * 0.5f);
        // Glow mạnh hơn khi gần đầy
        glowAlpha = Mathf.Lerp(glowMinAlpha, glowAlpha, ui.displayPct);
        if (ui.glowImage != null)
        {
            Color gc = targetColor;
            gc.a = glowAlpha;
            ui.glowImage.color = Color.Lerp(ui.glowImage.color, gc, Time.deltaTime * lerpSpeed);
        }

        // ── Percent text ──
        int pctInt = Mathf.RoundToInt(ui.displayPct * 100f);
        if (ui.percentText != null)
        {
            ui.percentText.text  = pctInt + "%";
            ui.percentText.color = targetColor;
        }

        // ── Background Image nền chung ──
        Color bgColor = targetColor;
        bgColor.a = bgAlpha;
        if (ui.bgImage != null)
        {
            ui.bgImage.enabled = true;
            ui.bgImage.color = Color.Lerp(ui.bgImage.color, bgColor, Time.deltaTime * lerpSpeed);
        }

        // ── Completed checkmark ──
        bool completed = ui.column.IsCompleted();
        if (completed && !ui.wasCompleted)
        {
            ui.wasCompleted = true;
            if (ui.completedCheckmark != null)
                StartCoroutine(PopIn(ui.completedCheckmark));
        }
    }

    // ─────────────────────────────────────────────────────────
    // Thanh cân bằng
    // ─────────────────────────────────────────────────────────

    void UpdateBalanceMeter()
    {
        if (balanceManager == null) return;

        float angle  = balanceManager.CurrentAngle;
        float pct    = Mathf.Clamp01(angle / 20f); // 20° = nguy hiểm tối đa

        // ── Fill thanh ──
        if (balanceFill != null)
        {
            balanceFill.fillAmount = pct;

            // Màu: xanh lá → vàng → đỏ
            Color bColor;
            if (pct < 0.5f)
                bColor = Color.Lerp(new Color(0.2f, 1f, 0.4f), new Color(1f, 0.85f, 0.1f), pct * 2f);
            else
                bColor = Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(1f, 0.15f, 0.1f), (pct - 0.5f) * 2f);

            balanceFill.color = Color.Lerp(balanceFill.color, bColor, Time.deltaTime * 8f);
        }

        // ── Kim nghiêng (needle) ──
        if (balanceNeedle != null)
        {
            float targetAngle = -pct * needleMaxAngle;
            balanceNeedle.localRotation = Quaternion.Lerp(
                balanceNeedle.localRotation,
                Quaternion.Euler(0, 0, targetAngle),
                Time.deltaTime * 8f
            );
        }

        // ── Text góc ──
        if (balanceAngleText != null)
            balanceAngleText.text = angle.ToString("F1") + "°";

        // ── Status text ──
        if (balanceStatusText != null)
        {
            if (angle < 5f)
            {
                balanceStatusText.text  = "✦ STABLE";
                balanceStatusText.color = new Color(0.3f, 1f, 0.5f);
            }
            else if (angle < 10f)
            {
                balanceStatusText.text  = "⚠ WARNING";
                balanceStatusText.color = new Color(1f, 0.85f, 0.1f);
            }
            else
            {
                balanceStatusText.text  = "✖ DANGER";
                balanceStatusText.color = new Color(1f, 0.15f, 0.1f);
            }
        }

        // ── Danger flash overlay ──
        if (dangerOverlay != null)
        {
            float targetAlpha = angle > 10f ? 0.18f * Mathf.Abs(Mathf.Sin(Time.time * dangerFlashSpeed)) : 0f;
            Color dc = dangerOverlay.color;
            dc.a = Mathf.Lerp(dc.a, targetAlpha, Time.deltaTime * 5f);
            dangerOverlay.color = dc;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────

    Color GetColumnColor(EnergyColumnUI ui, float pct)
    {
        if (pct < 0.5f)
            return Color.Lerp(ui.colorEmpty, ui.colorCharging, pct * 2f);
        else
            return Color.Lerp(ui.colorCharging, ui.colorFull, (pct - 0.5f) * 2f);
    }

    IEnumerator FadeInPanel()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            if (panelGroup != null) panelGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }
    }

    IEnumerator PopIn(GameObject target)
    {
        target.SetActive(true);
        target.transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 8f;
            float s = Mathf.LerpUnclamped(0f, 1f, EaseOutBack(Mathf.Clamp01(t)));
            target.transform.localScale = Vector3.one * s;
            yield return null;
        }
        target.transform.localScale = Vector3.one;
    }

    // Easing function cho hiệu ứng pop đàn hồi
    float EaseOutBack(float t)
    {
        float c = 1.70158f;
        float c3 = c + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
    }
}
