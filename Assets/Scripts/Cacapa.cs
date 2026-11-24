using UnityEngine;

public class Cacapa : MonoBehaviour
{
    [Header("Configurações da Caçapa")]
    [Tooltip("Pontos concedidos ao encaçapar uma bola alvo")]
    public int pontosPorBola = 100;

    [Header("Efeitos")]
    [Tooltip("Som opcional de encaçapamento")]
    public AudioSource somEncacapamento;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Caso seja uma bola alvo
        BolaAlvo bola = other.GetComponentInParent<BolaAlvo>();
        if (bola != null)
        {
            if (bola.TentarPontuar())
            {
                ScoreManager.Instance?.AdicionarPontos(pontosPorBola);
            }

            if (somEncacapamento != null)
            {
                somEncacapamento.Play();
            }

            bola.Encapacar();
            return;
        }

        // 2. Caso seja a bola do jogador (bola branca)
        BallPhysicsController jogador = other.GetComponentInParent<BallPhysicsController>();
        if (jogador != null)
        {
            Rigidbody rbJogador = jogador.GetComponent<Rigidbody>();
            if (rbJogador != null)
            {
                rbJogador.linearVelocity = Vector3.zero;
                rbJogador.angularVelocity = Vector3.zero;
            }

            // Reposiciona o jogador suavemente de volta à mesa
            BoxCollider mesaArea = Object.FindAnyObjectByType<BoxCollider>();
            if (mesaArea != null)
            {
                jogador.transform.position = new Vector3(mesaArea.bounds.center.x, mesaArea.bounds.max.y + 1.2f, mesaArea.bounds.center.z);
            }
        }
    }
}
