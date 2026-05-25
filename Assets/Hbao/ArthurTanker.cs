using UnityEngine;

public class ArthurTanker : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 3; // Arthur Tanker
        maxHealth = 150f;
        moveSpeed = 4f;
        damageAmount = 15f;
    }
}
