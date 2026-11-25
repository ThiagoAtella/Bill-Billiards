using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class GameFeelController : MonoBehaviour
{
    public static GameFeelController Instance { get; private set; }

    [Header("Referências")]
    public BallPhysicsController playerBall;
    public CinemachineCamera virtualCamera;
    public Camera mainCamera;

    [Header("Hitstop (Congelamento de Impacto)")]
    [Tooltip("Time.timeScale durante o hitstop (ex: 0.05 ou 0)")]
    [Range(0f, 0.2f)]
    public float hitstopTimeScale = 0.04f;

    [Tooltip("Duração do hitstop em segundos reais (WaitForSecondsRealtime)")]
    [Range(0.01f, 0.15f)]
    public float hitstopDuration = 0.035f;

    [Tooltip("Velocidade mínima do impacto para disparar Hitstop e Shake mesmo fora do Dash")]
    public float minImpactSpeedForFeel = 22f;

    [Header("Camera Shake")]
    [Tooltip("Duração da tremida de câmera no impacto")]
    public float shakeDuration = 0.15f;

    [Tooltip("Intensidade base do shake no impacto")]
    public float shakeBaseIntensity = 2.2f;

    [Tooltip("Intensidade máxima do shake em impactos fortes / Dash")]
    public float shakeMaxIntensity = 5.0f;

    [Tooltip("Frequência da tremida no Cinemachine Perlin")]
    public float shakeFrequency = 18f;

    [Header("FOV Dinâmico (Sensação de Velocidade)")]
    [Tooltip("FOV original da câmera")]
    public float baseFOV = 40f;

    [Tooltip("Aumento de FOV durante o Dash (+10 a +14 graus)")]
    [Range(8f, 20f)]
    public float dashFOVBonus = 12f;

    [Tooltip("Velocidade de abertura do FOV durante o Dash")]
    public float fovIncreaseSpeed = 10f;

    [Tooltip("Velocidade de retorno suave ao FOV original")]
    public float fovReturnSpeed = 4.5f;

    [Tooltip("Velocidade da bola a partir da qual o FOV também começa a abrir proporcionalmente")]
    public float speedForDynamicFOV = 20f;

    private CinemachineBasicMultiChannelPerlin perlinNoise;
    private float defaultNoiseAmplitude;
    private float defaultNoiseFrequency;
    private float currentFOV;
    private float currentShakeTimer;
    private float currentShakeMagnitude;
    private Coroutine hitstopCoroutine;

    public Vector3 CurrentShakeOffset { get; private set; }
    public bool IsHitstopActive => hitstopCoroutine != null;
    public float DesiredBaseTimeScale { get; set; } = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (playerBall == null)
            playerBall = Object.FindAnyObjectByType<BallPhysicsController>();

        if (virtualCamera == null)
            virtualCamera = Object.FindAnyObjectByType<CinemachineCamera>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (virtualCamera != null)
        {
            baseFOV = virtualCamera.Lens.FieldOfView;
            perlinNoise = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (perlinNoise != null)
            {
                defaultNoiseAmplitude = perlinNoise.AmplitudeGain;
                defaultNoiseFrequency = perlinNoise.FrequencyGain;
            }
        }
        else if (mainCamera != null)
        {
            baseFOV = mainCamera.fieldOfView;
        }

        currentFOV = baseFOV;
    }

    private void Update()
    {
        UpdateDynamicFOV();
        UpdateCameraShake();
    }

    private void UpdateDynamicFOV()
    {
        float targetBonus = 0f;

        if (playerBall != null)
        {
            if (playerBall.isDashing)
            {
                targetBonus = dashFOVBonus;
            }
            else
            {
                float speed = playerBall.CurrentHorizontalSpeed;
                if (speed > speedForDynamicFOV)
                {
                    float t = Mathf.InverseLerp(speedForDynamicFOV, playerBall.dashSpeed, speed);
                    targetBonus = dashFOVBonus * 0.65f * t;
                }
            }
        }

        float targetFOV = baseFOV + targetBonus;
        float lerpSpeed = targetFOV > currentFOV ? fovIncreaseSpeed : fovReturnSpeed;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.unscaledDeltaTime * lerpSpeed);

        if (virtualCamera != null)
        {
            var lens = virtualCamera.Lens;
            lens.FieldOfView = currentFOV;
            virtualCamera.Lens = lens;
        }

        if (mainCamera != null && virtualCamera == null)
        {
            mainCamera.fieldOfView = currentFOV;
        }
    }

    private void UpdateCameraShake()
    {
        if (currentShakeTimer > 0f)
        {
            currentShakeTimer -= Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(currentShakeTimer / Mathf.Max(0.01f, shakeDuration));
            float activeAmp = currentShakeMagnitude * normalizedTime;

            if (perlinNoise != null)
            {
                perlinNoise.AmplitudeGain = defaultNoiseAmplitude + activeAmp;
                perlinNoise.FrequencyGain = shakeFrequency;
            }

            CurrentShakeOffset = Random.insideUnitSphere * (activeAmp * 0.08f);

            if (currentShakeTimer <= 0f)
            {
                ResetShake();
            }
        }
    }

    private void ResetShake()
    {
        currentShakeTimer = 0f;
        CurrentShakeOffset = Vector3.zero;
        if (perlinNoise != null)
        {
            perlinNoise.AmplitudeGain = defaultNoiseAmplitude;
            perlinNoise.FrequencyGain = defaultNoiseFrequency;
        }
    }

    public void TriggerImpact(float impactSpeed, bool wasDashing)
    {
        if (!wasDashing && impactSpeed < minImpactSpeedForFeel)
            return;

        // 1. Hitstop
        if (hitstopCoroutine != null)
        {
            StopCoroutine(hitstopCoroutine);
            Time.timeScale = DesiredBaseTimeScale;
        }
        hitstopCoroutine = StartCoroutine(HitstopRoutine());

        // 2. Camera Shake proporcional à força do impacto
        float refSpeed = playerBall != null ? Mathf.Max(1f, playerBall.dashSpeed) : 60f;
        float impactFactor = Mathf.Clamp01(impactSpeed / refSpeed);
        if (wasDashing)
            impactFactor = Mathf.Max(impactFactor, 0.75f);

        currentShakeMagnitude = Mathf.Lerp(shakeBaseIntensity, shakeMaxIntensity, impactFactor);
        currentShakeTimer = shakeDuration;
    }

    private IEnumerator HitstopRoutine()
    {
        Time.timeScale = hitstopTimeScale;
        yield return new WaitForSecondsRealtime(hitstopDuration);
        Time.timeScale = DesiredBaseTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        hitstopCoroutine = null;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        ResetShake();
    }
}
