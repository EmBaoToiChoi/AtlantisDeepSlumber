using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Events;

namespace PLuan.Cutscenes
{
    /// <summary>
    /// Component quản lý Cutscene bằng Unity Timeline.
    /// Tự động tắt/bật UI gameplay, khóa/mở di chuyển nhân vật, chạy hiệu ứng Letterbox (khung đen điện ảnh) và hỗ trợ Bỏ qua (Skip Cutscene).
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public class CinematicCutsceneController : MonoBehaviour
    {
        [Header("--- Timeline Settings ---")]
        [Tooltip("PlayableDirector phát Timeline cutscene này.")]
        [SerializeField] private PlayableDirector director;

        [Tooltip("Tự động phát cutscene khi Start()?")]
        [SerializeField] private bool playOnStart = false;

        [Header("--- Player & Gameplay Control ---")]
        [Tooltip("GameObject Nhân vật chính (cần ẩn/tắt Control khi Cutscene diễn ra).")]
        [SerializeField] private GameObject playerGameObject;

        [Tooltip("Các Component Script điều khiển nhân vật cần Disable khi Cutscene chạy.")]
        [SerializeField] private Behaviour[] scriptsToDisable;

        [Tooltip("Canvas UI Gameplay chính (máu, minimap, nút skill...) cần ẩn đi khi quay Cutscene.")]
        [SerializeField] private GameObject gameplayUI;

        [Header("--- Cinematic Effects ---")]
        [Tooltip("Script quản lý thanh đen Điện ảnh (Letterbox UI).")]
        [SerializeField] private CinematicLetterboxUI letterboxUI;

        [Header("--- Skip Cutscene Options ---")]
        [Tooltip("Cho phép người chơi nhấn phím để bỏ qua Cutscene?")]
        [SerializeField] private bool allowSkip = true;

        [Tooltip("Phím dùng để bỏ qua Cutscene.")]
        [SerializeField] private KeyCode skipKey = KeyCode.Escape;

        [Header("--- Events ---")]
        public UnityEvent onCutsceneStart;
        public UnityEvent onCutsceneEnd;

        private bool isPlaying = false;

        private void Awake()
        {
            if (director == null)
            {
                director = GetComponent<PlayableDirector>();
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                PlayCutscene();
            }
        }

        private void Update()
        {
            if (isPlaying && allowSkip)
            {
                if (Input.GetKeyDown(skipKey))
                {
                    SkipCutscene();
                }
            }
        }

        /// <summary>
        /// Bắt đầu phát Cutscene
        /// </summary>
        public void PlayCutscene()
        {
            if (director == null || director.playableAsset == null)
            {
                Debug.LogWarning("[CinematicCutsceneController] Chưa gán Timeline Asset vào PlayableDirector!");
                return;
            }

            isPlaying = true;

            // 1. Tắt điều khiển Player & UI
            SetGameplayState(false);

            // 2. Bật thanh đen điện ảnh Letterbox
            if (letterboxUI != null)
            {
                letterboxUI.ShowLetterbox();
            }

            // 3. Đăng ký sự kiện hoàn tất Cutscene
            director.stopped += OnTimelineStopped;

            // 4. Phát Timeline
            director.Play();

            onCutsceneStart?.Invoke();
        }

        /// <summary>
        /// Bỏ qua (Skip) Cutscene ngay lập tức
        /// </summary>
        public void SkipCutscene()
        {
            if (!isPlaying || director == null) return;

            // Nhảy tới cuối đoạn Timeline
            director.time = director.duration;
            director.Evaluate();
            director.Stop();
        }

        private void OnTimelineStopped(PlayableDirector pd)
        {
            if (pd != director) return;

            director.stopped -= OnTimelineStopped;
            FinishCutscene();
        }

        private void FinishCutscene()
        {
            isPlaying = false;

            // 1. Trả lại trạng thái Gameplay
            SetGameplayState(true);

            // 2. Ẩn thanh đen Điện ảnh
            if (letterboxUI != null)
            {
                letterboxUI.HideLetterbox();
            }

            onCutsceneEnd?.Invoke();
        }

        private void SetGameplayState(bool active)
        {
            // Tắt/bật UI
            if (gameplayUI != null)
            {
                gameplayUI.SetActive(active);
            }

            // Tắt/bật script điều khiển Player
            if (scriptsToDisable != null)
            {
                foreach (var script in scriptsToDisable)
                {
                    if (script != null)
                    {
                        script.enabled = active;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Tự động kích hoạt khi Player đi vào vùng Trigger
            if (other.CompareTag("Player") && !isPlaying)
            {
                PlayCutscene();
            }
        }
    }
}
