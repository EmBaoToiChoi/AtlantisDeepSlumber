using UnityEngine;
using Unity.Netcode;

public class EnvironmentFeedback : NetworkBehaviour
{
    public BalanceManager balanceManager;

    public ParticleSystem dust;

    public GameObject crack;

    public AudioSource crackSound;

    public float warningAngle = 7f;

    private NetworkVariable<bool> isWarning = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    bool played = false;

    void Update()
    {
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
                
                // Tuỳ chọn: Tắt luôn GameObject của dust nếu cần
                // if (dust.gameObject.activeSelf) 
                //     dust.gameObject.SetActive(false);
            }

            if (crack != null && crack.activeSelf)
                crack.SetActive(false);

            played = false;
        }
    }
}