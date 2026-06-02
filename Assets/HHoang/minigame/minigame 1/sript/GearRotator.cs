using UnityEngine;
using Unity.Netcode;
using DG.Tweening;

public class GearRotator : NetworkBehaviour 
{
    [Header("Cấu hình quay")]
    public float rotationSpeed = 50f;
    public bool reverseDirection = false;
    private float originalSpeed;

    [Header("Cấu hình trượt mở cổng")]
    public Vector3 openOffset = new Vector3(-5f, 0, 0);
    public float moveDuration = 2f;
    public Vector3 rotationAxis = Vector3.up;
    private Vector3 originalPosition;

    private NetworkVariable<float> currentSpeed = new NetworkVariable<float>(0f, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        originalPosition = transform.localPosition;
        originalSpeed = rotationSpeed;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            float direction = reverseDirection ? -1f : 1f;
            currentSpeed.Value = rotationSpeed * direction; 
        }
    }

    void Update()
    {
        // 1. Quay bánh răng (Chạy trên cả Server và Client để đồng bộ)
        if (currentSpeed.Value != 0)
        {
            transform.Rotate(rotationAxis.normalized * currentSpeed.Value * Time.deltaTime, Space.Self);
        }
    }

    // 2. SỬA: Hàm Open/Close nên gọi ServerRpc để Server quyết định trạng thái
    public void OpenGear() { if (IsServer) OpenGearServerRpc(); }
    public void CloseGear() { if (IsServer) CloseGearServerRpc(); }

    [ServerRpc(RequireOwnership = false)]
    private void OpenGearServerRpc()
    {
        currentSpeed.Value = 0f;
        // Thực hiện di chuyển vật lý trên Server
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
        // Đồng bộ tới Client
        OpenGearClientRpc();
    }

    [ClientRpc]
    private void OpenGearClientRpc()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CloseGearServerRpc()
    {
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutCubic)
                .OnComplete(() => {
                    float direction = reverseDirection ? -1f : 1f;
                    currentSpeed.Value = originalSpeed * direction;
                });
        CloseGearClientRpc();
    }

    [ClientRpc]
    private void CloseGearClientRpc()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutCubic);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player") && currentSpeed.Value != 0f)
        {
            // Reset player vị trí an toàn (Server quyết định)
            other.transform.position = new Vector3(0f, 1f, 0f);
            Debug.Log("Server đã xử lý va chạm bánh răng!");
        }
    }
}