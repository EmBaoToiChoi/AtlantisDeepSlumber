using UnityEngine;

public class MayaHealOverTimeEffect : MonoBehaviour
{
    private float duration = 5f;
    private float healPerSecond = 10f;
    private float timer = 0f;
    private float tickTimer = 0f;
    private IPlayerHUDTarget player;

    public void Initialize(float duration, float healPerSecond)
    {
        this.duration = duration;
        this.healPerSecond = healPerSecond;
        this.timer = 0f;
        this.tickTimer = 0f;
    }

    private void Start()
    {
        player = GetComponent<IPlayerHUDTarget>();
        if (player == null)
        {
            player = GetComponentInChildren<IPlayerHUDTarget>();
        }
        if (player == null)
        {
            Destroy(this);
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        tickTimer += Time.deltaTime;

        if (tickTimer >= 1f)
        {
            tickTimer -= 1f;
            if (player != null && player.CurrentHealth > 0)
            {
                player.Heal(healPerSecond);
            }
        }

        if (timer >= duration)
        {
            Destroy(this);
        }
    }
}
