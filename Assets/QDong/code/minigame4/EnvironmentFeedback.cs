using UnityEngine;
using Unity.Netcode;

public class EnvironmentFeedback : NetworkBehaviour
{
    public BalanceManager balanceManager;

    public ParticleSystem dust;
    public ParticleSystem rockDust;

    public GameObject crack;

    public AudioSource crackSound;

    public float warningAngle = 7f;

    private NetworkVariable<bool> isWarning = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    bool played = false;
    private bool isCompleted = false;

    /// <summary>Gọi khi minigame hoàn thành để tắt hết hiệu ứng và ngừng Update.</summary>
    public void StopAll()
    {
        isCompleted = true;

        if (IsServer)
            isWarning.Value = false;

        if (dust != null) { dust.Stop(); dust.Clear(); dust.gameObject.SetActive(false); }
        if (rockDust != null) { rockDust.Stop(); rockDust.Clear(); rockDust.gameObject.SetActive(false); }
        if (crack != null) crack.SetActive(false);
        if (crackSound != null && crackSound.isPlaying) crackSound.Stop();
        played = false;

        Debug.Log("[EnvironmentFeedback] StopAll: Đã tắt hết hiệu ứng sau khi hoàn thành minigame.");
    }

    void Update()
    {
        if (isCompleted) return;

        if (IsServer)
        {
            if (balanceManager != null)
            {
                isWarning.Value = balanceManager.CurrentAngle >= warningAngle;
            }
        }

        if (isWarning.Value)
        {
            if (dust != null)
            {
                if (!dust.gameObject.activeSelf) 
                    dust.gameObject.SetActive(true);
                
                if (!dust.isPlaying)
                    dust.Play();
            }

            if (rockDust != null)
            {
                if (!rockDust.gameObject.activeSelf) 
                    rockDust.gameObject.SetActive(true);
                
                if (!rockDust.isPlaying)
                    rockDust.Play();
            }

            if (crack != null && !crack.activeSelf)
                crack.SetActive(true);

            if (!played)
            {
                if (crackSound != null)
                    crackSound.Play();
                played = true;
            }
        }
        else
        {
            if (dust != null)
            {
                if (dust.isPlaying)
                    dust.Stop();
                
                dust.Clear();
                
                if (dust.gameObject.activeSelf) 
                    dust.gameObject.SetActive(false);
            }

            if (crack != null && crack.activeSelf)
                crack.SetActive(false);

            if (crackSound != null && crackSound.isPlaying)
                crackSound.Stop();

            if (rockDust != null)
            {
                if (rockDust.isPlaying)
                    rockDust.Stop();
                
                rockDust.Clear();
                
                if (rockDust.gameObject.activeSelf)
                    rockDust.gameObject.SetActive(false);
            }

            played = false;
        }
    }
}