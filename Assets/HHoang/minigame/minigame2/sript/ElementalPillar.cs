using UnityEngine;
using Unity.Netcode;

// Định nghĩa các loại nguyên tố
public enum ElementType
{
    None,
    Lua,    // Hỏa
    Bang,   // Băng
    Set,    // Lôi (Sét)
    Nuoc    // Thủy (Nước)
}

[RequireComponent(typeof(AudioSource))] // Tự động thêm AudioSource vào Object nếu bạn quên
public class ElementalPillar : NetworkBehaviour
{
    [Header("Visual & Đổi Màu Trụ")]
    [Tooltip("Kéo cục model con cần đổi màu vào đây")]
    public Renderer pillarRenderer; 

    [Header("Hiệu Ứng Nguyên Tố Cực Riêng Biệt (VFX)")]
    [Tooltip("Kéo hiệu ứng Lửa (Fire VFX) vào đây")]
    public GameObject luaVFX;
    [Tooltip("Kéo hiệu ứng Băng (Ice VFX) vào đây")]
    public GameObject bangVFX;
    [Tooltip("Kéo hiệu ứng Sét (Electro VFX) vào đây")]
    public GameObject setVFX;
    [Tooltip("Kéo hiệu ứng Nước (Hydro VFX) vào đây")]
    public GameObject nuocVFX;

    // [THÊM ÂM THANH] Các biến lưu trữ file âm thanh cho từng hệ
    [Header("--- ÂM THANH NGUYÊN TỐ (SFX) ---")]
    public AudioSource pillarAudioSource;
    public AudioClip luaSFX;
    public AudioClip bangSFX;
    public AudioClip setSFX;
    public AudioClip nuocSFX;

    [Header("State (Đồng bộ mạng)")]
    // Biến mạng lưu trạng thái nguyên tố hiện tại của trụ
    public NetworkVariable<ElementType> currentElement = new NetworkVariable<ElementType>(
        ElementType.None, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    // Bảng màu chuẩn Genshin
    private Color fireColor = Color.red;
    private Color iceColor = Color.cyan; // Xanh ngọc
    private Color electroColor = new Color(0.5f, 0f, 0.5f); // Tím
    private Color hydroColor = Color.blue; // Xanh dương
    private Color defaultColor = Color.gray; // Màu lúc chưa kích hoạt

    void Start() 
    {
        // [THÊM ÂM THANH] Tự động tìm AudioSource nếu bạn chưa kéo vào
        if (pillarAudioSource == null)
        {
            pillarAudioSource = GetComponent<AudioSource>();
        }

        // Mặc định lúc vào game tắt sạch sành sanh mọi hiệu ứng
        DeactivateAllVFX();
        
        // Đăng ký sự kiện mạng
        currentElement.OnValueChanged += OnElementChanged;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Xử lý trường hợp người chơi vào trễ (Late-joiner), trụ vẫn hiển thị đúng VFX
        UpdateVisuals(currentElement.Value);
    }

    public override void OnDestroy()
    {
        currentElement.OnValueChanged -= OnElementChanged;
        base.OnDestroy();
    }

    // Xử lý đạn bắn trúng trên Server
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; 

        // Nếu trụ đã ăn nguyên tố rồi thì giữ nguyên không cho đổi nữa
        if (currentElement.Value != ElementType.None) return;

        // Check chính xác Tag của đạn để kích hoạt hệ
        if (other.CompareTag("Lua")) ActivatePillar(ElementType.Lua);
        else if (other.CompareTag("Bang")) ActivatePillar(ElementType.Bang);
        else if (other.CompareTag("Set")) ActivatePillar(ElementType.Set);
        else if (other.CompareTag("Nuoc")) ActivatePillar(ElementType.Nuoc);
    }

    private void ActivatePillar(ElementType element)
    {
        currentElement.Value = element;
    }

    private void OnElementChanged(ElementType previousValue, ElementType newValue)
    {
        UpdateVisuals(newValue);
        
        // [THÊM ÂM THANH] Chỉ phát âm thanh khi có sự thay đổi thực tế (vừa bị bắn trúng)
        if (previousValue == ElementType.None && newValue != ElementType.None)
        {
            PlayElementalSound(newValue);
        }
    }

    // [THÊM ÂM THANH] Hàm xử lý việc chọn và phát đúng âm thanh
    private void PlayElementalSound(ElementType element)
    {
        if (pillarAudioSource == null) return;

        AudioClip clipToPlay = null;
        switch (element)
        {
            case ElementType.Lua: clipToPlay = luaSFX; break;
            case ElementType.Bang: clipToPlay = bangSFX; break;
            case ElementType.Set: clipToPlay = setSFX; break;
            case ElementType.Nuoc: clipToPlay = nuocSFX; break;
        }

        if (clipToPlay != null)
        {
            // Dùng PlayOneShot để âm thanh phát ra trọn vẹn, vang lên như tiếng nổ
            pillarAudioSource.PlayOneShot(clipToPlay);
        }
    }

    // Cập nhật màu sắc và bật tắt các loại VFX tương ứng
    private void UpdateVisuals(ElementType element)
    {
        // Tự động tìm Renderer ở con nếu chưa kéo thả
        if (pillarRenderer == null) 
        {
            pillarRenderer = GetComponentInChildren<Renderer>();
        }

        if (pillarRenderer != null)
        {
            Color targetColor = defaultColor;
            switch (element)
            {
                case ElementType.Lua: targetColor = fireColor; break;
                case ElementType.Bang: targetColor = iceColor; break;
                case ElementType.Set: targetColor = electroColor; break;
                case ElementType.Nuoc: targetColor = hydroColor; break;
            }

            if (pillarRenderer.material.HasProperty("_BaseColor"))
            {
                pillarRenderer.material.SetColor("_BaseColor", targetColor);
            }
            else
            {
                pillarRenderer.material.color = targetColor; 
            }
        }

        // BƯỚC 1: Dập tắt toàn bộ hiệu ứng trước để tránh bị lỗi đè hạt lên nhau
        DeactivateAllVFX();

        // BƯỚC 2: Hệ nào bật đúng VFX hệ đó lên rực rỡ luôn
        switch (element)
        {
            case ElementType.Lua: 
                if (luaVFX != null) luaVFX.SetActive(true); 
                break;
            case ElementType.Bang: 
                if (bangVFX != null) bangVFX.SetActive(true); 
                break;
            case ElementType.Set: 
                if (setVFX != null) setVFX.SetActive(true); 
                break;
            case ElementType.Nuoc: 
                if (nuocVFX != null) nuocVFX.SetActive(true); 
                break;
        }
    }

    // Hàm phụ trợ tắt nhanh toàn bộ các GameObject hiệu ứng
    private void DeactivateAllVFX()
    {
        if (luaVFX != null) luaVFX.SetActive(false);
        if (bangVFX != null) bangVFX.SetActive(false);
        if (setVFX != null) setVFX.SetActive(false);
        if (nuocVFX != null) nuocVFX.SetActive(false);
    }
}