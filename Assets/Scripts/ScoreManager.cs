using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI TextMeshPro HUD")]
    [Tooltip("Componente TextMeshProUGUI no canto superior esquerdo")]
    public TextMeshProUGUI textoPontuacao;

    [Header("Pontuação")]
    public int pontuacaoAtual = 0;

    [Header("Cores do Placar")]
    public string corTituloHex = "#FFFFFF"; // Branco
    public string corNumeroHex = "#FFE600"; // Amarelo Neon

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        AtualizarInterface();
    }

    public void AdicionarPontos(int quantidade)
    {
        pontuacaoAtual += quantidade;
        AtualizarInterface();
    }

    public void ResetarPontos()
    {
        pontuacaoAtual = 0;
        AtualizarInterface();
    }

    public void AtualizarInterface()
    {
        if (textoPontuacao != null)
        {
            // "Score: " em branco, e o valor numérico em amarelo neon
            textoPontuacao.text = $"<color={corTituloHex}>Score: </color><color={corNumeroHex}>{pontuacaoAtual}</color>";
        }
    }
}
