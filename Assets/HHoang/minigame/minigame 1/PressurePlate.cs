using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    [Header("Cấu hình Nút Bấm")]
    public float sinkDistance = 0.2f; 
    public float speed = 5f;          

    [Header("Cấu hình Bánh Răng liên kết")]
    public float maxGearSpeed = 100f;  

    [HideInInspector] public float currentGearSpeed;

    private Vector3 initialPosition;
    private Vector3 targetPosition;
    private bool isPressed = false;

    void Start()
    {
        initialPosition = transform.position;
        targetPosition = initialPosition;
        currentGearSpeed = maxGearSpeed; 
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, speed * Time.deltaTime);

        float distanceRatio = Mathf.Clamp01((transform.position.y - (initialPosition.y - sinkDistance)) / sinkDistance);
        currentGearSpeed = distanceRatio * maxGearSpeed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("HeavyObj"))
        {
            isPressed = true;
            targetPosition = initialPosition - new Vector3(0, sinkDistance, 0); 
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("HeavyObj"))
        {
            isPressed = false;
            targetPosition = initialPosition; 
        }
    }
}