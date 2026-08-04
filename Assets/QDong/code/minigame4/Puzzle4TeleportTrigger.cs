using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class Puzzle4TeleportTrigger : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Kéo Puzzle4TrapTrigger vào đây để tắt đường sau khi teleport")]
    public Puzzle4TrapTrigger trapTrigger;

    [Tooltip("Kéo VideoCutsceneController vào đây để chạy phim cutscene trước")]
    public VideoCutsceneController cutsceneController;

    [Header("Teleport Settings")]
    [Tooltip("Điểm trung tâm đĩa để dịch chuyển tất cả người chơi tới")]
    public Transform teleportTarget;
    
    [Header("Cutscene Sync Settings")]
    [Tooltip("Khoảng thời gian trước khi phim hết để bắt đầu Teleport (giây)")]
    public float teleportBeforeFinishTime = 1.0f;

    private HashSet<ulong> playersTouched = new HashSet<ulong>();
    public bool activated = false;

    private int GetRequiredPlayersCount()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            int totalPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count;
            // Công thức điểm danh: 4 người cần 3, 3 cần 2, 2 cần 1, 1 cần 1
            return Mathf.Max(1, totalPlayers - 1);
        }
        return 1; // Chế độ chơi đơn
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || activated)
            return;

        // Chặn kích hoạt lại nếu minigame đã hoàn thành
        Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
        if (p4Manager != null && p4Manager.puzzleCompleted.Value)
        {
            Debug.Log("[Puzzle4Teleport] Minigame đã hoàn thành, không kích hoạt lại.");
            return;
        }

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsPlayerObject)
            {
                playersTouched.Add(netObj.OwnerClientId);
                playersTouched.RemoveWhere(id => NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(id));

                int requiredCount = GetRequiredPlayersCount();
                int totalInRoom = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1;

                Debug.Log($"[Puzzle4Teleport] Số người đã check-in: {playersTouched.Count}/{requiredCount} (Tổng user: {totalInRoom})");

                if (playersTouched.Count >= requiredCount)
                {
                    activated = true;
                    Debug.Log("[Puzzle4Teleport] Đã ĐỦ NGƯỜI CHẠM BOX! Kích hoạt Bẫy và Cutscene...");

                    if (trapTrigger != null)
                    {
                        trapTrigger.ActivateTrapFromTeleport();
                    }

                    StartCoroutine(PlayCutsceneAndTeleportAtEndRoutine(p4Manager));
                }
            }
        }
    }

    private IEnumerator PlayCutsceneAndTeleportAtEndRoutine(Puzzle4Manager p4Manager)
    {
        bool hasTeleported = false;

        // 1. CHẠY CUTSCENE
        if (cutsceneController != null)
        {
            cutsceneController.StartCutscene();

            // Đợi cho đến khi VideoPlayer chuẩn bị xong và bắt đầu Play
            yield return new WaitUntil(() => cutsceneController.isPlaying && cutsceneController.videoPlayer != null && cutsceneController.videoPlayer.isPlaying);

            var vp = cutsceneController.videoPlayer;

            // Vòng lặp chờ canh thời gian: khi thời gian còn lại <= 1s (hoặc bị skip)
            while (cutsceneController.isPlaying)
            {
                double timeRemaining = vp.length - vp.time;

                // Nếu phim còn tầm 1 giây (hoặc người chơi nhấn ESC làm vp ngừng chạy) thì Teleport ngầm ngay
                if (!hasTeleported && (timeRemaining <= teleportBeforeFinishTime || !vp.isPlaying))
                {
                    hasTeleported = true;
                    Debug.Log($"[Puzzle4Teleport] Phim còn {timeRemaining:F1}s -> Thực hiện Teleport ngầm tới đĩa!");
                    ExecuteTeleportToCenter(p4Manager);
                }

                yield return null;
            }
        }

        // Fallback: Nếu không có CutsceneController hoặc phim lỗi, đảm bảo luôn teleport
        if (!hasTeleported)
        {
            ExecuteTeleportToCenter(p4Manager);
        }

        // 2. BẮT ĐẦU MINIGAME SAU KHI PHIM KẾT THÚC HOÀN TOÀN
        if (p4Manager != null)
        {
            Debug.Log("[Puzzle4Teleport] Phim kết thúc hoàn toàn! Bắt đầu minigame!");
            p4Manager.StartMinigameFromTeleport();
        }
        else
        {
            Debug.LogError("[Puzzle4Teleport] LỖI: Không tìm thấy Puzzle4Manager trên Server!");
        }
    }

    private void ExecuteTeleportToCenter(Puzzle4Manager p4Manager)
    {
        if (p4Manager == null) return;

        int playerIndex = 0;
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        HashSet<NetworkObject> teleportedPlayers = new HashSet<NetworkObject>();

        foreach (GameObject pObj in allPlayers)
        {
            NetworkObject playerObj = pObj.GetComponentInParent<NetworkObject>();

            if (playerObj != null && playerObj.IsSpawned && !teleportedPlayers.Contains(playerObj))
            {
                teleportedPlayers.Add(playerObj);

                Vector3 spawnPos = teleportTarget != null ? teleportTarget.position : playerObj.transform.position;

                // Offset vị trí đứng thẳng chân trên đĩa (trục Y = 0)
                Vector3 offset = Vector3.zero;
                float dist = 1.0f;
                if (playerIndex == 0) offset = new Vector3(dist, 0f, dist);
                else if (playerIndex == 1) offset = new Vector3(-dist, 0f, dist);
                else if (playerIndex == 2) offset = new Vector3(dist, 0f, -dist);
                else if (playerIndex == 3) offset = new Vector3(-dist, 0f, -dist);

                spawnPos += offset;
                playerIndex++;

                CharacterController cc = playerObj.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                p4Manager.TeleportPlayerToCenterClientRpc(playerObj.NetworkObjectId, spawnPos);

                Unity.Netcode.Components.NetworkTransform netTransform = playerObj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (netTransform != null)
                {
                    try
                    {
                        netTransform.Teleport(spawnPos, playerObj.transform.rotation, playerObj.transform.localScale);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[Puzzle4Teleport] Không thể gọi Teleport trực tiếp cho Client {playerObj.OwnerClientId}: {e.Message}");
                    }
                }
                else
                {
                    playerObj.transform.position = spawnPos;
                }

                Rigidbody rb = playerObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }

                if (cc != null) cc.enabled = true;
            }
        }
    }

    [ClientRpc]
    public void ReappearClientRpc()
    {
        gameObject.SetActive(true);

        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Debug.Log("[Puzzle4Teleport] ReappearClientRpc: Box đã hiện lại (chỉ Renderer, Collider vẫn tắt)!");
    }
}