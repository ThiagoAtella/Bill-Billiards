using System.Collections;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("UI TextMeshPro HUD")]
    [Tooltip("Componente TextMeshProUGUI do placar no canto superior esquerdo")]
    public TextMeshProUGUI textoPontuacao;

    [Tooltip("Componente TextMeshProUGUI do contador de Combo (Txt_Combo)")]
    public TextMeshProUGUI textoCombo;

    [Header("Pontuação")]
    public int pontuacaoAtual = 0;

    [Header("Sistema de Combo")]
    [Tooltip("Multiplicador atual do combo (0 = sem combo ativo, 1 = x1, 2 = x2, etc.)")]
    public int comboAtual = 0;

    [Tooltip("Janela de tempo em segundos para encaçapar a próxima bola antes do combo zerar")]
    public float tempoJanelaCombo = 5f;

    [Tooltip("Tempo restante da janela do combo atual")]
    public float tempoRestanteCombo = 0f;

    [Header("Efeito Scale Punch do Combo")]
    [Tooltip("Escala máxima do texto de combo no momento em que o combo sobe")]
    public float comboPunchScale = 1.45f;

    [Tooltip("Duração do efeito de scale punch em segundos")]
    public float comboPunchDuration = 0.22f;

    [Header("Cores do Placar")]
    public string corTituloHex = "#FFFFFF";
    public string corNumeroHex = "#FFE600";
    public string corComboHex = "#FFB300";

    private Vector3 escalaOriginalCombo = Vector3.one;
    private Coroutine punchCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (textoCombo != null)
        {
            escalaOriginalCombo = textoCombo.rectTransform.localScale;
            if (escalaOriginalCombo.sqrMagnitude < 0.01f)
                escalaOriginalCombo = Vector3.one;
        }
    }

    private void Start()
    {
        AtualizarInterface();
    }

    private void Update()
    {
        if (comboAtual > 0)
        {
            tempoRestanteCombo -= Time.deltaTime;
            if (tempoRestanteCombo <= 0f)
            {
                ZerarCombo();
            }
            else
            {
                AtualizarTextoCombo();
            }
        }
    }

    public void AdicionarPontos(int pontosBase)
    {
        comboAtual++;
        tempoRestanteCombo = tempoJanelaCombo;

        int pontosGanhos = pontosBase * Mathf.Max(1, comboAtual);
        pontuacaoAtual += pontosGanhos;

        AtualizarInterface();
        DispararScalePunchCombo();
        AudioManager.Instance?.PlayComboUp(comboAtual, 1f);
    }

    public void ZerarCombo()
    {
        comboAtual = 0;
        tempoRestanteCombo = 0f;
        AtualizarInterface();
    }

    public void ZerarComboPorFalta()
    {
        comboAtual = 0;
        tempoRestanteCombo = 0f;
        AtualizarInterface();
    }

    public void ResetarPontos()
    {
        pontuacaoAtual = 0;
        comboAtual = 0;
        tempoRestanteCombo = 0f;
        AtualizarInterface();
    }

    public void AtualizarInterface()
    {
        if (textoPontuacao != null)
        {
            textoPontuacao.text = $"<color={corTituloHex}>Score: </color><color={corNumeroHex}>{pontuacaoAtual}</color>";
        }

        AtualizarTextoCombo();
    }

    private void AtualizarTextoCombo()
    {
        if (textoCombo == null)
            return;

        if (comboAtual > 0)
        {
            textoCombo.text = $"<color={corComboHex}>COMBO x{comboAtual}</color> <size=65%><color=#FFFFFF>({tempoRestanteCombo:0.0}s)</color></size>";
        }
        else
        {
            textoCombo.text = $"<color={corComboHex}>COMBO x1</color>";
        }
    }

    private void DispararScalePunchCombo()
    {
        if (textoCombo == null)
            return;

        if (punchCoroutine != null)
        {
            StopCoroutine(punchCoroutine);
            textoCombo.rectTransform.localScale = escalaOriginalCombo;
        }

        punchCoroutine = StartCoroutine(RotinaScalePunch());
    }

    private IEnumerator RotinaScalePunch()
    {
        RectTransform rt = textoCombo.rectTransform;
        Vector3 pico = escalaOriginalCombo * comboPunchScale;
        float metade = Mathf.Max(0.01f, comboPunchDuration * 0.4f);
        float retorno = Mathf.Max(0.01f, comboPunchDuration * 0.6f);

        float t = 0f;
        while (t < metade)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(escalaOriginalCombo, pico, t / metade);
            yield return null;
        }

        t = 0f;
        while (t < retorno)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(pico, escalaOriginalCombo, t / retorno);
            yield return null;
        }

        rt.localScale = escalaOriginalCombo;
        punchCoroutine = null;
    }
}
