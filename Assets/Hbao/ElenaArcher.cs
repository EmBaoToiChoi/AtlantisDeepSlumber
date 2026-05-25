using UnityEngine;

public class ElenaArcher : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 2; // Elena Archer
        maxHealth = 90f;
        moveSpeed = 6f;
        damageAmount = 18f;
        attackRange = 8f;
    }
}
