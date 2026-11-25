using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("AudioSource dedicado à música de fundo em loop (2D)")]
    [SerializeField] public AudioSource musicSource;

    [Tooltip("AudioSource dedicado aos efeitos sonoros com PlayOneShot")]
    [SerializeField] public AudioSource sfxSource;

    [Header("Clips de Música (BGM por Cena)")]
    [Tooltip("Música de fundo do MainMenu (jazz calmo)")]
    [SerializeField] public AudioClip menuBgmClip;

    [Tooltip("Música de fundo do Gameplay / SampleScene (jazz-funk animado)")]
    [SerializeField] public AudioClip gameplayBgmClip;

    [Header("Transição e Volume da Música")]
    [Tooltip("Duração total do fade suave na troca de música (segundos)")]
    [SerializeField] public float musicFadeDuration = 1.0f;

    [Tooltip("Volume alvo da música de fundo")]
    [Range(0f, 1f)]
    [SerializeField] public float targetMusicVolume = 0.35f;

    [Header("Clips de Efeitos Sonoros (SFX)")]
    [Tooltip("Array de sons para colisão bola-bola (sorteia um aleatório a cada batida)")]
    [SerializeField] public AudioClip[] ballHitClips;

    [Tooltip("Som de batida na tabela/parede da mesa")]
    [SerializeField] public AudioClip cushionHitClip;

    [Tooltip("Som de bola encaçapada")]
    [SerializeField] public AudioClip pocketClip;

    [Tooltip("Som de falta quando a bola branca cai na caçapa")]
    [SerializeField] public AudioClip foulClip;

    [Tooltip("Som do impulso do Dash")]
    [SerializeField] public AudioClip dashClip;

    [Tooltip("Som quando o multiplicador de combo sobe")]
    [SerializeField] public AudioClip comboUpClip;

    [Header("Configurações de SFX e Pitch")]
    [Range(0f, 1f)]
    [SerializeField] public float sfxMasterVolume = 1f;

    [Tooltip("Pitch mínimo na variação aleatória de SFX")]
    [SerializeField] public float minRandomPitch = 0.9f;

    [Tooltip("Pitch máximo na variação aleatória de SFX")]
    [SerializeField] public float maxRandomPitch = 1.12f;

    [Tooltip("Incremento de pitch por nível de combo (ex: 0.08f -> 1f + comboCount * 0.08f)")]
    [SerializeField] public float comboPitchStep = 0.08f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureAudioSources();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (Instance == this)
        {
            PlayMusicForScene(SceneManager.GetActiveScene().name);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this)
            return;

        PlayMusicForScene(scene.name);
    }

    public void PlayMusicForScene(string sceneName)
    {
        EnsureAudioSources();

        AudioClip targetClip = (sceneName == "MainMenu") ? menuBgmClip : gameplayBgmClip;
        if (targetClip == null)
            return;

        // Se a faixa alvo já estiver tocando no AudioSource, não reinicia a música
        if (musicSource.isPlaying && musicSource.clip == targetClip)
        {
            if (fadeCoroutine == null)
            {
                musicSource.volume = targetMusicVolume;
            }
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeMusic(targetClip));
    }

    public IEnumerator FadeMusic(AudioClip newClip)
    {
        if (musicSource == null)
            yield break;

        float halfDuration = Mathf.Max(0.05f, musicFadeDuration * 0.5f);

        // 1. Se já houver música tocando, abaixa o volume até 0
        if (musicSource.isPlaying && musicSource.clip != null && musicSource.volume > 0.001f)
        {
            float startVol = musicSource.volume;
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0f, elapsed / halfDuration);
                yield return null;
            }
        }

        musicSource.volume = 0f;
        musicSource.clip = newClip;

        if (newClip == null)
        {
            musicSource.Stop();
            fadeCoroutine = null;
            yield break;
        }

        // 2. Troca o clip, dá Play() e sobe o volume até targetMusicVolume
        musicSource.loop = true;
        musicSource.Play();

        float elapsedIn = 0f;
        while (elapsedIn < halfDuration)
        {
            elapsedIn += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetMusicVolume, elapsedIn / halfDuration);
            yield return null;
        }

        musicSource.volume = targetMusicVolume;
        fadeCoroutine = null;
    }

    private void EnsureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();

        if (musicSource == null && sources.Length > 0)
            musicSource = sources[0];

        if (sfxSource == null && sources.Length > 1)
            sfxSource = sources[1];

        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        if (sfxSource == null || sfxSource == musicSource)
            sfxSource = gameObject.AddComponent<AudioSource>();

        musicSource.spatialBlend = 0f;
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        sfxSource.spatialBlend = 0f;
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }

    public void PlayBallHit(float intensity = 1f)
    {
        if (ballHitClips == null || ballHitClips.Length == 0)
            return;

        AudioClip clip = ballHitClips[Random.Range(0, ballHitClips.Length)];
        PlaySfxWithRandomPitch(clip, intensity);
    }

    public void PlayCushionHit(float intensity = 1f)
    {
        PlaySfxWithRandomPitch(cushionHitClip, intensity);
    }

    public void PlayPocket(float intensity = 1f)
    {
        PlaySfxWithRandomPitch(pocketClip, intensity);
    }

    public void PlayFoul(float intensity = 1f)
    {
        PlaySfxWithRandomPitch(foulClip, intensity);
    }

    public void PlayDash(float intensity = 1f)
    {
        PlaySfxWithRandomPitch(dashClip, intensity);
    }

    public void PlayComboUp(int comboCount, float intensity = 1f)
    {
        if (comboUpClip == null || sfxSource == null)
            return;

        float vol = Mathf.Clamp01(intensity) * sfxMasterVolume;
        if (vol <= 0.001f)
            return;

        float escalatedPitch = Mathf.Clamp(1f + Mathf.Max(0, comboCount) * comboPitchStep, 1f, 2.2f);
        sfxSource.pitch = escalatedPitch;
        sfxSource.PlayOneShot(comboUpClip, vol);
    }

    private void PlaySfxWithRandomPitch(AudioClip clip, float intensity)
    {
        if (clip == null || sfxSource == null)
            return;

        float vol = Mathf.Clamp01(intensity) * sfxMasterVolume;
        if (vol <= 0.001f)
            return;

        sfxSource.pitch = Random.Range(minRandomPitch, maxRandomPitch);
        sfxSource.PlayOneShot(clip, vol);
    }
}
