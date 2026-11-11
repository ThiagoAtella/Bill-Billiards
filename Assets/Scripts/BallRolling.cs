
using UnityEngine;

public class BallRolling : MonoBehaviour
{
    public Transform player;
    public float rotationMultiplier = 1f;

    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = player.position;
    }

    void Update()
    {
        Vector3 movement = player.position - lastPosition;

        Vector3 horizontalMovement = new Vector3(
            movement.x,
            0,
            movement.z
        );

        if (horizontalMovement.magnitude > 0.001f)
        {
            Vector3 rotationAxis = Vector3.Cross(
                Vector3.up,
                horizontalMovement.normalized
            );

            float rotationAmount =
                horizontalMovement.magnitude * 360f /
                (2f * Mathf.PI * 0.5f);

            transform.Rotate(
                rotationAxis,
                rotationAmount * rotationMultiplier,
                Space.World
            );
        }

        lastPosition = player.position;
    }
}
