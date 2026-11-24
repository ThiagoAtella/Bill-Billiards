using UnityEngine;
using StarterAssets;

public class BallCameraTarget : MonoBehaviour
{
    [Header("Alvo")]
    public Transform ball;
    public float height = 1.2f;

    [Header("Camera")]
    public float sensitivity = 1f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Header("Aim Assist (Magnetismo Suave)")]
    [Tooltip("Ativa o sistema de assistência magnética de mira")]
    public bool enableAimAssist = true;

    [Tooltip("Ângulo do cone de detecção das bolas em graus")]
    [Range(5f, 30f)]
    public float aimAssistAngle = 14f;

    [Tooltip("Força de atração magnética em graus por segundo")]
    [Range(0f, 60f)]
    public float aimAssistStrength = 22f;

    [Tooltip("Distância máxima para ativar o magnetismo")]
    public float maxAssistDistance = 30f;

    [Tooltip("Fator de fricção/desaceleração da mira sobre o alvo (0.1 = muito lento, 1 = normal)")]
    [Range(0.2f, 1f)]
    public float slowdownFactor = 0.55f;

    private StarterAssetsInputs input;
    private float yaw;
    private float pitch;

    private void Awake()
    {
        yaw = transform.eulerAngles.y;
        pitch = NormalizeAngle(transform.eulerAngles.x);
        CacheInput();
    }

    private void LateUpdate()
    {
        if (ball == null)
            return;

        transform.position = ball.position + Vector3.up * height;

        if (input == null)
            CacheInput();

        if (input == null)
            return;

        Vector2 lookInput = input.look;
        float currentSensitivity = sensitivity;

        // Magnetismo suave e desaceleração de mira
        if (enableAimAssist)
        {
            ApplyAimAssist(lookInput, ref currentSensitivity);
        }

        yaw += lookInput.x * currentSensitivity;
        pitch = Mathf.Clamp(pitch + lookInput.y * sensitivity, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void ApplyAimAssist(Vector2 lookInput, ref float sens)
    {
        Vector3 ballPos = ball.position;
        Vector3 currentForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        BolaAlvo[] targetBalls = Object.FindObjectsByType<BolaAlvo>(FindObjectsSortMode.None);
        if (targetBalls == null || targetBalls.Length == 0)
            return;

        BolaAlvo bestTarget = null;
        float smallestAngle = aimAssistAngle;
        Vector3 bestDirToTarget = Vector3.zero;

        for (int i = 0; i < targetBalls.Length; i++)
        {
            var target = targetBalls[i];
            if (target == null || !target.enabled)
                continue;

            Vector3 toTarget = target.transform.position - ballPos;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist <= 0.05f || dist > maxAssistDistance)
                continue;

            toTarget.Normalize();
            float angle = Vector3.Angle(currentForward, toTarget);

            if (angle < smallestAngle)
            {
                smallestAngle = angle;
                bestTarget = target;
                bestDirToTarget = toTarget;
            }
        }

        if (bestTarget != null)
        {
            // 1. Fricção de mira: desacelera a sensibilidade quando passa perto do alvo
            float proximityRatio = 1f - (smallestAngle / aimAssistAngle);
            sens *= Mathf.Lerp(1f, slowdownFactor, proximityRatio);

            // 2. Magnetismo suave: puxa a mira em direção ao centro da bola
            float targetYaw = Mathf.Atan2(bestDirToTarget.x, bestDirToTarget.z) * Mathf.Rad2Deg;
            float pullStep = aimAssistStrength * proximityRatio * Time.deltaTime;

            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, pullStep);
        }
    }

    private void CacheInput()
    {
        if (ball != null)
            input = ball.GetComponent<StarterAssetsInputs>();
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
