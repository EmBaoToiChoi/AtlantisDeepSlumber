using UnityEngine;
using Unity.Netcode;

public class PuzzleCenterStation : NetworkBehaviour
{
    [Header("Cấu hình Bệ Trung Tâm")]
    public Transform snapPosition; // Điểm ngọc sẽ bị hút vào và nằm im
    public GameObject winEffectPrefab; // Hiệu ứng lúc qua màn (Win)

    // Đánh dấu bệ này đã giải quyết xong chưa
    public NetworkVariable<bool> isSolved = new NetworkVariable<bool>(false);

    // Dùng OnTriggerStay để tự động bắt ngọc khi nó rớt vào vùng
    private void OnTriggerStay(Collider other)
    {
        if (!IsServer || isSolved.Value) return;

        // Nếu vật thể chạm vào vùng này chính là viên Ngọc Puzzle
        if (other.TryGetComponent<PuzzleCrystalCore>(out var puzzleCore))
        {
            // Chỉ hút vào khi ngọc ĐANG RƠI TỰ DO (không ai cầm trên tay) và chưa bị khóa
            if (!puzzleCore.isSnapped.Value && puzzleCore.holderId.Value == ulong.MaxValue)
            {
                DisarmBomb(puzzleCore);
            }
        }
    }

    private void DisarmBomb(PuzzleCrystalCore core)
    {
        isSolved.Value = true;
        
        // 1. Ép ngọc hút vào vị trí trung tâm và khóa lại (Tắt bom đếm ngược)
        core.StartSnappingToStation(snapPosition);
        core.LockToStation();

        // 2. Kích hoạt hiệu ứng Chiến Thắng cho cả server thấy
        TriggerWinEffectClientRpc();
        
        Debug.Log("ĐÃ CẮM LÕI THÀNH CÔNG! BOM ĐÃ TẮT! WIN MINI-GAME 3!");
    }

    [ClientRpc]
    private void TriggerWinEffectClientRpc()
    {
        if (winEffectPrefab != null)
        {
            // Đẻ ra hiệu ứng Win (pháo hoa, ánh sáng thánh thiện...)
            Instantiate(winEffectPrefab, snapPosition.position, Quaternion.identity);
        }
    }
}