using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// [InitializeOnLoad]
public static class SetupBilliardsGame
{
    // [InitializeOnLoadMethod]
    [MenuItem("Tools/Configurar Pontuação e Caçapas")]
    public static void ConfigurarCena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Debug.Log("[Setup] Configurando HUD Neon e Gatilho do Fundo da Mesa...");

        // 1. Remover o grupo anterior de caçapas soltas, se existir
        GameObject oldCacapas = GameObject.Find("Cacapas");
        if (oldCacapas != null)
        {
            Undo.DestroyObjectImmediate(oldCacapas);
            Debug.Log("[Setup] Removido grupo antigo de caçapas esféricas.");
        }

        // 2. Localizar AreaSeguraMesa para referenciar o tamanho da mesa
        GameObject areaObj = GameObject.Find("AreaSeguraMesa");
        Vector3 tableCenter = new Vector3(-7.34f, 71.8f, 32.91f);
        Vector3 tableSize = new Vector3(100f, 0f, 188f);

        if (areaObj != null)
        {
            BoxCollider bc = areaObj.GetComponent<BoxCollider>();
            if (bc != null)
            {
                tableCenter = bc.bounds.center;
                tableSize = bc.bounds.size;
            }
        }

        // 3. Criar / Atualizar o Gatilho do Fundo da Mesa (Trigger)
        // Quando a bola cai em qualquer buraco da mesa, a gravidade a faz descer e tocar neste gatilho
        GameObject triggerFundo = GameObject.Find("GatilhoFundoMesa");
        if (triggerFundo == null)
        {
            triggerFundo = new GameObject("GatilhoFundoMesa");
            Undo.RegisterCreatedObjectUndo(triggerFundo, "Create GatilhoFundoMesa");
        }

        // Posicionado logo abaixo da superfície da mesa
        triggerFundo.transform.position = new Vector3(tableCenter.x, tableCenter.y - 2.5f, tableCenter.z);

        BoxCollider triggerCollider = triggerFundo.GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = triggerFundo.AddComponent<BoxCollider>();
        }
        triggerCollider.isTrigger = true;
        // Tamanho abrangendo toda a extensão da mesa e caçapas
        triggerCollider.size = new Vector3(tableSize.x + 15f, 3f, tableSize.z + 15f);

        Cacapa cacapaScript = triggerFundo.GetComponent<Cacapa>();
        if (cacapaScript == null)
        {
            cacapaScript = triggerFundo.AddComponent<Cacapa>();
        }
        cacapaScript.pontosPorBola = 100;
        EditorUtility.SetDirty(triggerFundo);

        // 4. Configurar Canvas UI para o HUD Neon no canto superior esquerdo
        GameObject canvasGo = GameObject.Find("CanvasHUD");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("CanvasHUD");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create CanvasHUD");
        }

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasGo.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasGo.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
        {
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // 5. Criar / Atualizar o Texto do Placar com Fonte Neon
        Transform scoreTransform = canvasGo.transform.Find("ScoreHUD");
        GameObject scoreGo;
        if (scoreTransform == null)
        {
            scoreGo = new GameObject("ScoreHUD");
            scoreGo.transform.SetParent(canvasGo.transform, false);
            Undo.RegisterCreatedObjectUndo(scoreGo, "Create ScoreHUD");
        }
        else
        {
            scoreGo = scoreTransform.gameObject;
        }

        RectTransform rect = scoreGo.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = scoreGo.AddComponent<RectTransform>();
        }

        // Fixa no canto superior esquerdo (Top-Left)
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(50f, -40f);
        rect.sizeDelta = new Vector2(550f, 100f);

        TextMeshProUGUI tmpText = scoreGo.GetComponent<TextMeshProUGUI>();
        if (tmpText == null)
        {
            tmpText = scoreGo.AddComponent<TextMeshProUGUI>();
        }

        // Carrega a fonte TiltNeon usada no menu
        string fontPath = "Assets/menu/fonte/TiltNeon-Regular-VariableFont_XROT,YROT SDF.asset";
        TMP_FontAsset neonFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (neonFont != null)
        {
            tmpText.font = neonFont;
        }

        tmpText.fontSize = 52f;
        tmpText.fontStyle = FontStyles.Bold;
        tmpText.richText = true;
        tmpText.alignment = TextAlignmentOptions.TopLeft;
        tmpText.enableWordWrapping = false;
        tmpText.text = "<color=white>Score: </color><color=#FFE600>0</color>";

        // Adiciona o efeito pulsante NeonPulse (mesmo do menu)
        NeonPulse neonPulse = scoreGo.GetComponent<NeonPulse>();
        if (neonPulse == null)
        {
            neonPulse = scoreGo.AddComponent<NeonPulse>();
        }
        neonPulse.brilhoMinimo = 0.3f;
        neonPulse.brilhoMaximo = 0.7f;
        neonPulse.velocidade = 1.2f;

        EditorUtility.SetDirty(scoreGo);

        // 6. Conectar no ScoreManager
        ScoreManager scoreMgr = Object.FindAnyObjectByType<ScoreManager>();
        if (scoreMgr == null)
        {
            GameObject mgrGo = new GameObject("ScoreManager");
            scoreMgr = mgrGo.AddComponent<ScoreManager>();
            Undo.RegisterCreatedObjectUndo(mgrGo, "Create ScoreManager");
        }
        scoreMgr.textoPontuacao = tmpText;
        scoreMgr.AtualizarInterface();
        EditorUtility.SetDirty(scoreMgr);

        // 7. Salvar alterações na cena
        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("[Setup] Sucesso! HUD Neon no canto superior esquerdo e Gatilho no fundo da mesa configurados!");
    }
}
