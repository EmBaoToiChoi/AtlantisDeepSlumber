using UnityEngine;

public class ArthurTanker : SimplePlayerTest
{
    private void Awake()
    {
        characterClassIndex = 3; // Arthur Tanker
        maxHealth = 150f;
        moveSpeed = 4f;
        runSpeedMultiplier = 2.0f;
        damageAmount = 15f;
        cameraOffset = new Vector3(0f, 10f, -6f); // Đưa camera lại gần và thấp hơn một chút
        cameraSensitivity = 3f;  
        cameraPivotHeight = 1.5f; 
    }
}
