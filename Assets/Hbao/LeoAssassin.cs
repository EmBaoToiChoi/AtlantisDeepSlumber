using UnityEngine;

public class LeoAssassin : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 0; // Leo Assassin
        maxHealth = 85f;
        moveSpeed = 6.5f;
        damageAmount = 25f;
        attackRange = 2f;
    }
}
