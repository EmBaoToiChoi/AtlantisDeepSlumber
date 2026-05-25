using UnityEngine;

public class MayaSupport : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 1; // Maya Support
        maxHealth = 100f;
        moveSpeed = 5f;
        damageAmount = 12f;
        attackRange = 5f;
    }
}
