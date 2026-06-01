using UnityEngine;

public class MayaSupport : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 1; // Maya Support
        maxHealth = 100f;
        moveSpeed = 5f;
        runSpeedMultiplier = 2.0f;
        damageAmount = 12f;
        attackRange = 5f;
        cameraOffset = new Vector3(0f, 10f, -6f); // Đưa camera lại gần và thấp hơn một chút
        cameraSensitivity = 3f;  
        cameraPivotHeight = 1.5f; 
    }
}
