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
        // BỎ DÒNG NÀY: if (IsServer) currentSpeed.Value = 0f;
        
        // Nếu bạn muốn nó MẶC ĐỊNH quay ngay khi game chạy, hãy để:
        if (IsServer)
        {
            float direction = reverseDirection ? -1f : 1f;
            currentSpeed.Value = rotationSpeed * direction; 
        }
    }
    public void SetSpeed(float speed)
    {
        if (IsServer) currentSpeed.Value = speed;
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
        if (IsServer) currentSpeed.Value = 0f; 
        OpenGearClientRpc();
    }

    [ClientRpc]
    private void OpenGearClientRpc()
    {
        transform.DOKill();
        // Trượt ra xong thì KHÔNG làm gì cả (vì bánh răng đã dừng)
        transform.DOLocalMove(originalPosition + openOffset, moveDuration).SetEase(Ease.InOutCubic);
    }

    public void CloseGear()
    {
        if (IsServer) currentSpeed.Value = 0f;    
        CloseGearClientRpc();
    }

    [ClientRpc]
    private void CloseGearClientRpc()
    {
        transform.DOKill();
        // Khi trượt về xong, thì mới set lại tốc độ quay (chỉ Server thực hiện)
        transform.DOLocalMove(originalPosition, moveDuration).SetEase(Ease.InOutCubic)
                .OnComplete(() => {
                    if (IsServer) {
                        float direction = reverseDirection ? -1f : 1f;
                        currentSpeed.Value = originalSpeed * direction;
                    }
                });
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