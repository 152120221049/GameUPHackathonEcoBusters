using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Voxel Karakter Adım ve Prosedürel Animasyon Yöneticisi (Squash & Stretch).
/// - Hem oyuncu (CharacterController25D) hem de NPC takipçiler (FollowerNPC) ile tam uyumludur.
/// - Yürürken SADECE [İleri] ve [Geri] adımları arasında geçiş yapar (İleri -> Geri -> İleri -> Geri).
/// - Durduğunda (Idle) varsayılan duruş modelini (Idle.vox) gösterir.
/// - Adım atarken dikey sıçrama (Hop) ve yere basışta yaylanma (Squash & Stretch) uygular.
/// - MagicaVoxel modellerinin canvas ve pivot kaymalarını otomatik normalize eder (ayak tabanı Y=0, gövde merkezi X=0, Z=0).
/// - Dönüşlerde viraja yatar (Banking/Lean); düz koşarken titreme yapmaz.
/// - MagicaVoxel modelleri genellikle güneye baktığından 180 derece ofset ile ön yüzü hareket yönüne kilitler.
/// </summary>
public class VoxelCharacterAnimator : MonoBehaviour
{
    [Header("--- VOXEL MODELLERİ (.vox veya Sahne Nesnesi) ---")]
    [Tooltip("Varsayılan duruş modeli (Idle).")]
    public GameObject idleModel;

    [Tooltip("1. Adım karesi (İleri / Step1).")]
    public GameObject walkStep1Model;

    [Tooltip("2. Adım karesi (Geri / Step2).")]
    public GameObject walkStep2Model;

    [Tooltip("Vakum modeli (Vacuum.vox).")]
    public GameObject vacuumToolModel;

    [Tooltip("Çöp Toplama Çubuğu modeli (trashcollectionstick.vox).")]
    public GameObject trashStickModel;

    [Header("--- YÖN VE ROTASYON ---")]
    [Tooltip("MagicaVoxel modellerinin ön yüzünü Unity ileri yönüne (+Z) hizalamak için Y ekseni açısı (Varsayılan 180).")]
    [SerializeField] private float modelYRotationOffset = 180f;

    [Header("--- ANİMASYON VE ADIM HIZI ---")]
    [Tooltip("İki adım arasındaki geçiş süresi (saniye). 0.26 - 0.32 doğal bir voxel yürüyüş temposu verir.")]
    [SerializeField] private float stepInterval = 0.28f;

    [Header("--- PROSEDÜREL YAYLANMA (SQUASH & STRETCH) ---")]
    [Tooltip("Adım atarken dikey sıçrama yüksekliği")]
    [SerializeField] private float bounceHeight = 0.03f;
    [Tooltip("Yere basış esnasındaki basıklık/yaylanma oranı")]
    [SerializeField] private float squashAmount = 0.015f;
    [Tooltip("Dönüşlerde gövdenin viraj yönüne yatma açısı")]
    [SerializeField] private float leanAngle = 8f;
    [Tooltip("Dururken nefes alma hafif yaylanması")]
    [SerializeField] private float idleBobSpeed = 2.2f;
    [SerializeField] private float idleBobAmount = 0.012f;

    [Header("--- VAKUM TEPKİSİ (SADECE OYUNCU) ---")]
    [SerializeField] private float vacuumRecoilPitch = -5f;
    [SerializeField] private float vacuumVibrationIntensity = 0.012f;
    [Tooltip("Vakum sırt çantasının karakterin sırtında/omuzunda duracağı yerel ofset")]
    [SerializeField] private Vector3 vacuumHandOffset = new Vector3(-0.22f, 1.05f, 0.06f);
    [Tooltip("Vakumun yerel rotasyonu")]
    [SerializeField] private Vector3 vacuumLocalRotation = new Vector3(0f, 0f, 0f);
    [Tooltip("Vakum sırt çantasının karakterin sırtında her zaman görünür olup olmayacağı")]
    [SerializeField] private bool alwaysShowVacuumBackpack = false;

    [Header("--- ÇÖP TOPLAMA ÇUBUĞU (STICK) AYARLARI ---")]
    [Tooltip("Çöp çubuğunun yerel pozisyon ofseti (Elde doğru durması için sağa ve ele hizalandı)")]
    [SerializeField] private Vector3 stickHandOffset = new Vector3(-0.55f, 0.72f, -0.04f);
    [Tooltip("Çöp çubuğunun yerel rotasyonu")]
    [SerializeField] private Vector3 stickLocalRotation = new Vector3(48f, 15f, -12f);
    [Tooltip("Çöp çubuğunun ölçek çarpanı (Elde dolgun ve net görünmesi için 1.5 katı: 2.35f)")]
    [SerializeField] private float stickScale = 2.35f;

    [Header("--- HİYERARŞİ AYARI ---")]
    [SerializeField] private Transform visualContainer;

    private CharacterController25D controller;
    private FollowerNPC followerNPC;
    private CityCitizenNPC citizenNPC;
    private PlayerInteractionController interactionController;

    private GameObject activeIdleInstance;
    private GameObject activeStep1Instance;
    private GameObject activeStep2Instance;
    private GameObject activeVacuumInstance;
    private GameObject activeStickInstance;

    private float stepTimer;
    private int walkStepIndex = 0; // 0: İleri, 1: Geri (Yürürken sadece bu ikisi döner)
    private bool wasMovingLastFrame = false;
    private Vector3 initialContainerScale = Vector3.one;
    private Vector3 initialContainerPos = Vector3.zero;

    private void Awake()
    {
        controller = GetComponent<CharacterController25D>() ?? GetComponentInParent<CharacterController25D>();
        followerNPC = GetComponent<FollowerNPC>() ?? GetComponentInParent<FollowerNPC>();
        citizenNPC = GetComponent<CityCitizenNPC>() ?? GetComponentInParent<CityCitizenNPC>();
        interactionController = GetComponent<PlayerInteractionController>() ?? GetComponentInParent<PlayerInteractionController>();

        // Eski sahnelerden kalan hatalı vacuum offset veya stick offset'lerini düzelt
        if (vacuumHandOffset.x < -0.5f) vacuumHandOffset = new Vector3(-0.22f, 1.05f, 0.06f);
        if (stickScale < 2.0f) stickScale = 2.35f;
        alwaysShowVacuumBackpack = false;

        EnsureVisualContainer();
        HidePrimitiveRootMesh();
        InitializeModelInstances();

        if (visualContainer != null)
        {
            initialContainerScale = visualContainer.localScale;
            initialContainerPos = visualContainer.localPosition;
        }
    }

    private void OnValidate()
    {
        if (visualContainer != null)
        {
            if (!Application.isPlaying)
            {
                EnsureVisualContainer();
                HidePrimitiveRootMesh();
            }

            Transform vac = activeVacuumInstance != null ? activeVacuumInstance.transform : visualContainer.Find("Vacuum_Instance");
            if (vac != null)
            {
                vac.localPosition = vacuumHandOffset;
                vac.localRotation = Quaternion.Euler(vacuumLocalRotation);
                if (vac.gameObject.activeSelf != alwaysShowVacuumBackpack && !Application.isPlaying)
                {
                    vac.gameObject.SetActive(alwaysShowVacuumBackpack);
                }
            }

            Transform stick = activeStickInstance != null ? activeStickInstance.transform : visualContainer.Find("TrashStick_Instance");
            if (stick != null)
            {
                stick.localPosition = stickHandOffset;
                stick.localRotation = Quaternion.Euler(stickLocalRotation);
                stick.localScale = Vector3.one * stickScale;
            }
        }
    }

    private void Start()
    {
        if (citizenNPC == null) citizenNPC = GetComponent<CityCitizenNPC>() ?? GetComponentInParent<CityCitizenNPC>();
        if (followerNPC == null) followerNPC = GetComponent<FollowerNPC>() ?? GetComponentInParent<FollowerNPC>();

        EnsureVisualContainer();
        InitializeModelInstances();
        ShowOnlyModel(activeIdleInstance);
    }

    private void Update()
    {
        bool isMoving = false;
        float currentSpeed = 0f;
        Vector3 moveDir = Vector3.zero;

        if (controller != null)
        {
            isMoving = controller.IsMoving;
            currentSpeed = controller.CurrentSpeed;
            moveDir = controller.MoveDirection;
        }
        else if (followerNPC != null && followerNPC.enabled)
        {
            isMoving = followerNPC.IsWalking;
            currentSpeed = followerNPC.MoveSpeed;
            moveDir = followerNPC.MoveDirection;
        }
        else if (citizenNPC != null && citizenNPC.enabled)
        {
            isMoving = citizenNPC.IsWalking;
            currentSpeed = citizenNPC.WanderSpeed;
            moveDir = citizenNPC.MoveDirection;
        }

        bool isVacuuming = interactionController != null && interactionController.IsVacuumActive;

        if (isMoving)
        {
            if (!wasMovingLastFrame)
            {
                // Yürüyüş başlar başlamaz ilk adımı hemen göster (bekleme gecikmesini önler)
                stepTimer = 0f;
                walkStepIndex = 0;
                ShowOnlyModel(activeStep1Instance != null ? activeStep1Instance : activeIdleInstance);
            }
            UpdateWalkingAnimation(currentSpeed);
        }
        else
        {
            if (wasMovingLastFrame)
            {
                UpdateIdleAnimation();
            }
        }

        wasMovingLastFrame = isMoving;
        ApplyProceduralEffects(isMoving, currentSpeed, moveDir, isVacuuming);
    }

    /// <summary>
    /// Yürüyüş esnasında SADECE [İleri] ve [Geri] adımları dönüşümlü oynar.
    /// </summary>
    private void UpdateWalkingAnimation(float currentSpeed)
    {
        float speedNorm = currentSpeed > 0.1f ? (currentSpeed / 7f) : 1f;
        float currentInterval = Mathf.Clamp(stepInterval / Mathf.Max(0.5f, speedNorm), 0.18f, 0.40f);

        stepTimer += Time.deltaTime;
        if (stepTimer >= currentInterval)
        {
            stepTimer = 0f;
            walkStepIndex = (walkStepIndex + 1) % 2; // 0: İleri, 1: Geri

            if (walkStepIndex == 0)
            {
                ShowOnlyModel(activeStep1Instance != null ? activeStep1Instance : activeIdleInstance);
            }
            else
            {
                ShowOnlyModel(activeStep2Instance != null ? activeStep2Instance : activeIdleInstance);
            }

            // Yalnızca oyuncu karakteri adım attığında hafif adım sesi çal
            if (controller != null && GameAudioAndJuiceManager.Instance != null)
            {
                GameAudioAndJuiceManager.Instance.PlayFootstep();
            }
        }
    }

    /// <summary>
    /// Karakter durduğunda varsayılan duruş karesi (Idle) gösterilir.
    /// </summary>
    private void UpdateIdleAnimation()
    {
        stepTimer = 0f;
        walkStepIndex = 0;
        ShowOnlyModel(activeIdleInstance);
    }

    private void ApplyProceduralEffects(bool isMoving, float currentSpeed, Vector3 moveDir, bool isVacuuming)
    {
        if (visualContainer == null) return;

        Vector3 targetScale = initialContainerScale;
        Vector3 targetPos = initialContainerPos;
        Quaternion targetRot = Quaternion.Euler(0f, modelYRotationOffset, 0f);

        if (isMoving)
        {
            float speedNorm = currentSpeed > 0.1f ? (currentSpeed / 7f) : 1f;
            float currentInterval = Mathf.Clamp(stepInterval / Mathf.Max(0.5f, speedNorm), 0.18f, 0.40f);
            float stepProgress = Mathf.Clamp01(stepTimer / currentInterval);

            // 1. Dikey sıçrama (Hop)
            float bounce = Mathf.Sin(stepProgress * Mathf.PI) * bounceHeight;
            targetPos.y += bounce;

            // 2. Yere basış yaylanması (Squash - karakteri basıklaştırmayacak ölçüde hafif)
            float squash = (1f - Mathf.Sin(stepProgress * Mathf.PI)) * squashAmount;
            targetScale.y = Mathf.Clamp(targetScale.y - squash, 0.98f, 1.05f);
            targetScale.x = Mathf.Clamp(targetScale.x + squash * 0.2f, 0.95f, 1.05f);
            targetScale.z = Mathf.Clamp(targetScale.z + squash * 0.2f, 0.95f, 1.05f);

            // 3. Viraja yatma (Lean / Banking): Karakter yön değiştirirken viraja yatsın, düz koşarken titremesin!
            if (moveDir.sqrMagnitude > 0.01f)
            {
                float turnAngle = Vector3.SignedAngle(transform.forward, moveDir, Vector3.up);
                float lean = Mathf.Clamp(-turnAngle * 0.35f, -leanAngle, leanAngle);
                targetRot *= Quaternion.Euler(0f, 0f, lean);
            }
        }
        else
        {
            // Dururken nefes alma hafif yaylanması
            float breath = Mathf.Sin(Time.time * idleBobSpeed) * idleBobAmount;
            targetPos.y += breath;
            targetScale.y += breath * 0.5f;
        }

        // Vakum çalışırken hafif geriye yaslanma tepkisi ve motor titremesi (oyuncu için)
        if (isVacuuming)
        {
            targetRot *= Quaternion.Euler(vacuumRecoilPitch, 0f, 0f);
            float shakeX = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * vacuumVibrationIntensity;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * vacuumVibrationIntensity;
            targetPos += new Vector3(shakeX, shakeY, 0f);

            if (activeVacuumInstance != null)
            {
                if (!activeVacuumInstance.activeSelf) activeVacuumInstance.SetActive(true);
                // Vakum sırt çantasına ve hortumuna motor titreşimi ver
                activeVacuumInstance.transform.localPosition = vacuumHandOffset + new Vector3(shakeX * 1.5f, shakeY * 1.5f, 0f);
                activeVacuumInstance.transform.localRotation = Quaternion.Euler(vacuumLocalRotation);
            }
        }
        else
        {
            if (activeVacuumInstance != null)
            {
                if (alwaysShowVacuumBackpack)
                {
                    if (!activeVacuumInstance.activeSelf) activeVacuumInstance.SetActive(true);
                    activeVacuumInstance.transform.localPosition = vacuumHandOffset;
                    activeVacuumInstance.transform.localRotation = Quaternion.Euler(vacuumLocalRotation);
                }
                else if (activeVacuumInstance.activeSelf)
                {
                    activeVacuumInstance.SetActive(false);
                }
            }
        }

        // Pürüzsüz enterpolasyonla uygula
        visualContainer.localPosition = Vector3.Lerp(visualContainer.localPosition, targetPos, 22f * Time.deltaTime);
        visualContainer.localScale = Vector3.Lerp(visualContainer.localScale, targetScale, 22f * Time.deltaTime);
        visualContainer.localRotation = Quaternion.Slerp(visualContainer.localRotation, targetRot, 18f * Time.deltaTime);
    }

    private void ShowOnlyModel(GameObject targetModel)
    {
        if (targetModel == null) return;

        if (activeIdleInstance != null) activeIdleInstance.SetActive(activeIdleInstance == targetModel);
        if (activeStep1Instance != null) activeStep1Instance.SetActive(activeStep1Instance == targetModel);
        if (activeStep2Instance != null) activeStep2Instance.SetActive(activeStep2Instance == targetModel);
    }

    private void EnsureVisualContainer()
    {
        bool isPlayer = (controller != null || GetComponent<CharacterController25D>() != null || GetComponentInParent<CharacterController25D>() != null);
        float footOffsetY = -1.0f;

        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (!isPlayer)
        {
            if (cap != null)
            {
                // NPC kapsülünün alt tabanı Y=0 olacak şekilde ayarla
                if (cap.center.y < 0.4f)
                {
                    cap.height = 1.8f;
                    cap.center = new Vector3(0f, 0.9f, 0f);
                }
                footOffsetY = cap.center.y - (cap.height * 0.5f);
            }
            else
            {
                footOffsetY = 0f;
            }
        }
        else if (cap != null)
        {
            footOffsetY = cap.center.y - (cap.height * 0.5f);
        }

        if (visualContainer == null)
        {
            var child = transform.Find("VisualRoot");
            if (child != null)
            {
                visualContainer = child;
            }
            else
            {
                GameObject vrObj = new GameObject("VisualRoot");
                vrObj.transform.SetParent(transform, false);
                vrObj.transform.localPosition = new Vector3(0f, footOffsetY, 0f);
                vrObj.transform.localRotation = Quaternion.Euler(0f, modelYRotationOffset, 0f);
                vrObj.transform.localScale = Vector3.one;
                visualContainer = vrObj.transform;
            }
        }

        if (visualContainer != null)
        {
            // NPC prefablarında önceden -1.0f olarak kaydedilmiş VisualRoot kaymasını sıfırla (yarı beline kadar gömülmeyi çözer)
            Vector3 lp = visualContainer.localPosition;
            if (Mathf.Abs(lp.y - footOffsetY) > 0.05f)
            {
                lp.y = footOffsetY;
                visualContainer.localPosition = lp;
            }
            initialContainerPos = new Vector3(0f, footOffsetY, 0f);
        }
    }

    private void HidePrimitiveRootMesh()
    {
        MeshRenderer rend = GetComponent<MeshRenderer>();
        if (rend != null) rend.enabled = false;
    }

    /// <summary>
    /// Inspector'daki modelleri sahne hiyerarşisine bağlar.
    /// Modellerin MagicaVoxel kaynaklı canvas ve pivot kaymalarını otomatik düzeltir.
    /// </summary>
    public void InitializeModelInstances()
    {
        EnsureVisualContainer();

        #if UNITY_EDITOR
        // Eğer slotlar boşsa projeden otomatik bul (yalnızca oyuncu için untitled/ileri/geri)
        if (GetComponent<CharacterController25D>() != null && (idleModel == null || walkStep1Model == null || walkStep2Model == null))
        {
            AutoFindProjectVoxAssets();
        }
        #endif

        // Eski veya mükerrer çocuk nesneleri temizle
        if (visualContainer != null)
        {
            for (int i = visualContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = visualContainer.GetChild(i);
                if (child.name != "IdleModel_Instance" && child.name != "Step1_Instance" && child.name != "Step2_Instance" && child.name != "Vacuum_Instance" && child.name != "TrashStick_Instance")
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(child.gameObject);
                    else
                        Destroy(child.gameObject);
                    #else
                    Destroy(child.gameObject);
                    #endif
                }
            }
        }

#if UNITY_EDITOR
        if (trashStickModel == null) trashStickModel = FindVoxAssetByName("trashcollectionstick");
#endif

        activeIdleInstance = EnsureChildInstance(idleModel, "IdleModel_Instance");
        activeStep1Instance = EnsureChildInstance(walkStep1Model, "Step1_Instance");
        activeStep2Instance = EnsureChildInstance(walkStep2Model, "Step2_Instance");
        activeVacuumInstance = EnsureChildInstance(vacuumToolModel, "Vacuum_Instance");
        activeStickInstance = EnsureChildInstance(trashStickModel, "TrashStick_Instance");

        // Tüm modellerin mesh tabanını (ayak tabanı Y=0, merkez X=0, Z=0) otomatik hizala
        NormalizeModelPivot(activeIdleInstance);
        NormalizeModelPivot(activeStep1Instance);
        NormalizeModelPivot(activeStep2Instance);
        NormalizeStickPivot(activeStickInstance);

        if (activeVacuumInstance != null)
        {
            activeVacuumInstance.transform.localPosition = vacuumHandOffset;
            activeVacuumInstance.transform.localRotation = Quaternion.Euler(vacuumLocalRotation);
            activeVacuumInstance.SetActive(false); // Başlangıçta vakum sırt çantası kapalı (çubukla başlar)
        }

        if (activeStickInstance != null)
        {
            activeStickInstance.transform.localPosition = stickHandOffset;
            activeStickInstance.transform.localRotation = Quaternion.Euler(stickLocalRotation);
            activeStickInstance.transform.localScale = Vector3.one * stickScale;
            activeStickInstance.SetActive(true); // Başlangıçta çubuk açık ve elde belirgin görünür
        }

        // VisualRoot altındaki TÜM .vox modellerinin (Idle, Step1, Step2, Vacuum, Stick)
        // içbükey (concave) MeshCollider bileşenlerini kaldır.
        StripAllChildColliders();

        ShowOnlyModel(activeIdleInstance);
    }

    public void SetEquipmentVisualMode(bool hasVacuumUnlocked, bool isVacuumActive)
    {
        if (activeVacuumInstance != null)
        {
            activeVacuumInstance.SetActive(hasVacuumUnlocked);
        }
        if (activeStickInstance != null)
        {
            // Oyuncu vakum aletini açana kadar veya vakum çekmiyorken elinde çöp çubuğunu taşır
            activeStickInstance.SetActive(!hasVacuumUnlocked || !isVacuumActive);
        }
    }

    public void PlayStickSwingAnimation()
    {
        if (activeStickInstance != null && activeStickInstance.activeInHierarchy)
        {
            StopCoroutine("StickSwingRoutine");
            StartCoroutine("StickSwingRoutine");
        }
    }

    private System.Collections.IEnumerator StickSwingRoutine()
    {
        if (activeStickInstance == null) yield break;
        Vector3 basePos = stickHandOffset;
        Quaternion baseRot = Quaternion.Euler(stickLocalRotation);

        float dur = 0.18f;
        float el = 0f;
        while (el < dur && activeStickInstance != null)
        {
            el += Time.deltaTime;
            float t = Mathf.Sin(Mathf.Clamp01(el / dur) * Mathf.PI);
            activeStickInstance.transform.localPosition = basePos + new Vector3(0f, -0.18f * t, 0.28f * t);
            activeStickInstance.transform.localRotation = Quaternion.Euler(stickLocalRotation.x + 45f * t, stickLocalRotation.y, stickLocalRotation.z);
            yield return null;
        }
        if (activeStickInstance != null)
        {
            activeStickInstance.transform.localPosition = basePos;
            activeStickInstance.transform.localRotation = baseRot;
        }
    }

    public void StripAllChildColliders()
    {
        if (visualContainer == null) return;

        Collider[] childColliders = visualContainer.GetComponentsInChildren<Collider>(true);
        foreach (var col in childColliders)
        {
            if (col == null) continue;
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }
    }

    /// <summary>
    /// MagicaVoxel canvas boyutu veya pivotu farklı olan modellerin
    /// ayak tabanını tam olarak Y=0, gövde merkezini ise X=0 ve Z=0 konumuna oturtur.
    /// Böylece adım atarken ileri/geri ya da yukarı/aşağı ışınlanma tamamen yok edilir.
    /// </summary>
    private void NormalizeModelPivot(GameObject modelInstance)
    {
        if (modelInstance == null) return;

        MeshFilter mf = modelInstance.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            // Alt nesnenin lokal pozisyonunu mesh sınırlarının tersi yönünde ofsetle
            mf.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        }
    }

    /// <summary>
    /// Çöp çubuğunun sapını (üst tutma noktasını) tam elin merkezine (0,0,0) oturtur.
    /// </summary>
    private void NormalizeStickPivot(GameObject stickInstance)
    {
        if (stickInstance == null) return;
        MeshFilter mf = stickInstance.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            mf.transform.localPosition = new Vector3(-b.center.x, -b.max.y, -b.center.z);
        }
    }

    private GameObject EnsureChildInstance(GameObject sourcePrefabOrSceneObj, string childName)
    {
        if (sourcePrefabOrSceneObj == null || visualContainer == null) return null;

        // 1. Eğer zaten visualContainer'ın altında bir sahne nesnesiyse doğrudan kullan
        if (sourcePrefabOrSceneObj.scene.IsValid() && sourcePrefabOrSceneObj.transform.IsChildOf(visualContainer))
        {
            return sourcePrefabOrSceneObj;
        }

        // 2. VisualRoot altında daha önce üretilmiş bu isimde bir çocuk var mı?
        Transform existing = visualContainer.Find(childName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        // 3. Proje asset'i veya harici nesneyse instantiate et
        GameObject inst = Instantiate(sourcePrefabOrSceneObj, visualContainer);
        inst.name = childName;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        inst.SetActive(false);
        return inst;
    }

    #if UNITY_EDITOR
    [ContextMenu("Modelleri Otomatik Bul ve Kur")]
    public void AutoFindProjectVoxAssets()
    {
        if (idleModel == null) idleModel = FindVoxAssetByName("untitled");
        if (walkStep1Model == null) walkStep1Model = FindVoxAssetByName("leri");
        if (walkStep2Model == null) walkStep2Model = FindVoxAssetByName("geri");
        if (vacuumToolModel == null) vacuumToolModel = FindVoxAssetByName("vacuum");
        if (trashStickModel == null) trashStickModel = FindVoxAssetByName("trashcollectionstick");

        if (idleModel != null || walkStep1Model != null || walkStep2Model != null || vacuumToolModel != null)
        {
            EditorUtility.SetDirty(this);
        }
    }

    private static GameObject FindVoxAssetByName(string key)
    {
        string[] allGuids = AssetDatabase.FindAssets("t:GameObject");
        foreach (var guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".vox", System.StringComparison.OrdinalIgnoreCase))
            {
                string fn = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
                if (fn.Contains(key.ToLower()) ||
                    (key.ToLower() == "leri" && (fn.Contains("leri") || fn.Contains("ileri"))))
                {
                    var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (loaded != null) return loaded;
                }
            }
        }
        return null;
    }
    #endif
}
