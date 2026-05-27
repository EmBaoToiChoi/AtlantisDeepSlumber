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
        if (currentSpeed.Value != 0)
        {
            transform.Rotate(rotationAxis.normalized * currentSpeed.Value * Time.deltaTime, Space.Self);
        }
    }

    public bool IsSpining() => currentSpeed.Value != 0f;

    // --- CÁC HÀM GỌI TỪ SERVER ĐỂ ĐỒNG BỘ CLIENT ---

    public void OpenGear()
    {
        OpenGearClientRpc();
    }

    [ClientRpc]
    private void OpenGearClientRpc()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
    }

    public void CloseGear()
    {
        // Server thực hiện việc cập nhật biến tốc độ
        if (IsServer)
        {
            float direction = reverseDirection ? -1f : 1f;
            currentSpeed.Value = originalSpeed * direction;
        }
        
        // Gọi ClientRpc để tất cả Client cùng chạy hiệu ứng đóng
        CloseGearClientRpc();
    }

    [ClientRpc]
    private void CloseGearClientRpc()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutCubic);
        Debug.Log("Client đã nhận lệnh đóng bánh răng và reset vị trí");
    }

    // --- XỬ LÝ VA CHẠM ---
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player") && IsSpining())
        {
            other.transform.position = new Vector3(0f, 1f, 0f);
            Debug.Log("Người chơi bị bánh răng nghiền nát!");
        }
    }
}