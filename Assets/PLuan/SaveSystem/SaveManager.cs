using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public const int TOTAL_CHARACTERS = 4; // 0: Leo, 1: Arthur, 2: Elena, 3: Maya

    [Header("Game Mode State")]
    public static bool IsContinueMode = false;
    public static bool HasPendingSpawnPosition = false;
    public static Vector3 PendingSpawnPosition = Vector3.zero;
    public static float PendingSpawnRotationY = 0f;

    private const string WORLD_SAVE_KEY = "Atlantis_WorldSaveData";
    private const string CHAR_SAVE_PREFIX = "Atlantis_CharSave_";
    private const string CUTSCENE_WATCHED_KEY = "Atlantis_CutsceneWatched";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================================
    // 1. QUẢN LÝ TIẾN TRÌNH THẾ GIỚI (WORLD PROGRESS - HOST)
    // =========================================================================

    [Serializable]
    public class WorldSaveData
    {
        public string sceneName = "MapSTART";
        public float posX;
        public float posY;
        public float posZ;
        public float rotY;
        public string saveTime = "";
        public bool hasValidSave = false;

        public bool isIntroCutsceneWatched = false;
        public List<string> completedQuests = new List<string>();
        public List<string> defeatedEnemyIds = new List<string>();
        public List<string> playedCutscenes = new List<string>();
    }

    public static bool HasWorldSave()
    {
        if (PlayerPrefs.HasKey(WORLD_SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(WORLD_SAVE_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<WorldSaveData>(json);
                return data != null && data.hasValidSave;
            }
        }
        return false;
    }

    public static WorldSaveData LoadWorldSave()
    {
        if (PlayerPrefs.HasKey(WORLD_SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(WORLD_SAVE_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<WorldSaveData>(json);
                if (data != null && data.hasValidSave)
                {
                    if (data.completedQuests == null) data.completedQuests = new List<string>();
                    if (data.defeatedEnemyIds == null) data.defeatedEnemyIds = new List<string>();
                    if (data.playedCutscenes == null) data.playedCutscenes = new List<string>();
                    return data;
                }
            }
        }
        return new WorldSaveData 
        { 
            sceneName = "MapSTART", 
            hasValidSave = false, 
            completedQuests = new List<string>(), 
            defeatedEnemyIds = new List<string>(),
            playedCutscenes = new List<string>()
        };
    }

    public static void SaveWorldSave(string sceneName, Vector3 position, float rotY)
    {
        var existing = LoadWorldSave();

        var data = new WorldSaveData
        {
            sceneName = string.IsNullOrEmpty(sceneName) ? SceneManager.GetActiveScene().name : sceneName,
            posX = position.x,
            posY = position.y,
            posZ = position.z,
            rotY = rotY,
            saveTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            hasValidSave = true,
            isIntroCutsceneWatched = existing.isIntroCutsceneWatched || IsIntroCutsceneWatched(),
            completedQuests = existing.completedQuests ?? new List<string>(),
            defeatedEnemyIds = existing.defeatedEnemyIds ?? new List<string>(),
            playedCutscenes = existing.playedCutscenes ?? new List<string>()
        };

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(WORLD_SAVE_KEY, json);
        PlayerPrefs.Save();
        Debug.Log($"[SaveManager] Saved World Progress: Scene={data.sceneName}, Pos={position}, Quests={data.completedQuests.Count}, Cutscenes={data.playedCutscenes.Count}, Time={data.saveTime}");
    }

    // =========================================================================
    // 2. CUTSCENE & QUEST & ENEMY STATE MANAGEMENT
    // =========================================================================

    // Thứ tự chuỗi nhiệm vụ chính từ đầu đến cuối theo thiết kế:
    // 1 -> 2a -> 2b -> 2c -> 6 -> 10 -> 8 -> 11 -> 4 -> 9 -> 13 -> 12 -> 3 -> 7 -> 14 -> 15
    public static readonly string[] QUEST_SEQUENCE_ORDER = new string[]
    {
        "ZombieQuest",          // 1. Dọn sạch Zombie mở đầu
        "BridgeCollapse",       // 2a. Tiến tới cầu (Sập cầu)
        "BridgeLogs",           // 2b. Chặt & Vác đủ 16 khúc gỗ
        "BridgeRepair",         // 2c. Xây cầu hoàn chỉnh 100%
        "StonePuzzleQuest",     // 6. Câu đố đẩy đá phiến áp lực
        "MazeGemQuest",         // 10. Mê cung thu thập ngọc
        "ElementalRockQuest",   // 8. Phá vỡ đá nguyên tố
        "WaterFreezeQuest",     // 11. Đóng băng mặt nước
        "CrystalPuzzleQuest",   // 4. Đặt 2 viên ngọc lên bệ đá
        "ElementalPillarQuest", // 9. Kích hoạt 4 trụ nguyên tố
        "RotatePillarQuest",    // 13. Xoay góc các trụ đá
        "ChargePillarQuest",    // 12. Sạc điện 3 trụ năng lượng
        "MiniBossQuest",        // 3. Tiêu diệt Mini Boss
        "LaserMirrorQuest",     // 7. Gương phản xạ tia Laser
        "SilasBossQuest",       // 14. Tiêu diệt Boss Silas
        "FinalBossQuest"        // 15. Tiêu diệt Final Boss Wolverine
    };

    public static int GetQuestStageIndex(string questKey)
    {
        if (string.IsNullOrEmpty(questKey)) return -1;
        for (int i = 0; i < QUEST_SEQUENCE_ORDER.Length; i++)
        {
            if (string.Equals(QUEST_SEQUENCE_ORDER[i], questKey, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    public static int GetCurrentMaxQuestStageIndex()
    {
        var world = LoadWorldSave();
        if (world == null || world.completedQuests == null || world.completedQuests.Count == 0) return -1;

        int maxIndex = -1;
        foreach (var q in world.completedQuests)
        {
            int idx = GetQuestStageIndex(q);
            if (idx > maxIndex) maxIndex = idx;
        }
        return maxIndex;
    }

    public static bool IsIntroCutsceneWatched()
    {
        return PlayerPrefs.GetInt(CUTSCENE_WATCHED_KEY, 0) == 1;
    }

    public static void SetIntroCutsceneWatched(bool watched)
    {
        PlayerPrefs.SetInt(CUTSCENE_WATCHED_KEY, watched ? 1 : 0);
        PlayerPrefs.Save();

        if (HasWorldSave())
        {
            var world = LoadWorldSave();
            world.isIntroCutsceneWatched = watched;
            PlayerPrefs.SetString(WORLD_SAVE_KEY, JsonUtility.ToJson(world));
            PlayerPrefs.Save();
        }
    }

    public static bool IsCutscenePlayed(string cutsceneId)
    {
        if (string.IsNullOrEmpty(cutsceneId)) return false;
        var world = LoadWorldSave();
        return world != null && world.playedCutscenes != null && world.playedCutscenes.Contains(cutsceneId);
    }

    public static void MarkCutscenePlayed(string cutsceneId)
    {
        if (string.IsNullOrEmpty(cutsceneId)) return;
        var world = LoadWorldSave();
        if (world.playedCutscenes == null) world.playedCutscenes = new List<string>();

        if (!world.playedCutscenes.Contains(cutsceneId))
        {
            world.playedCutscenes.Add(cutsceneId);
            world.hasValidSave = true;
            PlayerPrefs.SetString(WORLD_SAVE_KEY, JsonUtility.ToJson(world));
            PlayerPrefs.Save();
            Debug.Log($"[SaveManager] Marked Cutscene Played: {cutsceneId} (Total: {world.playedCutscenes.Count})");
        }
    }

    public static void ResetAllCutscenes()
    {
        var world = LoadWorldSave();
        if (world.playedCutscenes != null)
        {
            world.playedCutscenes.Clear();
            PlayerPrefs.SetString(WORLD_SAVE_KEY, JsonUtility.ToJson(world));
            PlayerPrefs.Save();
        }
        Debug.Log("[SaveManager] Reset all played cutscenes for New Game.");
    }

    public static bool IsQuestCompleted(string questKey)
    {
        if (string.IsNullOrEmpty(questKey)) return false;
        var world = LoadWorldSave();
        if (world == null) return false;

        // 1. Kiểm tra trực tiếp trong danh sách completedQuests
        if (world.completedQuests != null && world.completedQuests.Contains(questKey)) return true;

        // 2. Kiểm tra theo chuỗi tuần tự (Nếu đã vượt qua một mốc sau thì tất cả mốc trước đó đều coi là đã hoàn thành)
        int targetIdx = GetQuestStageIndex(questKey);
        if (targetIdx >= 0)
        {
            int currentMaxIdx = GetCurrentMaxQuestStageIndex();
            if (currentMaxIdx >= targetIdx) return true;
        }

        return false;
    }

    public static void MarkQuestCompleted(string questKey)
    {
        if (string.IsNullOrEmpty(questKey)) return;
        var world = LoadWorldSave();
        if (world.completedQuests == null) world.completedQuests = new List<string>();

        int targetIdx = GetQuestStageIndex(questKey);
        if (targetIdx >= 0)
        {
            // Tự động đánh dấu tất cả các bước trước đó trong chuỗi là đã hoàn thành!
            for (int i = 0; i <= targetIdx; i++)
            {
                string key = QUEST_SEQUENCE_ORDER[i];
                if (!world.completedQuests.Contains(key))
                {
                    world.completedQuests.Add(key);
                }
            }
        }
        else
        {
            if (!world.completedQuests.Contains(questKey))
            {
                world.completedQuests.Add(questKey);
            }
        }

        world.hasValidSave = true;
        PlayerPrefs.SetString(WORLD_SAVE_KEY, JsonUtility.ToJson(world));
        PlayerPrefs.Save();
        Debug.Log($"[SaveManager] Marked Quest Completed: {questKey} (Chain Stage: {targetIdx})");
    }

    public static bool IsEnemyDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return false;
        var world = LoadWorldSave();
        return world != null && world.defeatedEnemyIds != null && world.defeatedEnemyIds.Contains(enemyId);
    }

    public static void MarkEnemyDefeated(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return;
        var world = LoadWorldSave();
        if (world.defeatedEnemyIds == null) world.defeatedEnemyIds = new List<string>();
        if (!world.defeatedEnemyIds.Contains(enemyId))
        {
            world.defeatedEnemyIds.Add(enemyId);
            world.hasValidSave = true;
            PlayerPrefs.SetString(WORLD_SAVE_KEY, JsonUtility.ToJson(world));
            PlayerPrefs.Save();
            Debug.Log($"[SaveManager] Marked Enemy Defeated: {enemyId}");
        }
    }

    // =========================================================================
    // 3. QUẢN LÝ CHỈ SỐ THEO TỪNG TƯỚNG (CLASS-BASED CHARACTER STATS - PHƯƠNG ÁN 1)
    // =========================================================================

    public static PlayerStateData GetDefaultCharacterState(int classIndex)
    {
        float baseHp = 100f;
        if (classIndex == 1) baseHp = 120f; // Arthur
        else if (classIndex == 3) baseHp = 90f; // Maya

        return new PlayerStateData
        {
            health = baseHp,
            activeWeaponIndex = 1,
            isWeapon2Locked = false,
            isSkillsUnlocked = true,
            inventorySlots = new string[10],
            upgradePoints = 0,
            hpLevel = 0,
            mpLevel = 0,
            cooldownLevel = 0,
            damageLevel = 0,
            speedLevel = 0,
            playerLevel = 0,
            playerExp = 0f
        };
    }

    public static PlayerStateData LoadCharacterState(int classIndex)
    {
        string key = CHAR_SAVE_PREFIX + classIndex;
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonUtility.FromJson<PlayerStateData>(json);
                if (data != null) return data;
            }
        }
        return GetDefaultCharacterState(classIndex);
    }

    public static void SaveCharacterState(int classIndex, PlayerStateData state)
    {
        if (state == null) return;
        string key = CHAR_SAVE_PREFIX + classIndex;
        string json = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(key, json);

        // Lưu thêm vào PlayerPrefs cũ để đồng bộ
        PlayerPrefs.SetInt("SelectedCharacterId", classIndex);
        PlayerPrefs.SetInt("SelectedPlayerLevel_" + classIndex, state.playerLevel);
        PlayerPrefs.SetFloat("SelectedPlayerExp_" + classIndex, state.playerExp);
        PlayerPrefs.Save();

        Debug.Log($"[SaveManager] Saved Character {classIndex} Stats: Level={state.playerLevel}, Exp={state.playerExp}, HP={state.health}");
    }

    // =========================================================================
    // 4. KHỞI TẠO CHƠI MỚI (RESET TOÀN BỘ CHỈ SỐ)
    // =========================================================================

    public static void ResetAllStatsForNewGame()
    {
        IsContinueMode = false;
        HasPendingSpawnPosition = false;
        PendingSpawnPosition = Vector3.zero;
        PendingSpawnRotationY = 0f;

        // Reset dữ liệu của cả 4 tướng về mặc định (Lv 0, 0 EXP, full HP, túi đồ rỗng)
        for (int i = 0; i < TOTAL_CHARACTERS; i++)
        {
            var defaultState = GetDefaultCharacterState(i);
            SaveCharacterState(i, defaultState);
        }

        // Xóa save thế giới và trạng thái cutscene cũ
        PlayerPrefs.DeleteKey(WORLD_SAVE_KEY);
        PlayerPrefs.DeleteKey(CUTSCENE_WATCHED_KEY);
        PlayerPrefs.Save();

        Debug.Log("[SaveManager] Reset ALL 4 characters stats, cutscene & world save for NEW GAME!");
    }

    // =========================================================================
    // 5. THIẾT LẬP CHƠI TIẾP (CONTINUE GAME)
    // =========================================================================

    public static void PrepareContinueGame()
    {
        IsContinueMode = true;
        var world = LoadWorldSave();
        if (world != null && world.hasValidSave)
        {
            Vector3 savedPos = new Vector3(world.posX, world.posY, world.posZ);
            if (savedPos != Vector3.zero && savedPos.sqrMagnitude > 10f)
            {
                HasPendingSpawnPosition = true;
                PendingSpawnPosition = savedPos;
                PendingSpawnRotationY = world.rotY;
                Debug.Log($"[SaveManager] Prepared Continue Game: Target Scene={world.sceneName}, Pos={PendingSpawnPosition}, CutsceneWatched={world.isIntroCutsceneWatched}");
                return;
            }
        }

        HasPendingSpawnPosition = false;
        PendingSpawnPosition = Vector3.zero;
        PendingSpawnRotationY = 0f;
        Debug.Log("[SaveManager] Prepared Continue Game: Dùng điểm SpawnPoint mặc định của bản đồ.");
    }

    // =========================================================================
    // 6. TỰ ĐỘNG LƯU TOÀN BỘ KHI THOÁT RA MAIN MENU
    // =========================================================================

    public static void AutoSaveCurrentSession(Transform playerTransform, int classIndex, PlayerStateData currentState)
    {
        try
        {
            if (currentState != null)
            {
                SaveCharacterState(classIndex, currentState);
            }

            if (playerTransform != null)
            {
                string activeScene = SceneManager.GetActiveScene().name;
                SaveWorldSave(activeScene, playerTransform.position, playerTransform.eulerAngles.y);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Error during AutoSaveCurrentSession: {ex.Message}");
        }
    }
}
