using UnityEngine;

public class MayaSupport : MayaPlayer
{
    protected override void Awake()
    {
        base.Awake();

        characterClassIndex = 1; // Maya Support
        maxHealth = 100f;
        moveSpeed = 7f;
        runSpeedMultiplier = 2.0f;
        damageAmount = 12f;
        attackRange = 5f;
        cameraOffset = new Vector3(0f, 10f, -6f); // Đưa camera lại gần và thấp hơn một chút
        cameraSensitivity = 3f;  
        cameraPivotHeight = 1.5f; 
    }
}
