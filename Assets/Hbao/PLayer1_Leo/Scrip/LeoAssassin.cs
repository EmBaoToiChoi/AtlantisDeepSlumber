using UnityEngine;

public class LeoAssassin : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 0; // Leo Assassin
        maxHealth = 85f;
        moveSpeed = 3.5f;
        runSpeedMultiplier = 3.5f;
        damageAmount = 25f;
        attackRange = 2f;
        cameraOffset = new Vector3(0f, 10f, -6.5f); // Đưa camera lại gần và thấp hơn một chút
        cameraSensitivity = 3f;  
        cameraPivotHeight = 3.5f; 
    }
}
