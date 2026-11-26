using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class PocketAssist : MonoBehaviour
{
    [Header("Sucção / Funil para Bolas Alvo (Apenas em Hit Stun)")]
    [Tooltip("Força de atração horizontal em direção ao centro da caçapa")]
    [SerializeField] public float pullForce = 25f;

    [Tooltip("Força vertical para baixo (Vector3.down) para forçar a entrada da bola no buraco")]
    [SerializeField] public float downwardForce = 15f;

    [Tooltip("Amortecimento da velocidade horizontal (X e Z) a cada tick dentro do funil (ex: 0.92f)")]
    [Range(0.5f, 1f)]
    [SerializeField] public float horizontalDamping = 0.92f;

    [Header("Anti-Falta (Bola Branca) e Repulsão de Bolas Não-Atordoadas")]
    [Tooltip("Ativa a repulsão suave na borda da caçapa para evitar faltas acidentais da bola branca")]
    [SerializeField] public bool enableCueBallAvoidance = true;

    [Tooltip("Força suave que empurra a bola branca (ou bola alvo não atordoada) de volta para o centro da mesa")]
    [SerializeField] public float cueBallRepelForce = 16f;

    [Tooltip("Raio interno de falta da bola branca: acima dele ela é repelida na borda; abaixo dele ela cai normalmente")]
    [SerializeField] public float innerFoulRadius = 0.15f;

    private SphereCollider triggerCollider;
    private static Vector3 tableCenter = new Vector3(-6.0f, 71.8f, 35.0f);
    private static bool tableCenterInitialized;

    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (!tableCenterInitialized)
        {
            GameObject areaObj = GameObject.Find("AreaSeguraMesa");
            if (areaObj != null && areaObj.TryGetComponent<BoxCollider>(out BoxCollider bc))
            {
                tableCenter = bc.bounds.center;
                tableCenterInitialized = true;
            }
        }
    }

    public float GetWorldSuctionRadius()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        if (triggerCollider == null)
            return 1f;

        Vector3 s = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        return triggerCollider.radius * maxScale;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null)
            return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || rb.isKinematic)
            return;

        // 1. Bola Alvo: verifica estritamente IsHitStunned
        BolaAlvo targetBall = other.GetComponentInParent<BolaAlvo>();
        if (targetBall != null)
        {
            if (targetBall.IsHitStunned)
            {
                // Atingida pelo jogador (ou carambola ativa): aplica sucção/funil para entrar bonito
                ApplyTargetBallFunnel(rb);
            }
            else
            {
                // Andando sozinha (IsHitStunned == false): NÃO puxa; repele suavemente de volta para o centro da mesa
                ApplyAutonomousTargetBallRepulsion(rb);
            }
            return;
        }

        // 2. Bola Branca do Jogador (Anti-Falta na borda externa)
        BallPhysicsController cueBall = other.GetComponentInParent<BallPhysicsController>();
        if (cueBall != null)
        {
            ApplyCueBallEdgeAvoidance(rb);
        }
    }

    private void ApplyTargetBallFunnel(Rigidbody ballRb)
    {
        Vector3 pocketCenter = transform.position;
        Vector3 ballPos = ballRb.position;

        Vector3 toPocketHorizontal = new Vector3(pocketCenter.x - ballPos.x, 0f, pocketCenter.z - ballPos.z);
        float horizontalDist = toPocketHorizontal.magnitude;
        float maxRadius = Mathf.Max(0.01f, GetWorldSuctionRadius());

        float proximity = 1f - Mathf.Clamp01(horizontalDist / maxRadius);
        Vector3 pullDir = horizontalDist > 0.001f ? (toPocketHorizontal / horizontalDist) : Vector3.zero;

        float scaledPull = pullForce * Mathf.Lerp(0.45f, 1.35f, proximity);
        float scaledDown = downwardForce * Mathf.Lerp(0.55f, 1.5f, proximity);

        Vector3 totalAssistForce = pullDir * scaledPull + Vector3.down * scaledDown;
        ballRb.AddForce(totalAssistForce, ForceMode.Acceleration);

        Vector3 vel = ballRb.linearVelocity;
        vel.x *= horizontalDamping;
        vel.z *= horizontalDamping;
        ballRb.linearVelocity = vel;
    }

    private void ApplyAutonomousTargetBallRepulsion(Rigidbody ballRb)
    {
        Vector3 pocketCenter = transform.position;
        Vector3 ballPos = ballRb.position;

        // Direção combinada: afastando da boca da caçapa e apontando para o centro da mesa
        Vector3 awayFromPocket = new Vector3(ballPos.x - pocketCenter.x, 0f, ballPos.z - pocketCenter.z);
        Vector3 toTableCenter = new Vector3(tableCenter.x - ballPos.x, 0f, tableCenter.z - ballPos.z);

        Vector3 repelDir = (awayFromPocket.normalized + toTableCenter.normalized).normalized;
        if (repelDir.sqrMagnitude < 0.001f)
            repelDir = toTableCenter.normalized;

        // Empurra suavemente de volta para o feltro e redireciona qualquer velocidade que esteja indo para o buraco
        ballRb.AddForce(repelDir * (cueBallRepelForce * 1.5f), ForceMode.Acceleration);

        Vector3 vel = ballRb.linearVelocity;
        Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
        if (Vector3.Dot(horizVel, repelDir) < 0f)
        {
            Vector3 deflected = Vector3.Reflect(horizVel, repelDir);
            ballRb.linearVelocity = new Vector3(deflected.x, 0f, deflected.z);
        }
    }

    private void ApplyCueBallEdgeAvoidance(Rigidbody cueBallRb)
    {
        if (!enableCueBallAvoidance)
            return;

        Vector3 pocketCenter = transform.position;
        Vector3 ballPos = cueBallRb.position;

        Vector3 fromPocketHorizontal = new Vector3(ballPos.x - pocketCenter.x, 0f, ballPos.z - pocketCenter.z);
        float horizontalDist = fromPocketHorizontal.magnitude;

        if (horizontalDist <= innerFoulRadius)
            return;

        if (horizontalDist > 0.001f)
        {
            Vector3 repelDir = fromPocketHorizontal / horizontalDist;
            cueBallRb.AddForce(repelDir * cueBallRepelForce, ForceMode.Acceleration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        float suctionRadius = GetWorldSuctionRadius();

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.9f);
        Gizmos.DrawWireSphere(center, suctionRadius);

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.95f);
        Gizmos.DrawWireSphere(center, innerFoulRadius);
    }
}
