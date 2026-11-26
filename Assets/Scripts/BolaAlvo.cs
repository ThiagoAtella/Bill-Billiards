using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class BolaAlvo : MonoBehaviour
{
    [Header("Área Segura da Mesa")]
    public BoxCollider areaSegura;
    [Tooltip("Margem de segurança das bordas e caçapas para os destinos sorteados")]
    [SerializeField] public float margemExtra = 10f;

    [Header("Movimentação Dinâmica e Objetiva (Por Destinos na Mesa)")]
    [Tooltip("Velocidade de cruzeiro objetiva pela mesa (calibrada para a escala de 188 unidades da mesa)")]
    [SerializeField] public float targetSpeed = 14f;

    [Tooltip("Velocidade horizontal máxima permitida enquanto a bola se move sozinha")]
    [SerializeField] public float maxWanderSpeed = 16.5f;

    [Tooltip("Aceleração aplicada no FixedUpdate para manter targetSpeed com resposta imediata")]
    [SerializeField] public float acceleration = 35f;

    [Tooltip("Velocidade de alinhamento (radianos/s) da bola em direção ao ponto de destino atual")]
    [SerializeField] public float turnSpeed = 6.5f;

    [Tooltip("Intervalo mínimo (segundos) para trocar de destino / dar um pequeno impulso")]
    [SerializeField] public float burstIntervalMin = 2.5f;

    [Tooltip("Intervalo máximo (segundos) para trocar de destino / dar um pequeno impulso")]
    [SerializeField] public float burstIntervalMax = 4.5f;

    [Tooltip("Impulso extra ao iniciar um novo trajeto objetivo")]
    [SerializeField] public float burstForce = 3.5f;

    [Header("Hit Stun (Pausa na IA em Tacadas e Carambolas)")]
    [Tooltip("Duração base do Hit Stun em segundos após impacto (4.5f)")]
    [SerializeField] public float hitStunDuration = 4.5f;

    [Tooltip("Velocidade mínima acima da qual o Hit Stun continua ativo enquanto a bola desliza (1.5f)")]
    [SerializeField] public float minSpeedToKeepStun = 1.5f;

    [Tooltip("Limite máximo de segurança em segundos para o Hit Stun nunca travar infinito (7.0f)")]
    [SerializeField] public float maxHitStunSafetyDuration = 7.0f;

    [Tooltip("Velocidade mínima que uma bola em Hit Stun precisa ter para passar Hit Stun a outra bola")]
    [SerializeField] public float chainStunSpeedThreshold = 8.0f;

    [Header("Colisão com Jogador e Respawn")]
    [SerializeField] public float impulsoMinimo = 18f;
    [SerializeField] public float multiplicadorImpulso = 1.15f;

    [Tooltip("Tempo em segundos para a bola reaparecer após ser encaçapada")]
    [SerializeField] public float tempoParaRespawn = 4f;

    [HideInInspector] public float velocidade = 14f;

    private Rigidbody rb;
    private SphereCollider esfera;
    private MeshRenderer meshRenderer;

    private Vector3 destino;
    private Vector3 direcaoObjetivo;
    private float nextDestinationChangeTime;

    private float stunTimer;
    private float stunSafetyTimer;
    private float stunElapsed;

    private float raio;
    private float alturaMesa;
    private Vector3 lastFixedHorizontalVelocity;

    private bool atingida = false;
    private bool pontuada = false;
    private bool encacapada = false;
    private int ultimoFrameImpacto = -1;

    private Vector3 posicaoInicial;
    private Quaternion rotacaoInicial;
    private static PocketAssist[] cachedPockets;

    // Propriedade pública consultada pelo PocketAssist
    public bool IsHitStunned => stunTimer > 0f;
    public bool IsStunned => stunTimer > 0f;
    public bool FoiAtingida => atingida;
    public Vector3 LastPreCollisionVelocity => lastFixedHorizontalVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        esfera = GetComponent<SphereCollider>();
        meshRenderer = GetComponent<MeshRenderer>();

        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;

        ConfigureDynamicRigidbody();
    }

    private void ConfigureDynamicRigidbody()
    {
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 1f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Start()
    {
        if (areaSegura == null)
        {
            GameObject areaObj = GameObject.Find("AreaSeguraMesa");
            if (areaObj != null)
                areaSegura = areaObj.GetComponent<BoxCollider>();
        }

        if (cachedPockets == null || cachedPockets.Length == 0)
        {
            cachedPockets = Object.FindObjectsByType<PocketAssist>(FindObjectsSortMode.None);
        }

        Vector3 escala = transform.lossyScale;
        raio = esfera != null
            ? esfera.radius * Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z))
            : 1.2f;

        alturaMesa = areaSegura != null ? areaSegura.bounds.max.y + raio : transform.position.y;

        IniciarMovimentoObjetivo();
    }

    private void IniciarMovimentoObjetivo()
    {
        stunTimer = 0f;
        stunSafetyTimer = 0f;
        stunElapsed = 0f;

        EscolherNovoDestino(true);

        float initialSpeed = Mathf.Min(maxWanderSpeed, targetSpeed * Random.Range(0.95f, 1.15f));
        rb.linearVelocity = direcaoObjetivo * initialSpeed;
        lastFixedHorizontalVelocity = rb.linearVelocity;
    }

    private float LimiteX()
    {
        if (areaSegura == null) return 35f;
        float escala = Mathf.Max(0.0001f, Mathf.Abs(areaSegura.transform.lossyScale.x));
        return Mathf.Max(5f, areaSegura.size.x * 0.5f - (raio + margemExtra) / escala);
    }

    private float LimiteZ()
    {
        if (areaSegura == null) return 75f;
        float escala = Mathf.Max(0.0001f, Mathf.Abs(areaSegura.transform.lossyScale.z));
        return Mathf.Max(5f, areaSegura.size.z * 0.5f - (raio + margemExtra) / escala);
    }

    public void EscolherNovoDestino(bool aplicarBurst = false)
    {
        if (areaSegura == null)
        {
            float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            direcaoObjetivo = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)).normalized;
            destino = transform.position + direcaoObjetivo * 45f;
            return;
        }

        Vector3 centro = areaSegura.center;
        float limX = LimiteX();
        float limZ = LimiteZ();

        // Sorteia um ponto de destino objetivo bem distante da posição atual para a bola cruzar a mesa com propósito
        Vector3 candidato = transform.position;
        for (int tentativa = 0; tentativa < 8; tentativa++)
        {
            Vector3 local = centro + new Vector3(
                Random.Range(-limX, limX),
                0f,
                Random.Range(-limZ, limZ)
            );
            candidato = areaSegura.transform.TransformPoint(local);
            candidato.y = alturaMesa;

            if (Vector3.Distance(new Vector3(transform.position.x, alturaMesa, transform.position.z), candidato) >= 35f)
                break;
        }

        destino = candidato;
        Vector3 toDest = new Vector3(destino.x - transform.position.x, 0f, destino.z - transform.position.z);
        direcaoObjetivo = toDest.sqrMagnitude > 0.01f ? toDest.normalized : Vector3.forward;

        float minB = Mathf.Max(1.5f, burstIntervalMin);
        float maxB = Mathf.Max(minB, burstIntervalMax);
        nextDestinationChangeTime = Time.time + Random.Range(minB, maxB);

        if (aplicarBurst && rb != null && !rb.isKinematic && burstForce > 0.01f)
        {
            rb.AddForce(direcaoObjetivo * burstForce, ForceMode.VelocityChange);
        }
    }

    private void Update()
    {
        if (!encacapada && areaSegura != null)
        {
            if (transform.position.y < alturaMesa - 1.5f)
            {
                if (TentarPontuar())
                {
                    AudioManager.Instance?.PlayPocket(1f);
                    ScoreManager.Instance?.AdicionarPontos(100);
                }
                Encapacar();
            }
        }
    }

    private void FixedUpdate()
    {
        if (encacapada || rb == null || rb.isKinematic)
            return;

        // Trava vertical para manter as bolas alvo coladas no feltro
        if (rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
        float currentSpeed = horizontalVel.magnitude;
        lastFixedHorizontalVelocity = horizontalVel;

        // 1. Pausa na IA durante o Hit Stun:
        if (stunTimer > 0f)
        {
            stunTimer -= Time.fixedDeltaTime;
            stunSafetyTimer -= Time.fixedDeltaTime;
            stunElapsed += Time.fixedDeltaTime;

            if (stunSafetyTimer > 0f && currentSpeed > minSpeedToKeepStun)
            {
                stunTimer = Mathf.Max(stunTimer, 0.25f);
            }
            else if (stunElapsed >= 0.5f && currentSpeed <= minSpeedToKeepStun)
            {
                stunTimer = 0f;
            }

            if (stunTimer <= 0f || stunSafetyTimer <= 0f)
            {
                stunTimer = 0f;
                stunSafetyTimer = 0f;
                stunElapsed = 0f;
                if (!encacapada && transform.position.y >= alturaMesa - 0.8f)
                {
                    atingida = false;
                }
                EscolherNovoDestino(true);
            }
            return;
        }

        // 2. Verifica se chegou perto do destino atual ou se o tempo daquele trajeto expirou
        Vector3 posPlana = new Vector3(rb.position.x, 0f, rb.position.z);
        Vector3 destPlano = new Vector3(destino.x, 0f, destino.z);
        Vector3 paraDestino = destPlano - posPlana;
        float distDestino = paraDestino.magnitude;

        if (distDestino < 4.5f || Time.time >= nextDestinationChangeTime)
        {
            EscolherNovoDestino(true);
            paraDestino = new Vector3(destino.x - rb.position.x, 0f, destino.z - rb.position.z);
            distDestino = paraDestino.magnitude;
        }

        if (distDestino > 0.01f)
        {
            direcaoObjetivo = paraDestino / distDestino;
        }

        // 3. Auto-Preservação: se chegar perto de uma caçapa ou sair da margem segura, troca imediatamente o destino para o lado oposto / centro
        if (VerificarAutoPreservacao(out Vector3 safeDirToCenter))
        {
            direcaoObjetivo = safeDirToCenter;
            if (areaSegura != null)
            {
                destino = areaSegura.bounds.center + safeDirToCenter * 25f;
                destino.y = alturaMesa;
            }
        }

        // 4. Alinha o vetor de velocidade de forma objetiva com a direção do destino e garante velocidade viva (targetSpeed)
        if (currentSpeed > 0.2f)
        {
            Vector3 alignedDir = Vector3.RotateTowards(
                horizontalVel.normalized,
                direcaoObjetivo,
                turnSpeed * Time.fixedDeltaTime,
                0f
            ).normalized;

            horizontalVel = alignedDir * currentSpeed;
        }
        else
        {
            horizontalVel = direcaoObjetivo * Mathf.Min(targetSpeed, acceleration * Time.fixedDeltaTime * 3f);
        }

        // Acelera decididamente até targetSpeed
        if (horizontalVel.magnitude < targetSpeed)
        {
            float newMag = Mathf.MoveTowards(horizontalVel.magnitude, targetSpeed, acceleration * Time.fixedDeltaTime);
            horizontalVel = (horizontalVel.sqrMagnitude > 0.001f ? horizontalVel.normalized : direcaoObjetivo) * newMag;
        }
        else if (horizontalVel.magnitude > maxWanderSpeed)
        {
            horizontalVel = horizontalVel.normalized * maxWanderSpeed;
        }

        rb.linearVelocity = new Vector3(horizontalVel.x, vel.y, horizontalVel.z);
        lastFixedHorizontalVelocity = horizontalVel;
    }

    private bool VerificarAutoPreservacao(out Vector3 dirToCenter)
    {
        dirToCenter = Vector3.forward;
        if (areaSegura == null)
            return false;

        Bounds b = areaSegura.bounds;
        Vector3 pos = rb.position;
        Vector3 center = b.center;

        Vector3 toCenter = new Vector3(center.x - pos.x, 0f, center.z - pos.z);
        if (toCenter.sqrMagnitude > 0.001f)
            dirToCenter = toCenter.normalized;

        if (cachedPockets != null)
        {
            for (int i = 0; i < cachedPockets.Length; i++)
            {
                PocketAssist pa = cachedPockets[i];
                if (pa == null)
                    continue;

                Vector3 pPos = pa.transform.position;
                Vector3 fromPocket = new Vector3(pos.x - pPos.x, 0f, pos.z - pPos.z);
                float distToPocket = fromPocket.magnitude;
                float safePocketRadius = pa.GetWorldSuctionRadius() * 1.4f + 2f;

                if (distToPocket < safePocketRadius)
                {
                    if (fromPocket.sqrMagnitude > 0.001f)
                    {
                        dirToCenter = Vector3.Slerp(fromPocket.normalized, dirToCenter, 0.55f).normalized;
                    }
                    return true;
                }
            }
        }

        float safeMaxX = Mathf.Max(5f, b.extents.x - (raio + margemExtra));
        float safeMaxZ = Mathf.Max(5f, b.extents.z - (raio + margemExtra));

        float dx = Mathf.Abs(pos.x - center.x);
        float dz = Mathf.Abs(pos.z - center.z);

        return (dx > safeMaxX || dz > safeMaxZ);
    }

    public void AtivarHitStun(float customDuration = -1f)
    {
        atingida = true;
        float dur = customDuration > 0f ? customDuration : hitStunDuration;
        stunTimer = Mathf.Max(stunTimer, dur);
        stunSafetyTimer = Mathf.Max(stunSafetyTimer, maxHitStunSafetyDuration);
        stunElapsed = 0f;
    }

    public void AplicarImpactoDoJogador(BallPhysicsController jogador, Vector3 velocidadePreColisao, bool emDash)
    {
        if (encacapada || ultimoFrameImpacto == Time.frameCount)
            return;

        ultimoFrameImpacto = Time.frameCount;
        AtivarHitStun(hitStunDuration);

        if (rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (jogador != null)
        {
            Vector3 direcao = rb.position - jogador.transform.position;
            direcao.y = 0f;
            if (direcao.sqrMagnitude < 0.0001f)
                direcao = velocidadePreColisao.sqrMagnitude > 0.0001f ? velocidadePreColisao.normalized : Vector3.forward;
            else
                direcao.Normalize();

            float velAoLongoDoChoque = Mathf.Max(impulsoMinimo, Vector3.Dot(velocidadePreColisao, direcao) * multiplicadorImpulso);
            if (emDash)
            {
                velAoLongoDoChoque = velAoLongoDoChoque * jogador.dashHitMultiplier + jogador.dashBonusImpactImpulse;
            }

            Vector3 currentHoriz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float currentAlong = Vector3.Dot(currentHoriz, direcao);
            if (currentAlong < velAoLongoDoChoque)
            {
                rb.AddForce(direcao * (velAoLongoDoChoque - currentAlong), ForceMode.VelocityChange);
            }
            lastFixedHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (encacapada || collision == null || collision.collider == null)
            return;

        if (rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        // 1. Colisão direta com a Bola Branca (Jogador)
        BallPhysicsController jogador = collision.collider.GetComponentInParent<BallPhysicsController>();
        if (jogador != null)
        {
            AplicarImpactoDoJogador(jogador, jogador.LastPreCollisionVelocity, jogador.isDashing);
            return;
        }

        // 2. Colisão com outra Bola Alvo
        BolaAlvo outraBola = collision.collider.GetComponentInParent<BolaAlvo>();
        if (outraBola != null)
        {
            if (!IsHitStunned && outraBola.IsHitStunned)
            {
                float hitterSpeed = Mathf.Max(outraBola.LastPreCollisionVelocity.magnitude, collision.relativeVelocity.magnitude);
                if (hitterSpeed >= chainStunSpeedThreshold)
                {
                    AtivarHitStun(hitStunDuration);
                }
            }

            // Se não estiver em Hit Stun, escolhe um novo destino na direção para onde ricocheteou
            if (!IsHitStunned)
            {
                Vector3 postHoriz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (postHoriz.sqrMagnitude > 0.2f && areaSegura != null)
                {
                    direcaoObjetivo = postHoriz.normalized;
                    destino = rb.position + direcaoObjetivo * 45f;
                }
                else
                {
                    EscolherNovoDestino(false);
                }
            }
            return;
        }

        // 3. Colisão com tabela/parede da mesa
        if (!IsHitStunned && collision.contactCount > 0)
        {
            ContactPoint cp = collision.GetContact(0);
            if (Mathf.Abs(cp.normal.y) < 0.55f)
            {
                EscolherNovoDestino(false);
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (rb != null && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }

    public void Encapacar(float tempo = -1f)
    {
        if (encacapada)
            return;

        encacapada = true;
        float delay = tempo > 0f ? tempo : tempoParaRespawn;
        StartCoroutine(RotinaRespawn(delay));
    }

    private IEnumerator RotinaRespawn(float delay)
    {
        stunTimer = 0f;
        stunSafetyTimer = 0f;
        stunElapsed = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        if (esfera != null) esfera.enabled = false;
        if (meshRenderer != null) meshRenderer.enabled = false;

        yield return new WaitForSeconds(delay);

        transform.position = posicaoInicial;
        transform.rotation = rotacaoInicial;
        rb.position = posicaoInicial;

        atingida = false;
        pontuada = false;
        encacapada = false;

        if (meshRenderer != null) meshRenderer.enabled = true;
        if (esfera != null) esfera.enabled = true;

        ConfigureDynamicRigidbody();
        IniciarMovimentoObjetivo();
    }

    public bool TentarPontuar()
    {
        if (!atingida || pontuada)
            return false;

        pontuada = true;
        return true;
    }
}
