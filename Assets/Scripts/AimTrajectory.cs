using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(SphereCollider))]
public class AimTrajectory : MonoBehaviour
{
    [Header("Referências de Mira e Magnetismo")]
    [Tooltip("Referência ao BallCameraTarget (que contém o MagneticAim / Aim Assist da câmera)")]
    [SerializeField] public BallCameraTarget magneticAimCamera;

    [Tooltip("LineRenderer 1: Trajeto principal da bola branca e reflexão na tabela")]
    [SerializeField] public LineRenderer mainTrajectoryLine;

    [Tooltip("LineRenderer 2: Previsão de trajetória da bola alvo atingida")]
    [SerializeField] public LineRenderer targetPredictionLine;

    [Header("Parâmetros do Trajeto Fantasma (SphereCast)")]
    [Tooltip("Distância máxima da linha de mira")]
    [SerializeField] public float maxAimDistance = 95f;

    [Tooltip("Camadas de colisão para bolas e tabelas da mesa")]
    [SerializeField] public LayerMask collisionMask = ~(1 << 2);

    [Tooltip("Comprimento da linha de previsão que sai da bola alvo atingida")]
    [SerializeField] public float targetLineLength = 24f;

    [Tooltip("Comprimento do segmento refletido ao atingir a tabela/parede da mesa")]
    [SerializeField] public float wallReflectionLength = 22f;

    [Header("Integração com MagneticAim (Aim Assist)")]
    [Tooltip("Usa e potencializa o magnetismo do BallCameraTarget ao mirar com botão direito")]
    [SerializeField] public bool useMagneticAimAssist = true;

    [Tooltip("Ângulo do cone de assistência magnética ao mirar (sincronizado com o MagneticAim)")]
    [Range(5f, 30f)]
    [SerializeField] public float aimAssistAngle = 14f;

    [Tooltip("Força da assistência magnética na linha de mira (0 = sem desvio extra, 1 = trava suave no ponto ideal)")]
    [Range(0f, 1f)]
    [SerializeField] public float magneticSnapStrength = 0.65f;

    [Tooltip("Distância máxima para o magnetismo da mira atuar")]
    [SerializeField] public float maxAssistDistance = 45f;

    [Header("Cores das Linhas")]
    [Tooltip("Cor da linha principal da bola branca (Dourado/Branco semitransparente)")]
    [SerializeField] public Color mainLineColor = new Color(1f, 0.702f, 0f, 0.88f); // #FFB300

    [Tooltip("Cor da linha de previsão da bola alvo atingida")]
    [SerializeField] public Color targetLineColor = new Color(1f, 1f, 1f, 0.92f);

    [Header("Slow-Motion ao Mirar (Game Feel)")]
    [Tooltip("Reduz levemente o Time.timeScale enquanto segura o botão direito para mirar")]
    [SerializeField] public bool slowMotionWhileAiming = true;

    [Tooltip("Valor do Time.timeScale durante a mira (ex: 0.6f)")]
    [Range(0.2f, 1f)]
    [SerializeField] public float aimTimeScale = 0.6f;

    private SphereCollider sphereCollider;
    private bool wasAimingLastFrame;

    public bool IsAiming { get; private set; }
    public Vector3 AssistedAimDirection { get; private set; } = Vector3.forward;
    public BolaAlvo CurrentLockedTarget { get; private set; }

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();

        if (magneticAimCamera == null)
            magneticAimCamera = Object.FindAnyObjectByType<BallCameraTarget>();

        EnsureLineRenderers();
        SetLinesVisible(false);
    }

    public void EnsureLineRenderers()
    {
        if (mainTrajectoryLine == null)
        {
            Transform existingMain = transform.Find("Line_MainTrajectory");
            if (existingMain != null)
                mainTrajectoryLine = existingMain.GetComponent<LineRenderer>();
        }

        if (targetPredictionLine == null)
        {
            Transform existingTarget = transform.Find("Line_TargetPrediction");
            if (existingTarget != null)
                targetPredictionLine = existingTarget.GetComponent<LineRenderer>();
        }
    }

    private void LateUpdate()
    {
        // Sempre atualiza a direção assistida pelo AimTrajectory (mesmo com as linhas ocultas),
        // para que o Dash saia sempre na direção do AimTrajectory e nunca no WASD
        AssistedAimDirection = GetAssistedAimDirection();

        bool rightMouseHeld = ReadAimInput();
        IsAiming = rightMouseHeld;

        UpdateSlowMotion(IsAiming);

        if (!IsAiming)
        {
            if (wasAimingLastFrame)
            {
                SetLinesVisible(false);
            }
            wasAimingLastFrame = false;
            return;
        }

        wasAimingLastFrame = true;
        UpdateTrajectory();
    }

    public Vector3 GetAssistedAimDirection()
    {
        Vector3 origin = transform.position;
        Vector3 baseDir = ComputeBaseCameraDirection();
        AssistedAimDirection = ComputeMagneticAimDirection(origin, baseDir);
        return AssistedAimDirection;
    }

    private bool ReadAimInput()
    {
        bool held = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            held = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(1))
        {
            held = true;
        }
#endif
        return held;
    }

    private void UpdateSlowMotion(bool aiming)
    {
        float targetScale = (aiming && slowMotionWhileAiming) ? aimTimeScale : 1f;

        if (GameFeelController.Instance != null)
        {
            GameFeelController.Instance.DesiredBaseTimeScale = targetScale;

            // Se o Hitstop estiver rodando, não sobrescreve o congelamento; o Hitstop restaurará para DesiredBaseTimeScale ao terminar
            if (GameFeelController.Instance.IsHitstopActive)
                return;
        }

        if (aiming && slowMotionWhileAiming)
        {
            Time.timeScale = aimTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }
        else if (wasAimingLastFrame)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    public float GetRealSphereRadius()
    {
        if (sphereCollider == null)
            sphereCollider = GetComponent<SphereCollider>();

        Vector3 s = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        return sphereCollider != null ? sphereCollider.radius * maxScale : 1.2f;
    }

    private Vector3 ComputeBaseCameraDirection()
    {
        if (magneticAimCamera != null)
        {
            return magneticAimCamera.PlanarForward;
        }

        if (Camera.main != null)
        {
            Vector3 fwd = Camera.main.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                return fwd.normalized;
        }

        return Vector3.forward;
    }

    private Vector3 ComputeMagneticAimDirection(Vector3 origin, Vector3 baseDir)
    {
        CurrentLockedTarget = null;

        if (!useMagneticAimAssist)
            return baseDir;

        float cone = magneticAimCamera != null ? Mathf.Max(aimAssistAngle, magneticAimCamera.aimAssistAngle) : aimAssistAngle;
        float maxDist = magneticAimCamera != null ? Mathf.Max(maxAssistDistance, magneticAimCamera.maxAssistDistance) : maxAssistDistance;

        BolaAlvo target = null;
        Vector3 dirToTarget = baseDir;
        float angleToTarget = cone;

        if (magneticAimCamera != null)
        {
            target = magneticAimCamera.FindBestTargetInCone(origin, baseDir, cone, maxDist, out angleToTarget, out dirToTarget);
        }

        if (target == null)
            return baseDir;

        CurrentLockedTarget = target;

        // Calcula o ponto de impacto ideal (ponto onde o centro da bola branca deve bater na BolaAlvo)
        // Se a câmera já está dentro do cone magnético, interpola suavemente a direção para o ponto de impacto
        float proximity = 1f - Mathf.Clamp01(angleToTarget / Mathf.Max(0.01f, cone));
        float effectiveSnap = magneticSnapStrength * Mathf.SmoothStep(0f, 1f, proximity);

        Vector3 assistedDir = Vector3.Slerp(baseDir, dirToTarget, effectiveSnap);
        assistedDir.y = 0f;
        return assistedDir.sqrMagnitude > 0.0001f ? assistedDir.normalized : baseDir;
    }

    private void UpdateTrajectory()
    {
        EnsureLineRenderers();
        if (mainTrajectoryLine == null)
            return;

        Vector3 origin = transform.position;
        float realRadius = GetRealSphereRadius();

        Vector3 baseDir = ComputeBaseCameraDirection();
        AssistedAimDirection = ComputeMagneticAimDirection(origin, baseDir);

        // Aplica cores configuradas no Inspector
        ApplyLineColors();

        // Usa SphereCast com o raio real da bola branca (94% para não raspar no chão horizontal da mesa, mantendo precisão real nas bordas laterais)
        float castRadius = realRadius * 0.94f;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            castRadius,
            AssistedAimDirection,
            maxAimDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        bool foundHit = false;
        RaycastHit bestHit = default;
        float closestDist = float.MaxValue;

        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null)
                    continue;

                // Ignora a própria bola branca
                if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform))
                    continue;

                if (h.distance <= 0.001f)
                    continue;

                BolaAlvo hitBallCandidate = h.collider.GetComponentInParent<BolaAlvo>();

                // Se não for uma BolaAlvo, ignora o piso horizontal da mesa (normais apontando para cima) e considera apenas as tabelas/paredes laterais
                if (hitBallCandidate == null && Mathf.Abs(h.normal.y) > 0.55f)
                    continue;

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    foundHit = true;
                }
            }
        }

        mainTrajectoryLine.enabled = true;

        if (!foundHit)
        {
            mainTrajectoryLine.positionCount = 2;
            mainTrajectoryLine.SetPosition(0, origin);
            mainTrajectoryLine.SetPosition(1, origin + AssistedAimDirection * maxAimDistance);

            if (targetPredictionLine != null)
                targetPredictionLine.enabled = false;
            return;
        }

        // Posição do centro da bola branca fantasma no momento exato do impacto
        Vector3 ghostBallCenter = origin + AssistedAimDirection * bestHit.distance;
        ghostBallCenter.y = origin.y;

        BolaAlvo hitBall = bestHit.collider.GetComponentInParent<BolaAlvo>();

        if (hitBall != null)
        {
            // Atingiu outra bola de sinuca!
            mainTrajectoryLine.positionCount = 2;
            mainTrajectoryLine.SetPosition(0, origin);
            mainTrajectoryLine.SetPosition(1, ghostBallCenter);

            if (targetPredictionLine != null)
            {
                // Direção real do arremesso: linha que passa pelos centros das duas esferas no momento da colisão (equivalente a -hit.normal horizontal)
                Vector3 targetBallCenter = hitBall.transform.position;
                targetBallCenter.y = origin.y;

                Vector3 launchDir = targetBallCenter - ghostBallCenter;
                launchDir.y = 0f;

                if (launchDir.sqrMagnitude < 0.0001f)
                {
                    launchDir = -bestHit.normal;
                    launchDir.y = 0f;
                }
                launchDir.Normalize();

                targetPredictionLine.enabled = true;
                targetPredictionLine.positionCount = 2;
                targetPredictionLine.SetPosition(0, targetBallCenter);
                targetPredictionLine.SetPosition(1, targetBallCenter + launchDir * targetLineLength);
            }
        }
        else
        {
            // Atingiu uma parede/tabela da mesa: desenha o trajeto principal + segmento refletido (Vector3.Reflect)
            if (targetPredictionLine != null)
                targetPredictionLine.enabled = false;

            Vector3 wallNormal = bestHit.normal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude < 0.0001f)
                wallNormal = -AssistedAimDirection;
            else
                wallNormal.Normalize();

            Vector3 reflectedDir = Vector3.Reflect(AssistedAimDirection, wallNormal);
            reflectedDir.y = 0f;
            reflectedDir.Normalize();

            mainTrajectoryLine.positionCount = 3;
            mainTrajectoryLine.SetPosition(0, origin);
            mainTrajectoryLine.SetPosition(1, ghostBallCenter);
            mainTrajectoryLine.SetPosition(2, ghostBallCenter + reflectedDir * wallReflectionLength);
        }
    }

    private void ApplyLineColors()
    {
        if (mainTrajectoryLine != null)
        {
            mainTrajectoryLine.startColor = mainLineColor;
            Color endMain = mainLineColor;
            endMain.a *= 0.35f;
            mainTrajectoryLine.endColor = endMain;
        }

        if (targetPredictionLine != null)
        {
            targetPredictionLine.startColor = targetLineColor;
            Color endTarget = targetLineColor;
            endTarget.a *= 0.25f;
            targetPredictionLine.endColor = endTarget;
        }
    }

    private void SetLinesVisible(bool visible)
    {
        if (mainTrajectoryLine != null)
            mainTrajectoryLine.enabled = visible;

        if (targetPredictionLine != null)
            targetPredictionLine.enabled = visible;
    }

    private void OnDisable()
    {
        SetLinesVisible(false);
        if (wasAimingLastFrame)
        {
            if (GameFeelController.Instance != null)
                GameFeelController.Instance.DesiredBaseTimeScale = 1f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            wasAimingLastFrame = false;
        }
    }
}
