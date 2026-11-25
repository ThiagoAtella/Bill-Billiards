using UnityEngine;
using StarterAssets;

[DefaultExecutionOrder(-100)]
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

    // Propriedades públicas para integração limpa com o AimTrajectory
    public BolaAlvo CurrentAssistedTarget { get; private set; }
    public float CurrentTargetProximityRatio { get; private set; }
    public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

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

        Vector3 shakeOffset = GameFeelController.Instance != null ? GameFeelController.Instance.CurrentShakeOffset : Vector3.zero;
        transform.position = ball.position + Vector3.up * height + shakeOffset;

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
        else
        {
            CurrentAssistedTarget = null;
            CurrentTargetProximityRatio = 0f;
        }

        yaw += lookInput.x * currentSensitivity;
        pitch = Mathf.Clamp(pitch + lookInput.y * sensitivity, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void ApplyAimAssist(Vector2 lookInput, ref float sens)
    {
        Vector3 ballPos = ball.position;
        Vector3 currentForward = PlanarForward;

        BolaAlvo bestTarget = FindBestTargetInCone(ballPos, currentForward, aimAssistAngle, maxAssistDistance, out float smallestAngle, out Vector3 bestDirToTarget);

        CurrentAssistedTarget = bestTarget;

        if (bestTarget != null)
        {
            // 1. Fricção de mira: desacelera a sensibilidade quando passa perto do alvo
            float proximityRatio = 1f - (smallestAngle / Mathf.Max(0.01f, aimAssistAngle));
            CurrentTargetProximityRatio = proximityRatio;
            sens *= Mathf.Lerp(1f, slowdownFactor, proximityRatio);

            // 2. Magnetismo suave: puxa a mira em direção ao centro da bola (usa unscaledDeltaTime para fluidez em slow-motion)
            float targetYaw = Mathf.Atan2(bestDirToTarget.x, bestDirToTarget.z) * Mathf.Rad2Deg;
            float pullStep = aimAssistStrength * proximityRatio * Time.unscaledDeltaTime;

            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, pullStep);
        }
        else
        {
            CurrentTargetProximityRatio = 0f;
        }
    }

    public BolaAlvo FindBestTargetInCone(Vector3 origin, Vector3 forwardDir, float coneAngle, float maxDist, out float bestAngle, out Vector3 bestDirection)
    {
        bestAngle = coneAngle;
        bestDirection = forwardDir;
        BolaAlvo bestTarget = null;

        BolaAlvo[] targetBalls = Object.FindObjectsByType<BolaAlvo>(FindObjectsSortMode.None);
        if (targetBalls == null || targetBalls.Length == 0)
            return null;

        for (int i = 0; i < targetBalls.Length; i++)
        {
            var target = targetBalls[i];
            if (target == null || !target.enabled || !target.gameObject.activeInHierarchy)
                continue;

            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            if (mr != null && !mr.enabled)
                continue;

            Vector3 toTarget = target.transform.position - origin;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist <= 0.05f || dist > maxDist)
                continue;

            toTarget.Normalize();
            float angle = Vector3.Angle(forwardDir, toTarget);

            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestTarget = target;
                bestDirection = toTarget;
            }
        }

        return bestTarget;
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
