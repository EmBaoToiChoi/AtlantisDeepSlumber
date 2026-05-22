using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening; // Khai báo thư viện DOTween chuyên nghiệp

public class QTEController : MonoBehaviour
{
    [Header("Cấu hình Bong Bóng Prefab")]
    public GameObject notePrefabA;   
    public GameObject notePrefabD;   

    [Header("Cấu hình Vị Trí Canvas")]
    public Transform targetZone;     
    public Transform spawnLeftA;      
    public Transform spawnLeftD;      
    public GameObject qteCanvas;     

    [Header("Thông số Nhịp (Endless Mode)")]
    public float noteSpeed = 300f;       
    public float spawnInterval = 0.8f;   
    public float perfectRange = 25f;     
    public float goodRange = 50f;        
    
    [Tooltip("Khoảng cách pixel tối đa xung quanh Zone để nốt bị hóa xám khi bấm sớm. Ngoài khoảng này sẽ ko bị xám.")]
    public float maxGrayRange = 150f;    

    // Danh sách quản lý các nốt đang hoạt động thường
    private List<GameObject> activeNotes = new List<GameObject>();
    private List<KeyCode> noteKeys = new List<KeyCode>();
    private List<Vector3> noteTargetPositions = new List<Vector3>(); 

    // Danh sách để đánh dấu những nốt đã bị bấm hụt/hóa xám nhưng vẫn cho bay tiếp
    private HashSet<GameObject> failedNotes = new HashSet<GameObject>();
    
    // Danh sách khóa chết: Chứa những nốt đã trôi qua vạch đích, đang chạy hiệu ứng mờ dần, CẤM KO ĐƯỢC CHẠY CHECKHIT
    private HashSet<GameObject> deadNotes = new HashSet<GameObject>();

    private float spawnTimer;
    private int scoreCounter = 0; 
    private bool isGameActive = false;
    
    private MonoBehaviour currentLever; 
    private GearRotation targetGear;

    // --- BIẾN QUAN TRỌNG CHO LOGIC CO-OP 2 NGƯỜI ---
    private bool isCurrentlyHoldingSuccess = false; 
    private const int SCORE_THRESHOLD_TO_HOLD = 3; // Cần đạt từ 3 điểm trở lên mới tính là gõ nhịp tốt

    void Start()
    {
        targetGear = FindObjectOfType<GearRotation>();
        if (qteCanvas != null) qteCanvas.SetActive(false);
    }

    void Update()
    {
        if (!isGameActive) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval && activeNotes.Count < 6)
        {
            SpawnNote();
            spawnTimer = 0f;
        }

        MoveActiveNotes();
        HandlePlayerInput();

        // LIÊN TỤC CẬP NHẬT TRẠNG THÁI ĐÓNG GÓP CHO BÁNH RĂNG DỰA TRÊN ĐIỂM SỐ QTE
        UpdateGearContribution();
    }

    void UpdateGearContribution()
    {
        if (targetGear == null) return;

        // Nếu điểm số tốt (>=3) và chưa được tính là đang đóng góp giữ bánh răng
        if (scoreCounter >= SCORE_THRESHOLD_TO_HOLD && !isCurrentlyHoldingSuccess)
        {
            isCurrentlyHoldingSuccess = true;
            // Báo cho bánh răng biết máy này gạt cần thành công (Tăng số lượng người)
            targetGear.ThayDoiSoNguoiGiu(true); 
        }
        // Nếu gõ trượt quá nhiều làm điểm tụt xuống dưới ngưỡng khi đang giữ
        else if (scoreCounter < SCORE_THRESHOLD_TO_HOLD && isCurrentlyHoldingSuccess)
        {
            isCurrentlyHoldingSuccess = false;
            // Truyền tham số false để trừ đi 1 người giữ thành công
            targetGear.ThayDoiSoNguoiGiu(false); 
        }
    }

    public void StartQTE(MonoBehaviour lever)
    {
        currentLever = lever;
        isGameActive = true;
        scoreCounter = 0;
        spawnTimer = 0f;
        isCurrentlyHoldingSuccess = false; 

        ClearAllNotes();
        if (qteCanvas != null) qteCanvas.SetActive(true);
        
        PlayerMovement localPlayer = FindObjectOfType<PlayerMovement>();
        if (localPlayer != null && localPlayer.IsOwner)
        {
            localPlayer.SetCanMoveServerRpc(false); 
        }

        Debug.Log("--- KHỞI ĐỘNG QTE CO-OP: FIX CỨNG LỖI LỆCH TRỤC Y VÀ WORLDPOINT ---");
    }

    void SpawnNote()
    {
        if (notePrefabA == null || notePrefabD == null || qteCanvas == null || targetZone == null) return;

        KeyCode assignedKey = Random.value > 0.5f ? KeyCode.A : KeyCode.D;
        GameObject prefabToSpawn = (assignedKey == KeyCode.A) ? notePrefabA : notePrefabD;
        Transform startPoint = (assignedKey == KeyCode.A) ? spawnLeftA : spawnLeftD;
        
        if (startPoint == null) return;

        GameObject newNote = Instantiate(prefabToSpawn, qteCanvas.transform);
        newNote.transform.position = startPoint.position;
        newNote.transform.localScale = Vector3.one; 

        Vector3 specificTargetPos = new Vector3(targetZone.position.x, startPoint.position.y, targetZone.position.z);

        activeNotes.Add(newNote);
        noteKeys.Add(assignedKey);
        noteTargetPositions.Add(specificTargetPos); 
    }

    void MoveActiveNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i] == null) continue;
            if (deadNotes.Contains(activeNotes[i])) continue;

            Vector3 destination = noteTargetPositions[i];
            
            if (activeNotes[i].transform.position.x < destination.x)
            {
                activeNotes[i].transform.position = Vector3.MoveTowards(
                    activeNotes[i].transform.position, 
                    destination, 
                    noteSpeed * Time.deltaTime * 10f
                );
            }
            else
            {
                GameObject missedNote = activeNotes[i];

                if (!failedNotes.Contains(missedNote))
                {
                    ApplyPenalty("LỠ NHỊP (MISS)");
                }

                deadNotes.Add(missedNote);
                RemoveActiveNoteListsAt(i);
                TriggerPassZoneMissEffect(missedNote); 
            }
        }
    }

    // ==================== CÁC HIỆU ỨNG DOTWEEN ====================

    void TriggerHitEffect(GameObject noteObj)
    {
        if (noteObj == null) return;

        noteObj.transform.DOScale(1.3f, 0.1f).OnComplete(() =>
        {
            noteObj.transform.DOScale(0f, 0.12f).OnComplete(() =>
            {
                Destroy(noteObj);
            });
        });
    }

    void TriggerEarlyHitGrayEffect(GameObject noteObj)
    {
        if (noteObj == null) return;

        Image img = noteObj.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0.4f, 0.4f, 0.4f, img.color.a);
            noteObj.transform.DOShakePosition(0.15f, new Vector3(8f, 0f, 0f), 10, 90, false, true);
        }
    }

    void TriggerPassZoneMissEffect(GameObject noteObj)
    {
        if (noteObj == null) return;

        Image img = noteObj.GetComponent<Image>();
        if (img != null)
        {
            noteObj.transform.DOKill();
            img.DOKill();

            img.color = new Color(0.25f, 0.25f, 0.25f, img.color.a);
            noteObj.transform.DOMoveX(noteObj.transform.position.x + 100f, 0.35f).SetEase(Ease.Linear);

            img.DOFade(0f, 0.35f).OnComplete(() =>
            {
                if (noteObj != null)
                {
                    if (deadNotes.Contains(noteObj)) deadNotes.Remove(noteObj);
                    Destroy(noteObj);
                }
            });
        }
        else
        {
            Destroy(noteObj);
        }
    }

    // ======================================================================

    void HandlePlayerInput()
    {
        if (Input.GetKeyDown(KeyCode.A)) CheckHit(KeyCode.A);
        else if (Input.GetKeyDown(KeyCode.D)) CheckHit(KeyCode.D);
    }

    void CheckHit(KeyCode pressedKey)
    {
        int targetIndex = -1;
        float maxX = float.MinValue;

        // Tìm nốt đầu tiên hợp lệ của phím đó (Chưa bị hụt, chưa chết qua vạch)
        for (int i = 0; i < activeNotes.Count; i++)
        {
            if (activeNotes[i] == null) continue;
            if (noteKeys[i] != pressedKey) continue;
            if (failedNotes.Contains(activeNotes[i])) continue; 
            if (deadNotes.Contains(activeNotes[i])) continue; 

            if (activeNotes[i].transform.position.x > maxX)
            {
                maxX = activeNotes[i].transform.position.x;
                targetIndex = i;
            }
        }

        if (targetIndex != -1)
        {
            GameObject targetNote = activeNotes[targetIndex];
            
            // --- CÁCH SỬA MỚI CỰC CHUẨN: DÙNG LOCAL POSITION CỦA RECTTRANSFORM ---
            // Tuyệt đối không dùng WorldToScreenPoint nữa vì nó có độ sai lệch
            RectTransform noteRect = targetNote.GetComponent<RectTransform>();
            RectTransform targetRect = targetZone.GetComponent<RectTransform>();

            if (noteRect != null && targetRect != null)
            {
                // Fix chiều X chuẩn đét: so sánh tọa độ x local của nốt nhạc với vạch Zone
                // (Mặc định vạch Zone có anchor nằm ngay tâm nên x local là 0, nếu mày đặt anchor khác thì so x local của nó nhé)
                // Giả sử TargetZone RectTransform nằm giữa Canvas QTE, nốt nhạc bay tịnh tiến theo X
                
                // Lấy hiệu tọa độ X Local để so sánh (Phớt lờ độ cao Y)
                float pixelDist = Mathf.Abs(noteRect.localPosition.x - targetRect.localPosition.x);

                if (pixelDist <= perfectRange)
                {
                    scoreCounter += 2;
                    Debug.Log($"<color=cyan>PERFECT!</color> Tổng điểm: {scoreCounter}");
                    RemoveActiveNoteListsAt(targetIndex); 
                    TriggerHitEffect(targetNote);         
                }
                else if (pixelDist <= goodRange)
                {
                    scoreCounter += 1;
                    Debug.Log($"<color=green>GOOD!</color> Tổng điểm: {scoreCounter}");
                    RemoveActiveNoteListsAt(targetIndex); 
                    TriggerHitEffect(targetNote);         
                }
                // CHỈ HÓA XÁM KHI NẰM TRONG VÙNG SUNG QUANNH ZONE
                else if (pixelDist <= maxGrayRange)
                {
                    ApplyPenalty("BẤM SỚM VÙNG GẦN (MISS)");
                    
                    failedNotes.Add(targetNote);
                    TriggerEarlyHitGrayEffect(targetNote); // Hóa xám vì đã mò vào vùng nguy hiểm
                }
                else
                {
                    // NỐT Ở QUÁ XA VỚI VẠCH ĐÍCH (pixelDist > maxGrayRange)
                    ApplyPenalty("BẤM SỚM TỪ XA (MISS)");
                    
                    // TUYỆT ĐỐI KHÔNG add vào failedNotes, KHÔNG gọi hiệu ứng đổi màu!
                    // Nốt này vẫn giữ nguyên màu gốc và chạy bình thường tiếp tục đi vào vạch đích.
                }
            }
            else
            {
                // Phòng hờ nếu model UI không có RectTransform, quay về world point nhưng chỉ lấy X
                Vector2 noteScreenPos = RectTransformUtility.WorldToScreenPoint(null, targetNote.transform.position);
                Vector2 targetScreenPos = RectTransformUtility.WorldToScreenPoint(null, noteTargetPositions[targetIndex]);
                float pixelDist = Mathf.Abs(noteScreenPos.x - targetScreenPos.x);

                if (pixelDist <= perfectRange)
                {
                    scoreCounter += 2; RemoveActiveNoteListsAt(targetIndex); TriggerHitEffect(targetNote);
                }
                else if (pixelDist <= goodRange)
                {
                    scoreCounter += 1; RemoveActiveNoteListsAt(targetIndex); TriggerHitEffect(targetNote);
                }
                else { ApplyPenalty("MISS (SAI UI SETUP)"); }
            }
        }
        else
        {
            ApplyPenalty("BẤM LOẠN (MISS)");
        }
    }

    void ApplyPenalty(string reason)
    {
        scoreCounter = Mathf.Max(0, scoreCounter - 1);
        Debug.Log($"<color=red>{reason}</color> Tổng điểm: {scoreCounter}");
    }

    void RemoveActiveNoteListsAt(int index)
    {
        if (index < 0 || index >= activeNotes.Count) return;
        if (failedNotes.Contains(activeNotes[index])) failedNotes.Remove(activeNotes[index]);

        activeNotes.RemoveAt(index);
        noteKeys.RemoveAt(index);
        noteTargetPositions.RemoveAt(index);
    }

    public void ForceStopQTE()
    {
        isGameActive = false;
        if (qteCanvas != null) qteCanvas.SetActive(false);
        
        // HỦY CHƠI GIỮA CHỪNG: Nếu đang gõ nhịp tốt mà buông tay ra, lập tức trừ đi 1 người đóng góp
        if (isCurrentlyHoldingSuccess && targetGear != null)
        {
            targetGear.ThayDoiSoNguoiGiu(false); // Trả lại tốc độ cũ cho bánh răng
        }
        isCurrentlyHoldingSuccess = false;

        PlayerMovement localPlayer = FindObjectOfType<PlayerMovement>();
        if (localPlayer != null && localPlayer.IsOwner)
        {
            localPlayer.SetCanMoveServerRpc(true); 
        }

        ClearAllNotes();
    }

    void ClearAllNotes()
    {
        foreach (var note in activeNotes) { if (note != null) Destroy(note); }
        activeNotes.Clear();
        noteKeys.Clear();
        noteTargetPositions.Clear();
        failedNotes.Clear();
        deadNotes.Clear();
    }
}