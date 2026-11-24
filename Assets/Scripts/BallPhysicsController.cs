using UnityEngine;
using StarterAssets;

[RequireComponent(typeof(Rigidbody))]
public class BallPhysicsController : MonoBehaviour
{
    [Header("Movimentação")]
    public float acceleration = 50f;
    public float maxSpeed = 15f;

    [Header("Dash / Tacada")]
    public float dashImpulse = 150f;
    public float dashCooldown = 1f;

    private Rigidbody rb;
    private StarterAssetsInputs input;
    private Transform cameraTransform;

    private float nextDashTime;
    private bool dashRequested;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<StarterAssetsInputs>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        // Detecta o espaço pelo Starter Assets
        if (input != null && input.jump)
        {
            dashRequested = true;
            input.jump = false;
        }
    }

    void FixedUpdate()
    {
        if (cameraTransform == null || input == null)
            return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * input.move.y +
            right * input.move.x;

        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);

        // Movimentação normal: aplica aceleração na direção desejada respeitando o limite direcional
        if (direction.sqrMagnitude > 0.01f)
        {
            Vector3 targetDir = direction.normalized;
            float currentSpeedInDir = Vector3.Dot(horizontalVelocity, targetDir);

            // Permite acelerar nas direções combinadas (ex: W + A/D) até maxSpeed
            if (currentSpeedInDir < maxSpeed)
            {
                float neededAccel = Mathf.Min(acceleration, (maxSpeed - currentSpeedInDir) / Time.fixedDeltaTime);
                rb.AddForce(targetDir * neededAccel, ForceMode.Acceleration);
            }
        }

        // Tacada / Dash (aplica impulso total sem clamp artificial)
        if (dashRequested)
        {
            dashRequested = false;

            if (Time.time >= nextDashTime)
            {
                Vector3 dashDirection =
                    direction.sqrMagnitude > 0.01f
                    ? direction.normalized
                    : forward;

                rb.AddForce(
                    dashDirection * dashImpulse,
                    ForceMode.Impulse
                );

                nextDashTime = Time.time + dashCooldown;
            }
        }
    }
}
