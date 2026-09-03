using UnityEngine;

public class ElenaArcher : ElenaPlayer
{
    protected override void Awake()
    {
        base.Awake();

        characterClassIndex = 2; // Elena Archer
        maxHealth = 90f;
        moveSpeed = 8f;
        runSpeedMultiplier = 2.0f;
        damageAmount = 18f;
        attackRange = 8f;
        cameraOffset = new Vector3(0f, 10f, -6f); // Đưa camera lại gần và thấp hơn một chút
        cameraSensitivity = 3f;  
        cameraPivotHeight = 1.5f; 
    }
}
