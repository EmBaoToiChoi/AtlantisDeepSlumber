using UnityEngine;

public class ElenaArcher_Old : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 2; // Elena Archer
        maxHealth = 90f;
        moveSpeed = 6f;
        runSpeedMultiplier = 2.0f;
        damageAmount = 18f;
        attackRange = 8f;
        cameraOffset = new Vector3(0f, 10f, -6f); // Đưa camera lại gần và thấp hơn một chút
        cameraSensitivity = 3f;  
        cameraPivotHeight = 1.5f; 
    }
}
