using UnityEngine;

public class Cacapa : MonoBehaviour
{
    [Header("Configurações da Caçapa")]
    [Tooltip("Pontos concedidos ao encaçapar uma bola alvo")]
    public int pontosPorBola = 100;

    [Header("Efeitos")]
    [Tooltip("Som opcional local de encaçapamento (fallback)")]
    public AudioSource somEncacapamento;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Caso seja uma bola alvo
        BolaAlvo bola = other.GetComponentInParent<BolaAlvo>();
        if (bola != null)
        {
            if (bola.TentarPontuar())
            {
                AudioManager.Instance?.PlayPocket(1f);
                ScoreManager.Instance?.AdicionarPontos(pontosPorBola);
            }

            if (somEncacapamento != null)
            {
                somEncacapamento.Play();
            }

            bola.Encapacar();
            return;
        }

        // 2. Caso seja a bola do jogador (bola branca - falta)
        BallPhysicsController jogador = other.GetComponentInParent<BallPhysicsController>();
        if (jogador != null)
        {
            AudioManager.Instance?.PlayFoul(1f);
            ScoreManager.Instance?.ZerarComboPorFalta();

            Rigidbody rbJogador = jogador.GetComponent<Rigidbody>();
            if (rbJogador != null)
            {
                rbJogador.linearVelocity = Vector3.zero;
                rbJogador.angularVelocity = Vector3.zero;
            }

            // Reposiciona o jogador suavemente de volta à mesa
            GameObject areaObj = GameObject.Find("AreaSeguraMesa");
            BoxCollider mesaArea = areaObj != null ? areaObj.GetComponent<BoxCollider>() : Object.FindAnyObjectByType<BoxCollider>();
            if (mesaArea != null)
            {
                jogador.transform.position = new Vector3(mesaArea.bounds.center.x, mesaArea.bounds.max.y + 1.2f, mesaArea.bounds.center.z);
            }
        }
    }
}
