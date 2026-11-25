using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class BallPhysicsController : MonoBehaviour
{
    [Header("Movimentação Normal")]
    public float acceleration = 65f;
    public float maxSpeed = 24f;

    [Header("Dash / Tacada Explosiva (Estilo Rocket League)")]
    [Tooltip("Velocidade explosiva instantânea aplicada no início do Dash")]
    public float dashSpeed = 75f;

    [Tooltip("Impulso adicional do Dash (mantido para ajuste fino)")]
    public float dashImpulse = 75f;

    [Tooltip("Duração da janela ativa de impacto do Dash (isDashing) em segundos")]
    public float dashDuration = 0.35f;

    [Tooltip("Tempo de recarga entre Dashes em segundos")]
    public float dashCooldown = 1.1f;

    [Tooltip("Multiplicador de transferência de velocidade ao atingir outra bola durante o Dash")]
    public float dashHitMultiplier = 1.45f;

    [Tooltip("Bônus extra de impulso transferido na direção da tacada durante o Dash")]
    public float dashBonusImpactImpulse = 25f;

    [Tooltip("Estado atual da janela de Dash")]
    public bool isDashing;

    [Header("Freio Seco / Drift (Left Shift)")]
    [Tooltip("Linear Damping normal da bola em rolamento livre")]
    public float normalLinearDamping = 0.08f;

    [Tooltip("Linear Damping alto aplicado ao segurar Left Shift para frear/cortar inércia")]
    public float brakeLinearDamping = 7.5f;

    [Tooltip("Multiplicador de aceleração direcional durante o Drift para fazer curvas fechadas")]
    public float driftCorneringMultiplier = 1.85f;

    [Tooltip("Indica se o jogador está segurando o freio/drift (Left Shift)")]
    public bool isBraking;

    [Header("Rastro Visual (TrailRenderer)")]
    [Tooltip("TrailRenderer que emite durante o Dash ou em alta velocidade")]
    public TrailRenderer dashTrail;

    [Tooltip("Velocidade mínima para ativar o rastro mesmo fora do Dash")]
    public float trailSpeedThreshold = 32f;

    private Rigidbody rb;
    private StarterAssetsInputs input;
    private Transform cameraTransform;
    private AimTrajectory aimTrajectory;

    private float nextDashTime;
    private float dashEndTime;
    private bool dashRequested;
    private Vector3 lastFixedHorizontalVelocity;

    public float CurrentHorizontalSpeed => lastFixedHorizontalVelocity.magnitude;
    public Vector3 LastPreCollisionVelocity => lastFixedHorizontalVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
        if (input != null && input.jump)
        {
            dashRequested = true;
            input.jump = false;
        }

        // Verifica Left Shift tanto pelo StarterAssetsInputs (sprint) quanto direto pelo Keyboard
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
        isBraking = shiftHeld;

        if (isDashing && Time.time >= dashEndTime)
        {
            isDashing = false;
        }

        UpdateTrail();
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

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * input.move.y + right * input.move.x;
        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        lastFixedHorizontalVelocity = horizontalVelocity;

        // 1. Mecânica de Freio Seco / Drift (Left Shift)
        if (isBraking && !isDashing)
        {
            rb.linearDamping = brakeLinearDamping;

            // Se o jogador estiver virando durante o drift, corta a velocidade lateral para fazer curva fechada
            if (direction.sqrMagnitude > 0.01f)
            {
                Vector3 targetDir = direction.normalized;
                Vector3 forwardComponent = Vector3.Project(horizontalVelocity, targetDir);
                Vector3 lateralComponent = horizontalVelocity - forwardComponent;

                // Corta rapidamente a derrapagem lateral
                rb.AddForce(-lateralComponent * brakeLinearDamping, ForceMode.Acceleration);
            }
        }
        else
        {
            rb.linearDamping = normalLinearDamping;
        }

        // 2. Movimentação normal
        if (direction.sqrMagnitude > 0.01f)
        {
            Vector3 targetDir = direction.normalized;
            float currentSpeedInDir = Vector3.Dot(horizontalVelocity, targetDir);
            float effectiveAccel = isBraking ? acceleration * driftCorneringMultiplier : acceleration;

            if (currentSpeedInDir < maxSpeed)
            {
                float neededAccel = Mathf.Min(effectiveAccel, (maxSpeed - currentSpeedInDir) / Time.fixedDeltaTime);
                rb.AddForce(targetDir * neededAccel, ForceMode.Acceleration);
            }
        }

        // 3. Dash Explosivo com janela ativa (isDashing)
        if (dashRequested)
        {
            dashRequested = false;

            if (Time.time >= nextDashTime)
            {
                ExecuteDash(direction, forward);
            }
        }
    }

    private void ExecuteDash(Vector3 moveDirection, Vector3 cameraForward)
    {
        if (aimTrajectory == null) aimTrajectory = GetComponent<AimTrajectory>();
        Vector3 dashDirection = (aimTrajectory != null && aimTrajectory.IsAiming)
            ? aimTrajectory.AssistedAimDirection
            : (moveDirection.sqrMagnitude > 0.01f ? moveDirection.normalized : cameraForward);

        // Corta o freio e zera velocidade contrária/lateral para uma arrancada limpa e explosiva
        rb.linearDamping = normalLinearDamping;
        Vector3 currentVel = rb.linearVelocity;
        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(new Vector3(currentVel.x, 0f, currentVel.z), dashDirection));

        float targetBurstSpeed = Mathf.Max(dashSpeed, dashImpulse, forwardSpeed + dashSpeed * 0.65f);
        Vector3 newHorizontalVel = dashDirection * targetBurstSpeed;

        // VelocityChange garante arrancada imediata independente do atrito estático inicial
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
        BolaAlvo bolaAlvo = collision.collider.GetComponentInParent<BolaAlvo>();
        Rigidbody otherRb = collision.collider.attachedRigidbody;

        if (bolaAlvo == null && (otherRb == null || otherRb == rb))
            return;

        float impactSpeed = Mathf.Max(lastFixedHorizontalVelocity.magnitude, rb.linearVelocity.magnitude);

        // Dispara Hitstop + Camera Shake quando em Dash ou impacto forte
        if (GameFeelController.Instance != null)
        {
            GameFeelController.Instance.TriggerImpact(impactSpeed, isDashing);
        }

        // Se atingiu uma BolaAlvo, delega a transferência de momento com bônus de Dash
        if (bolaAlvo != null)
        {
            bolaAlvo.AplicarImpactoDoJogador(this, lastFixedHorizontalVelocity, isDashing);
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
}
