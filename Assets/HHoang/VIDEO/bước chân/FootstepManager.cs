using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FootstepManager : MonoBehaviour
{
    [Header("Bỏ mấy file âm thanh bước chân vào đây")]
    public AudioClip[] footstepSounds; 
    
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        // Tùy chỉnh audio source không bị 3D quá gắt nếu là game 2D/3D đơn giản
        // audioSource.spatialBlend = 1f; // Bật dòng này nếu m làm game 3D
    }

    // Hàm này sẽ được gọi trực tiếp từ Animator thông qua Animation Event
    public void PlayFootstep()
    {
        if (footstepSounds.Length > 0)
        {
            // Lấy ngẫu nhiên 1 âm thanh trong mảng để phát
            int randomIndex = Random.Range(0, footstepSounds.Length);
            
            // Dùng PlayOneShot để các tiếng bước chân có thể đè lên nhau nếu nhân vật chạy nhanh
            audioSource.PlayOneShot(footstepSounds[randomIndex]);
        }
    }
}