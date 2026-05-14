using UnityEngine;

public class UnderwaterCamera : MonoBehaviour
{
    public float moveAmount = 0.05f;
    public float speed = 1f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float x = Mathf.Sin(Time.time * speed) * moveAmount;
        float y = Mathf.Cos(Time.time * speed) * moveAmount;

        transform.position = startPos + new Vector3(x, y, 0);
    }
}