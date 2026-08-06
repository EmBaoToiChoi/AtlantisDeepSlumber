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
        bool isServerOrOffline = (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) || IsServer;
        Debug.Log($"[PUZZLE4_DEBUG] OnTriggerEnter va chạm bởi '{other.name}' (Tag: {other.tag}). IsServerOrOffline: {isServerOrOffline}, IsServer: {IsServer}, activated: {activated}");

        if (!isServerOrOffline || activated)
            return;

        // Chặn kích hoạt lại nếu minigame đã hoàn thành
        Puzzle4Manager p4Manager = FindAnyObjectByType<Puzzle4Manager>();
        if (p4Manager != null && p4Manager.puzzleCompleted.Value)
        {
            Debug.Log("[PUZZLE4_DEBUG] Minigame đã hoàn thành, không kích hoạt lại.");
            return;
        }

        if (other.CompareTag("Player") || other.GetComponentInParent<IPlayerHUDTarget>() != null)
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            bool isValidPlayer = (netObj != null) || other.CompareTag("Player") || other.GetComponentInParent<IPlayerHUDTarget>() != null;

            if (isValidPlayer)
            {
                ulong clientId = netObj != null ? netObj.OwnerClientId : 0;
                playersTouched.Add(clientId);

                int requiredCount = GetRequiredPlayersCount();
                int totalInRoom = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1;

                Debug.Log($"[PUZZLE4_DEBUG] Số người đã check-in: {playersTouched.Count}/{requiredCount} (Tổng user: {totalInRoom})");

                if (playersTouched.Count >= requiredCount)
                {
                    activated = true;
                    Debug.Log("[PUZZLE4_DEBUG] Đã ĐỦ NGƯỜI CHẠM BOX! Kích hoạt Bẫy và Cutscene...");

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
        Debug.Log($"[PUZZLE4_DEBUG] PlayCutsceneAndTeleportAtEndRoutine BẮT ĐẦU. CutsceneController: {(cutsceneController != null ? cutsceneController.name : "NULL")}");

        // 1. CHẠY CUTSCENE (NẾU CÓ)
        if (cutsceneController != null)
        {
            // Tắt hoàn toàn tự động teleport của VideoCutsceneController
            // để Puzzle4 tự quản lý teleport
            cutsceneController.disableTeleport = true;
            if (cutsceneController.playerSpots != null)
            {
                cutsceneController.playerSpots.Clear();
            }

            Debug.Log("[PUZZLE4_DEBUG] Gọi cutsceneController.StartCutscene()...");
            cutsceneController.StartCutscene();

            // Chờ cutscene bắt đầu (isPlaying = true được set trong StartCutsceneServer)
            float waitStartTimeout = 5f;
            float waited = 0f;
            while (!cutsceneController.isPlaying && waited < waitStartTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[PUZZLE4_DEBUG] Cutscene isPlaying = {cutsceneController.isPlaying} (chờ {waited:F1}s)");

            // QUAN TRỌNG: Trên Dedicated Server (VPS), videoPlayer.isPlaying sẽ KHÔNG BAO GIỜ true
            // vì PlayCutsceneClientRpc() là ClientRpc và không chạy trên Dedicated Server.
            // Nên ta KHÔNG được chờ videoPlayer.isPlaying!
            // Thay vào đó, ta chờ cho isPlaying trở về false (được set bởi WaitAndTeleportAndFinish trên server).

            Debug.Log("[PUZZLE4_DEBUG] Chờ cutscene kết thúc (isPlaying -> false)...");
            while (cutsceneController.isPlaying)
            {
                yield return null;
            }
            Debug.Log("[PUZZLE4_DEBUG] Cutscene đã kết thúc! isPlaying = false. Bắt đầu teleport...");
        }

        // 2. TELEPORT TẤT CẢ NGƯỜI CHƠI LÊN ĐĨA CÂN
        Debug.Log("[PUZZLE4_DEBUG] Gọi ExecuteTeleportToCenter...");
        ExecuteTeleportToCenter(p4Manager);

        // 3. BẮT ĐẦU MINIGAME
        if (p4Manager != null)
        {
            Debug.Log("[PUZZLE4_DEBUG] Phim kết thúc hoàn toàn! Gọi p4Manager.StartMinigameFromTeleport()...");
            p4Manager.StartMinigameFromTeleport();
        }
        else
        {
            Debug.LogError("[PUZZLE4_DEBUG] LỖI: Không tìm thấy Puzzle4Manager trên Server!");
        }
    }

    private void ExecuteTeleportToCenter(Puzzle4Manager p4Manager)
    {
        if (p4Manager == null)
        {
            p4Manager = FindAnyObjectByType<Puzzle4Manager>();
        }

        Debug.Log($"[PUZZLE4_DEBUG] ExecuteTeleportToCenter BẮT ĐẦU. p4Manager: {(p4Manager != null ? p4Manager.name : "NULL")}");

        Vector3 centerDiskPos = Vector3.zero;
        bool foundDiskPos = false;

        // Ưu tiên 1: Dùng teleportTarget từ Inspector (Teleport minigame4)
        if (teleportTarget != null)
        {
            centerDiskPos = teleportTarget.position;
            foundDiskPos = true;
            Debug.Log($"[PUZZLE4_DEBUG] Lấy tọa độ đĩa từ teleportTarget Inspector: {centerDiskPos}");
        }

        // Ưu tiên 2: Tự động lấy vị trí đĩa từ BalanceManager (đĩa nghiêng vòng quay)
        if (!foundDiskPos)
        {
            BalanceManager bm = p4Manager != null ? p4Manager.balanceManager : null;
            if (bm == null) bm = FindAnyObjectByType<BalanceManager>();

            if (bm != null)
            {
                centerDiskPos = bm.diskRigidbody != null ? bm.diskRigidbody.transform.position : bm.transform.position;
                foundDiskPos = true;
                Debug.Log($"[PUZZLE4_DEBUG] Lấy tọa độ đĩa từ BalanceManager: {centerDiskPos}");
            }
        }

        // Ưu tiên 3: Tìm GameObject "đĩa nghiêng vòng quay"
        if (!foundDiskPos)
        {
            GameObject diskObj = GameObject.Find("đĩa nghiêng vòng quay");
            if (diskObj != null)
            {
                centerDiskPos = diskObj.transform.position;
                foundDiskPos = true;
                Debug.Log($"[PUZZLE4_DEBUG] Lấy tọa độ đĩa từ GameObject 'đĩa nghiêng vòng quay': {centerDiskPos}");
            }
        }

        if (!foundDiskPos)
        {
            Debug.LogError("[PUZZLE4_DEBUG] KHÔNG TÌM THẤY đĩa cân! Bỏ qua teleport.");
            return;
        }

        // Nâng độ cao +1.5m lên mặt đĩa nếu chưa có offset
        if (teleportTarget == null)
        {
            centerDiskPos.y += 1.5f;
        }

        Debug.Log($"[PUZZLE4_DEBUG] Thực hiện Teleport tất cả người chơi tới tâm đĩa cân tại {centerDiskPos}");

        bool isNetcodeActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (isNetcodeActive)
        {
            // --- NẾU ĐANG CHẠY NETCODE ONLINE / HOST ---
            List<NetworkObject> netPlayers = new List<NetworkObject>();

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null && !netPlayers.Contains(client.PlayerObject))
                {
                    netPlayers.Add(client.PlayerObject);
                }
            }

            GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject pObj in taggedPlayers)
            {
                NetworkObject netObj = pObj.GetComponentInParent<NetworkObject>();
                if (netObj != null && !netPlayers.Contains(netObj))
                {
                    netPlayers.Add(netObj);
                }
            }

            Debug.Log($"[PUZZLE4_DEBUG] Netcode Active: True. Tìm thấy {netPlayers.Count} người chơi trong Netcode.");

            int pIdx = 0;
            foreach (NetworkObject playerObj in netPlayers)
            {
                Vector3 spawnPos = centerDiskPos;
                float dist = 1.2f;
                if (pIdx == 0) spawnPos += new Vector3(dist, 0f, dist);
                else if (pIdx == 1) spawnPos += new Vector3(-dist, 0f, dist);
                else if (pIdx == 2) spawnPos += new Vector3(dist, 0f, -dist);
                else if (pIdx == 3) spawnPos += new Vector3(-dist, 0f, -dist);
                pIdx++;

                Debug.Log($"[PUZZLE4_DEBUG] Server gửi TeleportPlayerToCenterClientRpc cho NetworkObjectId: {playerObj.NetworkObjectId} (OwnerClientId: {playerObj.OwnerClientId}) tới pos: {spawnPos}");

                if (p4Manager != null)
                {
                    p4Manager.TeleportPlayerToCenterClientRpc(playerObj.NetworkObjectId, spawnPos);
                }
            }
        }
        else
        {
            // --- NẾU TEST OFFLINE TRONG UNITY EDITOR (KHÔNG START HOST/NETCODE) ---
            List<GameObject> offlinePlayers = new List<GameObject>();

            GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject pObj in taggedPlayers)
            {
                if (!offlinePlayers.Contains(pObj)) offlinePlayers.Add(pObj);
            }

            var allMonos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allMonos)
            {
                if (mono is IPlayerHUDTarget)
                {
                    if (!offlinePlayers.Contains(mono.gameObject)) offlinePlayers.Add(mono.gameObject);
                }
            }

            int pIdx = 0;
            foreach (GameObject pObj in offlinePlayers)
            {
                Vector3 spawnPos = centerDiskPos;
                float dist = 1.2f;
                if (pIdx == 0) spawnPos += new Vector3(dist, 0f, dist);
                else if (pIdx == 1) spawnPos += new Vector3(-dist, 0f, dist);
                else if (pIdx == 2) spawnPos += new Vector3(dist, 0f, -dist);
                else if (pIdx == 3) spawnPos += new Vector3(-dist, 0f, -dist);
                pIdx++;

                StartCoroutine(DirectOfflineTeleportRoutine(pObj, spawnPos));
            }
        }
    }

    private IEnumerator DirectOfflineTeleportRoutine(GameObject playerObj, Vector3 pos)
    {
        if (playerObj == null) yield break;

        CharacterController cc = playerObj.GetComponentInParent<CharacterController>();
        if (cc == null) cc = playerObj.GetComponent<CharacterController>();

        UnityEngine.AI.NavMeshAgent nav = playerObj.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
        if (nav == null) nav = playerObj.GetComponent<UnityEngine.AI.NavMeshAgent>();

        Rigidbody rb = playerObj.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = playerObj.GetComponent<Rigidbody>();

        bool wasKinematic = false;
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cc != null) cc.enabled = false;
        if (nav != null) nav.enabled = false;

        yield return new WaitForEndOfFrame();

        playerObj.transform.position = pos;
        if (rb != null) rb.position = pos;

        Physics.SyncTransforms();

        yield return new WaitForEndOfFrame();

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        if (cc != null) cc.enabled = true;
        if (nav != null) nav.enabled = true;

        Debug.Log($"[Puzzle4Teleport] Đã Teleport OFFLINE thành công nhân vật {playerObj.name} tới {pos}");
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