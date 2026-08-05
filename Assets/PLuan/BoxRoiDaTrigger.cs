using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class BoxRoiDaTrigger : NetworkBehaviour
{
    [Header("Cutscene Settings")]
    [Tooltip("Kéo VideoCutsceneController vào đây để phát phim khi rơi đá")]
    public VideoCutsceneController cutsceneController;

    [Header("GameObjects to Toggle")]
    [Tooltip("Object Đá Chặn Cửa 1")]
    public GameObject DaChanCua1;
    [Tooltip("Object Đá Chặn Cửa 2")]
    public GameObject DaChanCua2;
    [Tooltip("Object Đá Chặn Cửa Chưa Rơi (sẽ bị tắt đi)")]
    public GameObject daChanCuaChuaRoi;

    private bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

    private bool hasFallen = false;
    private HashSet<ulong> playersTouched = new HashSet<ulong>();

    private int GetRequiredPlayersCount(int totalPlayers)
    {
        // Công thức điểm danh: 4 người cần 3, 3 cần 2, 2 cần 1, 1 cần 1
        return Mathf.Max(1, totalPlayers - 1);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasFallen) return;

        if (IsPlayer(other.gameObject))
        {
            if (IsNetworkActive)
            {
                // Chỉ Server mới xử lý đếm người chơi check-in
                if (IsServer)
                {
                    NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
                    ulong clientId = netObj != null ? netObj.OwnerClientId : 0;

                    // Lưu ID người chơi (chạm 1 lần là lưu vĩnh viễn)
                    playersTouched.Add(clientId);
                    playersTouched.RemoveWhere(id => NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

                    int totalInRoom = NetworkManager.Singleton.ConnectedClientsList.Count;
                    int requiredCount = GetRequiredPlayersCount(totalInRoom);

                    Debug.Log($"[BoxRoiDaTrigger] Số người đã check-in: {playersTouched.Count}/{requiredCount} (Tổng user: {totalInRoom})");

                    // ĐỦ NGƯỜI CHECK-IN -> TẮT COLLIDER VÀ CHẠY CUTSCENE + ĐỔI TRẠNG THÁI ĐÁ
                    if (playersTouched.Count >= requiredCount)
                    {
                        hasFallen = true;

                        // Tắt Collider trước để tránh ai chạm thêm trong lúc đang chờ
                        Collider col = GetComponent<Collider>();
                        if (col != null) col.enabled = false;

                        StartCoroutine(PlayCutsceneAndExecuteFallRoutine());
                    }
                }
            }
            else
            {
                // Standalone / Offline
                hasFallen = true;

                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;

                StartCoroutine(PlayCutsceneAndExecuteFallRoutine());
            }
        }
    }

    private IEnumerator PlayCutsceneAndExecuteFallRoutine()
    {
        // 1. Phát Cutscene
        if (cutsceneController != null)
        {
            cutsceneController.StartCutscene();
        }

        // 2. Bật Đá 1, Đá 2 & Tắt Đá chưa rơi ngầm trong lúc chiếu phim/mờ đen
        if (IsNetworkActive)
        {
            if (IsServer)
            {
                ExecuteRockFall();
                TriggerRockFallClientRpc();
            }
        }
        else
        {
            ExecuteRockFall();
        }

        // 3. Đợi Cutscene kết thúc hoàn toàn (hoặc nếu bấm ESC Skip)
        if (cutsceneController != null)
        {
            yield return new WaitUntil(() => !cutsceneController.isPlaying);
        }

        // 4. Đã chiếu phim xong -> Mới tắt hẳn Trigger Box này
        gameObject.SetActive(false);
    }

    [ClientRpc]
    private void TriggerRockFallClientRpc()
    {
        ExecuteRockFall();
    }

    private void ExecuteRockFall()
    {
        Debug.Log($"[BoxRoiDaTrigger] Kích hoạt chuyển đổi trạng thái đá rơi.");

        // Bật Đá 1
        if (DaChanCua1 != null)
        {
            DaChanCua1.SetActive(true);
            ActivateAllChildrenRecursive(DaChanCua1.transform);
            
            var puzzles1 = DaChanCua1.GetComponentsInChildren<ElementalRockPuzzle>(true);
            if (puzzles1 != null && puzzles1.Length > 0)
            {
                foreach (var puzzle in puzzles1)
                {
                    puzzle.ShowRock();
                }
            }
        }

        // Bật Đá 2
        if (DaChanCua2 != null)
        {
            DaChanCua2.SetActive(true);
            ActivateAllChildrenRecursive(DaChanCua2.transform);

            var puzzles2 = DaChanCua2.GetComponentsInChildren<ElementalRockPuzzle>(true);
            if (puzzles2 != null && puzzles2.Length > 0)
            {
                foreach (var puzzle in puzzles2)
                {
                    puzzle.ShowRock();
                }
            }
        }

        // Tắt Đá chưa rơi
        if (daChanCuaChuaRoi != null)
        {
            daChanCuaChuaRoi.SetActive(false);
        }
    }

    private void ActivateAllChildrenRecursive(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            child.gameObject.SetActive(true);
            ActivateAllChildrenRecursive(child);
        }
    }

    private bool IsPlayer(GameObject go)
    {
        if (go == null) return false;

        if (go.GetComponent<LeoPlayer>() != null || go.GetComponentInParent<LeoPlayer>() != null) return true;
        if (go.GetComponent<ArthurPlayer>() != null || go.GetComponentInParent<ArthurPlayer>() != null) return true;
        if (go.GetComponent<ElenaPlayer>() != null || go.GetComponentInParent<ElenaPlayer>() != null) return true;
        if (go.GetComponent<MayaPlayer>() != null || go.GetComponentInParent<MayaPlayer>() != null) return true;

        if (go.CompareTag("Player") || (go.transform.parent != null && go.transform.parent.CompareTag("Player"))) return true;

        return false;
    }
}