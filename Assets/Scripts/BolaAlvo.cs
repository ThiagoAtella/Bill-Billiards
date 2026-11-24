using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class BolaAlvo : MonoBehaviour
{
    [Header("Área segura")]
    public BoxCollider areaSegura;
    public float margemExtra = 0.2f;

    [Header("Movimentação")]
    public float velocidade = 1.5f;

    [Header("Colisão com jogador")]
    public float impulsoMinimo = 0.2f;
    public float multiplicadorImpulso = 0.1f;

    [Header("Encaçapar e Respawn")]
    [Tooltip("Tempo em segundos para a bola reaparecer no rack após ser encaçapada")]
    public float tempoParaRespawn = 4f;

    private Rigidbody rb;
    private SphereCollider esfera;
    private MeshRenderer meshRenderer;

    private Vector3 destino;
    private float raio;
    private float altura;

    private bool atingida = false;
    private bool pontuada = false;
    private bool encacapada = false;

    // Posição e rotação originais para o respawn
    private Vector3 posicaoInicial;
    private Quaternion rotacaoInicial;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        esfera = GetComponent<SphereCollider>();
        meshRenderer = GetComponent<MeshRenderer>();

        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        if (areaSegura == null)
        {
            Debug.LogError("Área segura não configurada!");
            enabled = false;
            return;
        }

        Vector3 escala = transform.lossyScale;

        raio = esfera.radius * Mathf.Max(
            Mathf.Abs(escala.x),
            Mathf.Abs(escala.y),
            Mathf.Abs(escala.z)
        );

        altura = areaSegura.bounds.max.y + raio;
        rb.position = LimitarNaArea(rb.position);

        EscolherDestino();
    }

    void Update()
    {
        // Fallback: se a bola foi atingida e caiu da mesa (Y menor que a altura da mesa - 1.5)
        if (atingida && !encacapada && areaSegura != null)
        {
            if (transform.position.y < altura - 1.5f)
            {
                if (TentarPontuar())
                {
                    ScoreManager.Instance?.AdicionarPontos(100);
                }
                Encapacar();
            }
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
        // 1. Zera a física e esconde a bola
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        esfera.enabled = false;
        if (meshRenderer != null) meshRenderer.enabled = false;

        // 2. Aguarda o tempo de respawn
        yield return new WaitForSeconds(delay);

        // 3. Reposiciona na posição inicial no Rack
        transform.position = posicaoInicial;
        transform.rotation = rotacaoInicial;
        rb.position = posicaoInicial;

        // 4. Reseta as flags
        atingida = false;
        pontuada = false;
        encacapada = false;

        // 5. Reativa visual, colisor e reposiciona na mesa
        if (meshRenderer != null) meshRenderer.enabled = true;
        esfera.enabled = true;

        if (areaSegura != null)
        {
            rb.position = LimitarNaArea(rb.position);
            EscolherDestino();
        }
    }

    float LimiteX()
    {
        float escala = Mathf.Max(
            0.0001f,
            Mathf.Abs(areaSegura.transform.lossyScale.x)
        );

        return Mathf.Max(
            0f,
            areaSegura.size.x / 2f
            - (raio + margemExtra) / escala
        );
    }

    float LimiteZ()
    {
        float escala = Mathf.Max(
            0.0001f,
            Mathf.Abs(areaSegura.transform.lossyScale.z)
        );

        return Mathf.Max(
            0f,
            areaSegura.size.z / 2f
            - (raio + margemExtra) / escala
        );
    }

    Vector3 LimitarNaArea(Vector3 posicao)
    {
        Transform area = areaSegura.transform;

        Vector3 local = area.InverseTransformPoint(posicao);
        Vector3 centro = areaSegura.center;

        local.x = Mathf.Clamp(
            local.x,
            centro.x - LimiteX(),
            centro.x + LimiteX()
        );

        local.z = Mathf.Clamp(
            local.z,
            centro.z - LimiteZ(),
            centro.z + LimiteZ()
        );

        local.y = centro.y;

        Vector3 resultado = area.TransformPoint(local);
        resultado.y = altura;

        return resultado;
    }

    void EscolherDestino()
    {
        Vector3 centro = areaSegura.center;

        Vector3 local = centro + new Vector3(
            Random.Range(-LimiteX(), LimiteX()),
            0f,
            Random.Range(-LimiteZ(), LimiteZ())
        );

        destino = areaSegura.transform.TransformPoint(local);
        destino.y = altura;
    }

    void FixedUpdate()
    {
        if (atingida || encacapada || areaSegura == null)
            return;

        Vector3 atual = rb.position;

        if (Vector3.Distance(atual, destino) < 0.1f)
        {
            EscolherDestino();
        }

        Vector3 proxima = Vector3.MoveTowards(
            atual,
            destino,
            velocidade * Time.fixedDeltaTime
        );

        proxima = LimitarNaArea(proxima);
        Vector3 movimento = proxima - atual;

        rb.MovePosition(proxima);

        if (movimento.sqrMagnitude > 0.000001f)
        {
            Vector3 eixo = Vector3.Cross(
                Vector3.up,
                movimento.normalized
            );

            float angulo = movimento.magnitude /
                           raio * Mathf.Rad2Deg;

            rb.MoveRotation(
                Quaternion.AngleAxis(angulo, eixo)
                * rb.rotation
            );
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (atingida || encacapada)
            return;

        BallPhysicsController jogador =
            collision.collider.GetComponentInParent
            <BallPhysicsController>();

        if (jogador == null)
            return;

        atingida = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        Rigidbody corpoJogador =
            jogador.GetComponent<Rigidbody>();

        Vector3 direcao =
            rb.position - jogador.transform.position;

        direcao.y = 0f;

        float velocidadeJogador = 0f;

        if (corpoJogador != null)
        {
            Vector3 v = corpoJogador.linearVelocity;
            v.y = 0f;
            velocidadeJogador = v.magnitude;

            if (direcao.sqrMagnitude < 0.0001f)
                direcao = v;
        }

        if (direcao.sqrMagnitude < 0.0001f)
            direcao = Vector3.forward;

        float impulso = Mathf.Max(
            impulsoMinimo,
            velocidadeJogador * multiplicadorImpulso
        );

        rb.AddForce(
            direcao.normalized * impulso,
            ForceMode.Impulse
        );
    }

    public bool TentarPontuar()
    {
        if (!atingida || pontuada)
            return false;

        pontuada = true;
        return true;
    }
}
