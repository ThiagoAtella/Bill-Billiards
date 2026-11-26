using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
#endif

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class BallPhysicsController : MonoBehaviour
{
    [Header("Movimentação Normal")]
    [SerializeField] public float acceleration = 50f;
    [SerializeField] public float maxSpeed = 15f;

    [Header("Pulo Curto / Hop Arcade (Space)")]
    [Tooltip("Velocidade vertical inicial do Hop (ajustada para a escala da mesa)")]
    [SerializeField] public float jumpForce = 34f;

    [Tooltip("Multiplicador de gravidade durante a SUBIDA do pulo (evita efeito de gravidade da Lua na subida)")]
    [SerializeField] public float upwardGravityMultiplier = 14f;

    [Tooltip("Multiplicador de gravidade durante a QUEDA (rb.linearVelocity.y < 0) para o pouso ser rápido e seco")]
    [SerializeField] public float fallGravityMultiplier = 18f;

    [Tooltip("Cooldown mínimo entre pulos em segundos")]
    [SerializeField] public float jumpCooldown = 0.25f;

    [Tooltip("Margem extra abaixo do raio do SphereCollider para o Ground Check")]
    [SerializeField] public float groundCheckExtraDistance = 0.4f;

    [Tooltip("Camadas consideradas chão/mesa para o Ground Check")]
    [SerializeField] public LayerMask groundCheckMask = ~(1 << 2);

    [Tooltip("Indica se a bola branca está tocando a superfície da mesa")]
    public bool isGrounded;

    [Header("Dash / Tacada Explosiva (Botão Esquerdo do Mouse ou E)")]
    [Tooltip("Velocidade explosiva instantânea aplicada no início do Dash")]
    [SerializeField] public float dashSpeed = 75f;

    [Tooltip("Impulso adicional do Dash (mantido para ajuste fino)")]
    [SerializeField] public float dashImpulse = 75f;

    [Tooltip("Duração da janela ativa de impacto do Dash (isDashing) em segundos")]
    [SerializeField] public float dashDuration = 0.35f;

    [Tooltip("Tempo de recarga entre Dashes em segundos")]
    [SerializeField] public float dashCooldown = 1.1f;

    [Tooltip("Multiplicador de transferência de velocidade ao atingir outra bola durante o Dash")]
    [SerializeField] public float dashHitMultiplier = 1.45f;

    [Tooltip("Bônus extra de impulso transferido na direção da tacada durante o Dash")]
    [SerializeField] public float dashBonusImpactImpulse = 25f;

    [Tooltip("Amortecimento do recuo inverso ao atingir uma bola alvo (0.55 = reduz 55% do recuo para trás)")]
    [Range(0f, 0.9f)]
    [SerializeField] public float recoilDamping = 0.55f;

    [Tooltip("Estado atual da janela de Dash")]
    public bool isDashing;

    [Header("Drift / Derrapagem e Freio Suave (Left Shift)")]
    [Tooltip("Linear Damping normal da bola em rolamento livre")]
    [SerializeField] public float normalLinearDamping = 0.08f;

    [Tooltip("Velocidade de rotação do vetor de velocidade durante o Drift (radianos/s)")]
    [SerializeField] public float driftTurnSpeed = 5f;

    [Tooltip("Fator de retenção da velocidade durante o Drift (0.95 = preserva ~95% do embalo na curva)")]
    [Range(0.5f, 1f)]
    [SerializeField] public float driftSpeedRetention = 0.95f;

    [Tooltip("Linear Damping leve aplicado durante o Drift com direção (WASD)")]
    [SerializeField] public float driftDamping = 0.5f;

    [Tooltip("Desaceleração controlada (freio suave) quando segura Left Shift SEM apertar WASD")]
    [SerializeField] public float smoothBrakeDeceleration = 22f;

    [Tooltip("Indica se o jogador está segurando o Drift / Freio (Left Shift)")]
    public bool isDrifting;

    [Header("Rastro Visual (TrailRenderer)")]
    [Tooltip("TrailRenderer que emite durante o Dash ou em alta velocidade")]
    [SerializeField] public TrailRenderer dashTrail;

    [Tooltip("Velocidade mínima para ativar o rastro mesmo fora do Dash")]
    [SerializeField] public float trailSpeedThreshold = 32f;

    private Rigidbody rb;
    private SphereCollider sphereCollider;
    private StarterAssetsInputs input;
    private Transform cameraTransform;
    private AimTrajectory aimTrajectory;

    private float nextDashTime;
    private float dashEndTime;
    private bool dashRequested;

    private float nextJumpTime;
    private bool jumpRequested;
    private bool isRisingFromHop;

    private bool wasCursorLockedLastFrame;
    private Vector3 lastFixedHorizontalVelocity;

    public float CurrentHorizontalSpeed => lastFixedHorizontalVelocity.magnitude;
    public Vector3 LastPreCollisionVelocity => lastFixedHorizontalVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = normalLinearDamping;

        input = GetComponent<StarterAssetsInputs>();
        aimTrajectory = GetComponent<AimTrajectory>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (dashTrail == null)
            dashTrail = GetComponentInChildren<TrailRenderer>();

        if (dashTrail != null)
            dashTrail.emitting = false;
    }

    private void Update()
    {
        bool isCursorLockedNow = (Cursor.lockState == CursorLockMode.Locked);

        // 1. Pulo Curto (Hop) vinculado à tecla Space
        bool spacePressed = false;
        if (input != null && input.jump)
        {
            spacePressed = true;
            input.jump = false;
        }
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            spacePressed = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space))
        {
            spacePressed = true;
        }
#endif
        if (spacePressed)
        {
            jumpRequested = true;
        }

        // 2. Dash vinculado ao Botão Esquerdo do Mouse (Mouse 0) ou tecla E
        bool dashTriggered = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && isCursorLockedNow && wasCursorLockedLastFrame)
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                dashTriggered = true;
            }
        }
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            dashTriggered = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0) && isCursorLockedNow && wasCursorLockedLastFrame)
        {
            dashTriggered = true;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            dashTriggered = true;
        }
#endif
        if (dashTriggered)
        {
            dashRequested = true;
        }

        // 3. Drift / Freio vinculado ao Left Shift
        bool shiftHeld = input != null && input.sprint;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
        {
            shiftHeld = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift))
        {
            shiftHeld = true;
        }
#endif
        isDrifting = shiftHeld;

        if (isDashing && Time.time >= dashEndTime)
        {
            isDashing = false;
        }

        wasCursorLockedLastFrame = isCursorLockedNow;
        UpdateTrail();
    }

    private float GetRealSphereRadius()
    {
        if (sphereCollider == null)
            sphereCollider = GetComponent<SphereCollider>();

        Vector3 s = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        return sphereCollider != null ? sphereCollider.radius * maxScale : 1.2f;
    }

    public bool CheckGrounded()
    {
        float radius = GetRealSphereRadius();
        Vector3 origin = transform.position + Vector3.up * 0.15f;
        float castRadius = radius * 0.55f;
        float maxDist = (radius - castRadius) + 0.15f + groundCheckExtraDistance;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            castRadius,
            Vector3.down,
            maxDist,
            groundCheckMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                    continue;

                if (hits[i].normal.y > 0.35f)
                    return true;
            }
        }

        if (Physics.Raycast(
            transform.position,
            Vector3.down,
            out RaycastHit rayHit,
            radius + groundCheckExtraDistance,
            groundCheckMask,
            QueryTriggerInteraction.Ignore))
        {
            if (rayHit.collider != null && rayHit.collider.transform != transform && !rayHit.collider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateTrail()
    {
        if (dashTrail == null)
            return;

        bool shouldEmit = isDashing || CurrentHorizontalSpeed >= trailSpeedThreshold;
        if (dashTrail.emitting != shouldEmit)
        {
            dashTrail.emitting = shouldEmit;
        }
    }

    private void FixedUpdate()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform == null || input == null)
            return;

        isGrounded = CheckGrounded();

        // Assim que a velocidade vertical passar do pico do pulo (<= 0), encerra a fase de subida voluntária
        if (isRisingFromHop && rb.linearVelocity.y <= 0f)
        {
            isRisingFromHop = false;
        }

        // IMPEDE HOPS DE GOLFINHO E QUICADAS NO CHÃO:
        // Se o jogador NÃO está na subida de um Hop voluntário (Space), a bola branca NUNCA pode ter velocidade Y positiva!
        if (!isRisingFromHop && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * input.move.y + right * input.move.x;
        direction = Vector3.ClampMagnitude(direction, 1f);
        bool hasMoveInput = direction.sqrMagnitude > 0.01f;

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);

        // 1. Mecânica de Drift (Left Shift + WASD) vs Freio Suave (Left Shift sem WASD)
        if (isDrifting)
        {
            if (hasMoveInput)
            {
                rb.linearDamping = driftDamping;

                float currentSpeed = horizontalVel.magnitude;
                if (currentSpeed > 0.1f)
                {
                    Vector3 targetDir = direction.normalized;
                    Vector3 newDir = Vector3.RotateTowards(
                        horizontalVel.normalized,
                        targetDir,
                        driftTurnSpeed * Time.fixedDeltaTime,
                        0f
                    ).normalized;

                    float retentionPerStep = Mathf.Pow(Mathf.Clamp(driftSpeedRetention, 0.5f, 1f), Time.fixedDeltaTime);
                    float retainedSpeed = currentSpeed * retentionPerStep;

                    horizontalVel = newDir * retainedSpeed;
                    rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
                }
            }
            else
            {
                rb.linearDamping = driftDamping;
                horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, smoothBrakeDeceleration * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
            }
        }
        else
        {
            rb.linearDamping = normalLinearDamping;
        }

        lastFixedHorizontalVelocity = horizontalVel;

        // 2. Movimentação normal
        if (hasMoveInput)
        {
            Vector3 targetDir = direction.normalized;
            float currentSpeedInDir = Vector3.Dot(horizontalVel, targetDir);

            if (currentSpeedInDir < maxSpeed)
            {
                float neededAccel = Mathf.Min(acceleration, (maxSpeed - currentSpeedInDir) / Time.fixedDeltaTime);
                rb.AddForce(targetDir * neededAccel, ForceMode.Acceleration);
            }
        }

        // 3. Pulo Curto / Hop (Space) com Ground Check e Cooldown
        if (jumpRequested)
        {
            jumpRequested = false;

            if (isGrounded && Time.time >= nextJumpTime)
            {
                ExecuteHop();
            }
        }

        // 4. Gravidade Arcade no ar
        if (!isGrounded)
        {
            if (isRisingFromHop && rb.linearVelocity.y > 0.05f && upwardGravityMultiplier > 1f)
            {
                Vector3 extraUpGravity = Physics.gravity * (upwardGravityMultiplier - 1f);
                rb.AddForce(extraUpGravity, ForceMode.Acceleration);
            }
            else if (rb.linearVelocity.y < -0.05f && fallGravityMultiplier > 1f)
            {
                Vector3 extraFallGravity = Physics.gravity * (fallGravityMultiplier - 1f);
                rb.AddForce(extraFallGravity, ForceMode.Acceleration);
            }
        }

        // 5. Dash Explosivo — SEMPRE na direção do AimTrajectory
        if (dashRequested)
        {
            dashRequested = false;

            if (Time.time >= nextDashTime)
            {
                ExecuteDash(forward);
            }
        }
    }

    private void ExecuteHop()
    {
        isRisingFromHop = true;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        nextJumpTime = Time.time + jumpCooldown;
        isGrounded = false;
    }

    private void ExecuteDash(Vector3 cameraForward)
    {
        if (aimTrajectory == null)
            aimTrajectory = GetComponent<AimTrajectory>();

        Vector3 dashDirection = (aimTrajectory != null)
            ? aimTrajectory.GetAssistedAimDirection()
            : cameraForward;

        if (dashDirection.sqrMagnitude < 0.0001f)
            dashDirection = cameraForward;
        else
            dashDirection.Normalize();

        rb.linearDamping = normalLinearDamping;
        Vector3 currentVel = rb.linearVelocity;
        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(new Vector3(currentVel.x, 0f, currentVel.z), dashDirection));

        float targetBurstSpeed = Mathf.Max(dashSpeed, dashImpulse, forwardSpeed + dashSpeed * 0.65f);
        Vector3 newHorizontalVel = dashDirection * targetBurstSpeed;

        Vector3 velocityDelta = new Vector3(newHorizontalVel.x - currentVel.x, 0f, newHorizontalVel.z - currentVel.z);
        rb.AddForce(velocityDelta, ForceMode.VelocityChange);

        isDashing = true;
        AudioManager.Instance?.PlayDash(1f);
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;
        lastFixedHorizontalVelocity = newHorizontalVel;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Se não estiver subindo voluntariamente de um Hop (ou se bater em outra bola), corta qualquer quique vertical espúrio
        if (!isRisingFromHop && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        BolaAlvo bolaAlvo = collision.collider.GetComponentInParent<BolaAlvo>();
        Rigidbody otherRb = collision.collider.attachedRigidbody;

        if (bolaAlvo == null && (otherRb == null || otherRb == rb))
            return;

        // Ao colidir com outra bola, encerra subida de pulo e mantém a colisão 100% no plano da mesa
        isRisingFromHop = false;
        if (rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        float impactSpeed = Mathf.Max(lastFixedHorizontalVelocity.magnitude, rb.linearVelocity.magnitude);

        if (GameFeelController.Instance != null)
        {
            GameFeelController.Instance.TriggerImpact(impactSpeed, isDashing);
        }

        if (bolaAlvo != null)
        {
            bolaAlvo.AplicarImpactoDoJogador(this, lastFixedHorizontalVelocity, isDashing);
            AmortecerRecuoInverso(bolaAlvo.transform.position);
        }
        else if (otherRb != null && !otherRb.isKinematic && isDashing)
        {
            Vector3 hitDir = (otherRb.position - rb.position);
            hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f)
                hitDir = lastFixedHorizontalVelocity.normalized;
            else
                hitDir.Normalize();

            float bonusForce = impactSpeed * (dashHitMultiplier - 1f) + dashBonusImpactImpulse;
            otherRb.AddForce(hitDir * bonusForce, ForceMode.Impulse);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!isRisingFromHop && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }

    private void AmortecerRecuoInverso(Vector3 targetBallPosition)
    {
        Vector3 postVel = rb.linearVelocity;
        Vector3 postHoriz = new Vector3(postVel.x, 0f, postVel.z);

        // Direção da tacada pré-impacto (ou direção até a bola atingida)
        Vector3 shotDir = lastFixedHorizontalVelocity.sqrMagnitude > 0.01f
            ? lastFixedHorizontalVelocity.normalized
            : new Vector3(targetBallPosition.x - rb.position.x, 0f, targetBallPosition.z - rb.position.z).normalized;

        if (shotDir.sqrMagnitude < 0.0001f)
            return;

        // 1. Se a velocidade pós-impacto tiver componente oposta à direção da tacada, amortece esse recuo em ~55%
        float forwardDot = Vector3.Dot(postHoriz, shotDir);
        if (forwardDot < 0f)
        {
            Vector3 backwardComponent = shotDir * forwardDot; // aponta para trás
            Vector3 lateralComponent = postHoriz - backwardComponent;
            postHoriz = lateralComponent + backwardComponent * (1f - recoilDamping);
        }

        // 2. Também verifica recuo direto na linha de centro entre as duas bolas (normal do choque)
        Vector3 hitNormal = new Vector3(targetBallPosition.x - rb.position.x, 0f, targetBallPosition.z - rb.position.z);
        if (hitNormal.sqrMagnitude > 0.0001f)
        {
            hitNormal.Normalize();
            float normalDot = Vector3.Dot(postHoriz, hitNormal);
            if (normalDot < 0f)
            {
                Vector3 normalRecoil = hitNormal * normalDot;
                Vector3 tangentVel = postHoriz - normalRecoil;
                postHoriz = tangentVel + normalRecoil * (1f - recoilDamping);
            }
        }

        rb.linearVelocity = new Vector3(postHoriz.x, Mathf.Min(0f, postVel.y), postHoriz.z);
    }
}
