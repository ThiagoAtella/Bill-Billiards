using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BallSound : MonoBehaviour
{
    [Header("Limites de Velocidade para Volume")]
    [Tooltip("Velocidade mínima de impacto para disparar som (ignora encostadas leves)")]
    [SerializeField] public float minImpactSpeed = 0.5f;

    [Tooltip("Velocidade de impacto correspondente ao volume máximo (1.0)")]
    [SerializeField] public float maxImpactSpeed = 15f;

    [Header("Anti-Spam")]
    [Tooltip("Mini cooldown em segundos entre sons de colisão na mesma bola")]
    [SerializeField] public float collisionCooldown = 0.05f;

    private float lastSoundTime = -10f;
    private BallPhysicsController playerController;

    private void Awake()
    {
        playerController = GetComponent<BallPhysicsController>();
    }

    private void OnCollisionEnter(Collision col)
    {
        if (AudioManager.Instance == null || col == null || col.collider == null)
            return;

        if (Time.unscaledTime - lastSoundTime < collisionCooldown)
            return;

        float speed = col.relativeVelocity.magnitude;

        // Caso uma das bolas seja o jogador colidindo com uma BolaAlvo que estava isKinematic,
        // garante a leitura da velocidade real pré-colisão
        if (playerController != null)
        {
            speed = Mathf.Max(speed, playerController.LastPreCollisionVelocity.magnitude);
        }
        else
        {
            BallPhysicsController otherPlayer = col.collider.GetComponentInParent<BallPhysicsController>();
            if (otherPlayer != null)
            {
                speed = Mathf.Max(speed, otherPlayer.LastPreCollisionVelocity.magnitude);
            }
        }

        if (speed < minImpactSpeed)
            return;

        BallSound otherBall = col.collider.GetComponentInParent<BallSound>();
        bool isBallToBall = otherBall != null ||
                            col.collider.GetComponentInParent<BolaAlvo>() != null ||
                            col.collider.GetComponentInParent<BallPhysicsController>() != null;

        if (isBallToBall)
        {
            // Evita som duplicado em colisão bola-bola: apenas o objeto com maior GetInstanceID() dispara o som
            GameObject otherRoot = otherBall != null ? otherBall.gameObject : col.collider.gameObject;
            if (gameObject.GetInstanceID() < otherRoot.GetInstanceID())
                return;

            float volume = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
            lastSoundTime = Time.unscaledTime;
            if (otherBall != null)
                otherBall.RegisterSharedCollisionTime(lastSoundTime);

            AudioManager.Instance.PlayBallHit(volume);
        }
        else
        {
            // Verifica se bateu na tabela/parede lateral da mesa (ignorando o contato vertical com o chão/pano da mesa)
            if (col.contactCount > 0)
            {
                ContactPoint contact = col.GetContact(0);
                if (Mathf.Abs(contact.normal.y) > 0.6f)
                    return;
            }

            float volume = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
            lastSoundTime = Time.unscaledTime;
            AudioManager.Instance.PlayCushionHit(volume);
        }
    }

    public void RegisterSharedCollisionTime(float time)
    {
        lastSoundTime = time;
    }
}
