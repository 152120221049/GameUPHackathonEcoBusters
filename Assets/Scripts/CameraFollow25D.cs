using UnityEngine;

/// <summary>
/// 2.5D İzometrik Kamera Takip ve Görsel Tatmin (Oomph / Screen Shake & Zoom Punch) Sistemi.
/// - Oyuncuyu izometrik bir açıdan pürüzsüzce (SmoothDamp) takip eder.
/// - Vakum çekimi, inşa, üretim, tamir ve arıtma anlarında "Oomph" hissi veren
///   Kamera Sarsıntısı (Screen Shake) ve Yakınlaşma Darbesi (Zoom Kick / Punch) uygular.
/// </summary>
public class CameraFollow25D : MonoBehaviour
{
    [Header("--- HEDEF ---")]
    [Tooltip("Takip edilecek oyuncu")]
    [SerializeField] private Transform target;

    [Header("--- İZOMETRİK KONUM VE OFSET ---")]
    [Tooltip("Kameranın hedefe olan izometrik mesafesi (Örn: X: -10, Y: 14, Z: -10 veya X: 0, Y: 12, Z: -10)")]
    [SerializeField] private Vector3 offset = new Vector3(-10f, 14f, -10f);

    [Header("--- TAKİP YUMUŞATMA (SMOOTHING) ---")]
    [Tooltip("Kameranın hedefe yetişme yumuşatma süresi (Düşük = daha sıkı takip, Yüksek = daha esnek)")]
    [SerializeField] private float smoothTime = 0.2f;

    [Header("--- AÇI VE ROTASYON ---")]
    [Tooltip("Aktifse kameranın rotasyonunu otomatik olarak hedefe veya izometrik açıya kilitler")]
    [SerializeField] private bool lockRotation = true;
    [Tooltip("Kameranın bakış açısı (Pitch: X ekseni eğimi, Yaw: Y ekseni dönüşü)")]
    [SerializeField] private Vector2 isometricAngle = new Vector2(40f, 45f);

    [Header("--- ZOOM / YAKINLAŞMA (OPSİYONEL) ---")]
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 2.0f;

    public static CameraFollow25D Instance { get; private set; }

    private Vector3 currentVelocity;
    private float currentZoomMultiplier = 1.0f;
    private float targetZoomMultiplier = 1.0f;
    private float defaultMinZoom = 0.5f;
    private float defaultMaxZoom = 2.0f;
    private bool isBuildModeZoomActive = false;

    // Oomph / Screen Shake & Zoom Kick değişkenleri
    private float shakeTimer = 0f;
    private float shakeDuration = 0.15f;
    private float shakeMagnitude = 0f;
    private float zoomKickOffset = 0f;
    private PlayerInteractionController interactionController;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void Awake()
    {
        Instance = this;
        defaultMinZoom = minZoom;
        defaultMaxZoom = maxZoom;
    }

    public void SetBuildModeZoom(bool active)
    {
        isBuildModeZoomActive = active;
        if (active)
        {
            // İnşa modunda geniş taktiksel bakış açısına geç
            minZoom = 0.75f;
            maxZoom = 2.75f;
            targetZoomMultiplier = 1.65f;
        }
        else
        {
            // İnşa modundan çıkıldığında normal kameraya toparla
            minZoom = defaultMinZoom;
            maxZoom = defaultMaxZoom;
            targetZoomMultiplier = 1.0f;
        }
    }

    private void Start()
    {
        if (target == null || !target.gameObject.scene.IsValid())
        {
            var player = FindFirstObjectByType<CharacterController25D>();
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) target = tagged.transform;
            }
        }

        if (target != null)
        {
            interactionController = target.GetComponent<PlayerInteractionController>();
            transform.position = target.position + offset;
            ApplyRotation();
        }

        // Binaların arkasında karakterin görünür kalması için Occlusion Fader'ı otomatik bağla
        if (GetComponent<CameraOcclusionFader>() == null)
        {
            gameObject.AddComponent<CameraOcclusionFader>();
        }
    }

    /// <summary>
    /// Kamera üzerinde anlık sarsıntı (Screen Shake) ve yakınlaşma vuruşu (Zoom Punch) tetikler.
    /// </summary>
    public void TriggerOomphShake(float duration, float magnitude, float zoomKick = 0f)
    {
        if (magnitude > shakeMagnitude || shakeTimer <= 0.02f)
        {
            shakeDuration = Mathf.Max(0.05f, duration);
            shakeTimer = shakeDuration;
            shakeMagnitude = magnitude;
        }
        zoomKickOffset = Mathf.Clamp(zoomKickOffset + zoomKick, -0.18f, 0.18f);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleZoom();

        // Vakum aktifken hafif odaklanma (Zoom-in) ve mikro-titreşim
        float vacuumZoomBias = 0f;
        Vector3 vacuumMicroJitter = Vector3.zero;
        if (interactionController != null && interactionController.IsVacuumActive)
        {
            vacuumZoomBias = -0.035f;
            float shakeSetting = GameAudioAndJuiceManager.Instance != null ? GameAudioAndJuiceManager.Instance.ScreenShakeIntensity : 1f;
            vacuumMicroJitter = Random.insideUnitSphere * (0.022f * shakeSetting);
        }

        currentZoomMultiplier = Mathf.Lerp(currentZoomMultiplier, targetZoomMultiplier, Time.deltaTime * 6.5f);
        zoomKickOffset = Mathf.Lerp(zoomKickOffset, 0f, Time.deltaTime * 11f);
        float effectiveZoom = Mathf.Clamp(currentZoomMultiplier + zoomKickOffset + vacuumZoomBias, minZoom * 0.85f, maxZoom * 1.1f);

        // Hedef pozisyon (offset * efektif zoom çarpanı ile)
        Vector3 targetPos = target.position + (offset * effectiveZoom);

        // Pürüzsüz takip
        Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, smoothTime);

        // Anlık Oomph Sarsıntısı (Screen Shake)
        Vector3 shakeOffset = vacuumMicroJitter;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float damper = Mathf.Clamp01(shakeTimer / Mathf.Max(0.01f, shakeDuration));
            shakeOffset += Random.insideUnitSphere * (shakeMagnitude * damper);
        }

        transform.position = smoothedPos + shakeOffset;

        if (lockRotation)
        {
            ApplyRotation();
        }
    }

    private void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(isometricAngle.x, isometricAngle.y, 0f);
    }

    private void HandleZoom()
    {
        if (!enableZoom) return;

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetZoomMultiplier -= Mathf.Sign(scroll) * 0.15f * zoomSpeed;
                targetZoomMultiplier = Mathf.Clamp(targetZoomMultiplier, minZoom, maxZoom);
            }
        }
#else
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoomMultiplier -= scroll * zoomSpeed;
            targetZoomMultiplier = Mathf.Clamp(targetZoomMultiplier, minZoom, maxZoom);
        }
#endif
    }

    private void OnValidate()
    {
        if (lockRotation)
        {
            ApplyRotation();
        }
    }
}
