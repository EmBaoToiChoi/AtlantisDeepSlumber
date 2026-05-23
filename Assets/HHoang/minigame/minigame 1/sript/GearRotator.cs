using UnityEngine;
using Unity.Netcode;
using DG.Tweening; // Nhớ đảm bảo dự án đã có DOTween

public class GearRotator : NetworkBehaviour 
{
    [Header("Cấu hình quay")]
    public float rotationSpeed = 50f;
    public bool reverseDirection = false;

    [Header("Cấu hình trượt mở cổng")]
    public Vector3 openOffset = new Vector3(-5f, 0, 0); // Vị trí trượt tới
    public float moveDuration = 2f; // Thời gian trượt
    private Vector3 originalPosition;

    private NetworkVariable<float> currentSpeed = new NetworkVariable<float>(0f, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        originalPosition = transform.localPosition;
    }

    public override void OnNetworkSpawn()
    {
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
            transform.Rotate(Vector3.up * currentSpeed.Value * Time.deltaTime, Space.Self);
        }
    }

    // --- CÁC HÀM ĐIỀU KHIỂN BÁNH RĂNG ---

    public bool IsSpining()
    {
        return currentSpeed.Value != 0f;
    }

    public void UpdateSpeedFromServer(float newSpeed)
    {
        if (IsServer)
        {
            float direction = reverseDirection ? -1f : 1f;
            currentSpeed.Value = newSpeed == 0f ? 0f : newSpeed * direction;
        }
    }

    // Hàm mở cổng (Gọi từ MiniGameManager)
    public void OpenGear()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
    }

    // Hàm đóng cổng
    public void CloseGear()
    {
        transform.DOKill();
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutCubic);
    }

    // --- XỬ LÝ VA CHẠM ---
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            // Chỉ nghiền nát nếu bánh răng đang quay
            if (IsSpining())
            {
                // Reset vị trí player (Bạn thay vector3 này bằng vị trí hồi sinh/checkpoint)
                other.transform.position = new Vector3(0f, 1f, 0f);
                Debug.Log("Người chơi bị bánh răng nghiền nát!");
            }
        }
    }
}