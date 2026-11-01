using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Eko-Mucit Oyun İçi Arayüz Yöneticisi (Gameplay HUD).
/// - Ekranın üst kısmında Üs, Şehir Merkezi (TownHall), Fabrika (RecyclingBuilding), Çiftlik ve Nehir yönlerini gösteren Yön Pusulası Barı içerir.
/// - Sağ üstte [ESC] Ayarlar butonu ve oyun içinde her an açılabilen tam kapsamlı Ses, Grafik ve Oomph (Kamera Sarsıntısı) Ayarlar Menüsü içerir.
/// - Canlı çanta doluluk oranı, 3 Kademeli Ekonomi malzemeleri (Ham Madde -> Ara Ürün -> Eko-İcatlar) ve Ada Sağlığı / Ağaç sayılarını gösterir.
/// - Atölye menüsünde hem ara ürün/eko-icat üretimini [1,2,3,6,7,8] hem de ekipman yükseltmelerini [4,5] yönetir.
/// </summary>
public class GameplayHUD : MonoBehaviour
{
    [Header("--- SAHNEDEKİ UI REFERANSLARI ---")]
    [Tooltip("Sol üstteki Envanter / Çanta metni")]
    [SerializeField] private TextMeshProUGUI inventoryText;

    [Tooltip("Alt ortadaki etkileşim kutusu arka planı")]
    [SerializeField] private Image promptBoxBg;
    [Tooltip("Alt ortadaki etkileşim ve vakum ipucu metni")]
    [SerializeField] private TextMeshProUGUI promptText;

    [Tooltip("Üst ortadaki bildirim kutusu (Toast)")]
    [SerializeField] private GameObject notificationBox;
    [Tooltip("Üst ortadaki bildirim metni")]
    [SerializeField] private TextMeshProUGUI notificationText;

    [Tooltip("Hurdalık Atölyesi Üretim Paneli")]
    [SerializeField] private GameObject craftingPanel;

    [Header("--- YÖN PUSULASI BARI (OTOMATİK OLUŞTURULUR) ---")]
    [SerializeField] private RectTransform compassBarRect;
    [SerializeField] private TextMeshProUGUI compassText;

    [Header("--- OYUN İÇİ AYARLAR PANELİ (OTOMATİK OLUŞTURULUR) ---")]
    [SerializeField] private GameObject inGameSettingsPanel;
    private Slider masterVolSlider;
    private Slider musicVolSlider;
    private Slider sfxVolSlider;
    private Slider shakeSlider;
    private TextMeshProUGUI masterValText;
    private TextMeshProUGUI musicValText;
    private TextMeshProUGUI sfxValText;
    private TextMeshProUGUI shakeValText;
    private TextMeshProUGUI qualityBtnText;
    private TextMeshProUGUI fullscreenBtnText;

    [Header("--- ATÖLYE MALZEME VE YÜKSELTME METİNLERİ (OPSİYONEL) ---")]
    [SerializeField] private TextMeshProUGUI recipe1CostText;
    [SerializeField] private TextMeshProUGUI recipe2CostText;
    [SerializeField] private TextMeshProUGUI recipe3CostText;
    [SerializeField] private TextMeshProUGUI vacuumUpgradeCostText;
    [SerializeField] private TextMeshProUGUI backpackUpgradeCostText;
    [SerializeField] private TextMeshProUGUI extraRecipesFooterText;

    [Header("--- ATÖLYE VE GELİŞTİRME BUTONLARI ---")]
    [SerializeField] private Button btnCraftFilter;
    [SerializeField] private Button btnCraftCompost;
    [SerializeField] private Button btnCraftBuoy;
    [SerializeField] private Button btnUpgradeVacuum;
    [SerializeField] private Button btnUpgradeBackpack;

    private PlayerInventory inventory;
    private PlayerInteractionController interactionController;
    private PlayerProgression progression;
    private ProceduralTerrainGenerator terrainGenerator;
    private Transform playerTransform;

    private float notificationTimer;
    private float compassUpdateTimer;
    private bool isCraftingOpen;
    private bool isSettingsOpen;
    private bool isInventoryOpen = false;
    private bool isGuideOpen = false;
    private bool isMinimapOpen = false;
    private bool isTutorialOpen = true;

    private GameObject compactHudBarObj;
    private TextMeshProUGUI compactHudText;
    private GameObject detailedInventoryPanelObj;
    private GameObject questGuidePanelObj;
    private GameObject welcomeTutorialModalObj;
    private GameObject minimapPanelObj;
    private RectTransform minimapMapAreaRect;
    private RectTransform minimapPlayerMarker;
    private TextMeshProUGUI minimapInfoText;
    private float minimapUpdateTimer;

    private void Awake()
    {
        EnsureEventSystemForUI();
    }

    private void Start()
    {
        inventory = PlayerInventory.Instance != null ? PlayerInventory.Instance : FindFirstObjectByType<PlayerInventory>();
        interactionController = FindFirstObjectByType<PlayerInteractionController>();
        progression = PlayerProgression.Instance != null ? PlayerProgression.Instance : FindFirstObjectByType<PlayerProgression>();
        terrainGenerator = FindFirstObjectByType<ProceduralTerrainGenerator>();

        if (interactionController != null)
            playerTransform = interactionController.transform;

        EnsureCompassBar();
        EnsureExtraCraftingFooter();
        EnsureInGameSettingsMenu();
        EnsureCompactHudAndModals();
        EnsureMinimapPanel();
        EnsureWelcomeTutorialModal();
        DecorateCraftingCardsWithVisuals();

        // Buton tıklamalarını çalışma anında (Runtime) güvenle bağla
        BindButton(ref btnCraftFilter, "Btn_Craft1", CraftCarbonFilter);
        BindButton(ref btnCraftCompost, "Btn_Craft2", CraftSuperCompost);
        BindButton(ref btnCraftBuoy, "Btn_Craft3", CraftRiverBuoy);
        BindButton(ref btnUpgradeVacuum, "Btn_UpgradeVac", UpgradeVacuum);
        BindButton(ref btnUpgradeBackpack, "Btn_UpgradeBag", UpgradeBackpack);

        if (inventory != null)
        {
            inventory.OnInventoryChanged += OnInventoryUpdated;
            inventory.OnCoinsChanged += OnCoinsUpdated;
            inventory.OnInventoryNotification += ShowNotification;
        }

        if (progression != null)
        {
            progression.OnProgressionUpdated += RefreshDisplay;
        }

        if (EcoSystemManager.Instance != null)
        {
            EcoSystemManager.Instance.OnEcoScoreChanged += OnEcoUpdated;
            EcoSystemManager.Instance.OnReputationChanged += OnEcoUpdated;
        }

        WorkbenchInteractable.OnWorkbenchToggled += ToggleCraftingMenu;

        if (craftingPanel != null)
        {
            craftingPanel.SetActive(false);
        }

        if (notificationBox != null)
        {
            notificationBox.SetActive(false);
        }

        RefreshDisplay();
        UpdateCompassBar();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;

        if (inventory != null)
        {
            inventory.OnInventoryChanged -= OnInventoryUpdated;
            inventory.OnCoinsChanged -= OnCoinsUpdated;
            inventory.OnInventoryNotification -= ShowNotification;
        }

        if (progression != null)
        {
            progression.OnProgressionUpdated -= RefreshDisplay;
        }

        if (EcoSystemManager.Instance != null)
        {
            EcoSystemManager.Instance.OnEcoScoreChanged -= OnEcoUpdated;
            EcoSystemManager.Instance.OnReputationChanged -= OnEcoUpdated;
        }

        WorkbenchInteractable.OnWorkbenchToggled -= ToggleCraftingMenu;
    }

    private void Update()
    {
        HandleSettingsHotkey();
        UpdateInteractionPrompt();
        UpdateNotificationTimer();
        HandleCraftingHotkeys();

        compassUpdateTimer -= Time.unscaledDeltaTime;
        if (compassUpdateTimer <= 0f)
        {
            compassUpdateTimer = 0.15f;
            UpdateCompassBar();
        }

        if (isMinimapOpen)
        {
            minimapUpdateTimer -= Time.unscaledDeltaTime;
            if (minimapUpdateTimer <= 0f)
            {
                minimapUpdateTimer = 0.08f;
                UpdateMinimap();
            }
        }
    }

    private void EnsureEventSystemForUI()
    {
#if ENABLE_INPUT_SYSTEM
        var es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            es = esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            var standalone = es.GetComponent<StandaloneInputModule>();
            if (standalone != null) DestroyImmediate(standalone);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
#endif
    }

    /// <summary>
    /// Oyun içinde [ESC] veya [P] tuşuyla ya da sağ üstteki [ESC] AYARLAR butonuyla açılan
    /// Ses (Master, Müzik, SFX), Kamera Oomph Sarsıntısı, Grafik Kalitesi ve Tam Ekran menüsünü kurar.
    /// </summary>
    private void EnsureInGameSettingsMenu()
    {
        if (inGameSettingsPanel != null) return;

        // 1. Sağ üst köşeye hızlı [ESC] AYARLAR & SES butonu ekle
        Transform existingBtn = transform.Find("TopRightSettingsBtn");
        if (existingBtn == null)
        {
            GameObject topBtnObj = new GameObject("TopRightSettingsBtn");
            topBtnObj.transform.SetParent(transform, false);
            RectTransform rt = topBtnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-18f, -14f);
            rt.sizeDelta = new Vector2(175f, 44f);

            Image bg = topBtnObj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.18f, 0.26f, 0.92f);

            Outline outline = topBtnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.85f, 1f, 0.7f);

            Button btn = topBtnObj.AddComponent<Button>();
            btn.onClick.AddListener(ToggleSettingsMenu);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(topBtnObj.transform, false);
            RectTransform txtRt = txtObj.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "<b>[ESC] AYARLAR</b>";
            tmp.fontSize = 15f;
            tmp.color = new Color(0.9f, 0.96f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            if (inventoryText != null && inventoryText.font != null) tmp.font = inventoryText.font;
        }

        // 2. Oyun İçi Ayarlar Modal Paneli
        GameObject panelObj = new GameObject("InGameSettingsPanel");
        panelObj.transform.SetParent(transform, false);
        inGameSettingsPanel = panelObj;

        RectTransform panelRt = panelObj.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(680f, 560f);

        Image panelBg = panelObj.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.09f, 0.14f, 0.97f);

        Outline panelOutline = panelObj.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.3f, 0.9f, 0.65f, 0.85f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        CreateMenuLabel(panelObj.transform, "Title", "<b>AYARLAR & SES KONTROLÜ</b>", 28f, new Vector2(0f, 235f), new Color(0.35f, 1f, 0.65f));
        CreateMenuLabel(panelObj.transform, "SubTitle", "Ses seviyelerini, Oomph/Sarsıntı şiddetini ve ekran ayarlarını anında değiştirebilirsin.", 14.5f, new Vector2(0f, 198f), new Color(0.75f, 0.85f, 0.95f));

        float masterVol = GameAudioAndJuiceManager.Instance != null ? GameAudioAndJuiceManager.Instance.MasterVolume : PlayerPrefs.GetFloat("MasterVol", 0.85f);
        float musicVol = GameAudioAndJuiceManager.Instance != null ? GameAudioAndJuiceManager.Instance.MusicVolume : PlayerPrefs.GetFloat("MusicVol", 0.55f);
        float sfxVol = GameAudioAndJuiceManager.Instance != null ? GameAudioAndJuiceManager.Instance.SfxVolume : PlayerPrefs.GetFloat("SfxVol", 0.90f);
        float shakeVal = GameAudioAndJuiceManager.Instance != null ? GameAudioAndJuiceManager.Instance.ScreenShakeIntensity : PlayerPrefs.GetFloat("ShakeIntensity", 1.0f);

        masterVolSlider = CreateInteractiveSliderRow(panelObj.transform, "Genel Ses (Master):", 140f, 0f, 1f, masterVol, out masterValText, OnMasterSliderChanged);
        musicVolSlider = CreateInteractiveSliderRow(panelObj.transform, "Ambiyans Müziği:", 85f, 0f, 1f, musicVol, out musicValText, OnMusicSliderChanged);
        sfxVolSlider = CreateInteractiveSliderRow(panelObj.transform, "Ses Efektleri (SFX):", 30f, 0f, 1f, sfxVol, out sfxValText, OnSfxSliderChanged);
        shakeSlider = CreateInteractiveSliderRow(panelObj.transform, "Oomph / Kamera Sarsıntısı:", -25f, 0f, 2f, shakeVal, out shakeValText, OnShakeSliderChanged);

        // Grafik Kalitesi & Tam Ekran Butonları
        Button qualityBtn = CreateActionBtn(panelObj.transform, "Btn_CycleQuality", "", new Vector2(-155f, -95f), new Vector2(280f, 46f), new Color(0.18f, 0.32f, 0.48f), CycleQualityLevel);
        qualityBtnText = qualityBtn.GetComponentInChildren<TextMeshProUGUI>();

        Button fsBtn = CreateActionBtn(panelObj.transform, "Btn_ToggleFS", "", new Vector2(155f, -95f), new Vector2(280f, 46f), new Color(0.18f, 0.32f, 0.48f), ToggleFullscreenMode);
        fullscreenBtnText = fsBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Alt Aksiyon Butonları
        CreateActionBtn(panelObj.transform, "Btn_TestOomph", "★ SES & OOMPH TEST ET", new Vector2(0f, -160f), new Vector2(340f, 44f), new Color(0.55f, 0.38f, 0.14f), TestOomphButton);
        CreateActionBtn(panelObj.transform, "Btn_Resume", "OYUNA DEVAM ET [ESC]", new Vector2(-155f, -225f), new Vector2(280f, 50f), new Color(0.16f, 0.62f, 0.34f), () => SetSettingsMenuOpen(false));
        CreateActionBtn(panelObj.transform, "Btn_QuitOrMenu", "ANA MENÜ / ÇIKIŞ", new Vector2(155f, -225f), new Vector2(280f, 50f), new Color(0.65f, 0.22f, 0.22f), ReturnToMainMenuOrQuit);

        RefreshSettingsUIValues();
        inGameSettingsPanel.SetActive(false);
    }

    private TextMeshProUGUI CreateMenuLabel(Transform parent, string name, string text, float fontSize, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(620f, 40f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (inventoryText != null && inventoryText.font != null) tmp.font = inventoryText.font;
        return tmp;
    }

    private Slider CreateInteractiveSliderRow(Transform parent, string label, float yPos, float min, float max, float initialVal, out TextMeshProUGUI valueLabel, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = new GameObject($"Row_{label}");
        row.transform.SetParent(parent, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchoredPosition = new Vector2(0f, yPos);
        rowRt.sizeDelta = new Vector2(600f, 40f);

        // Sol Etiket
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(row.transform, false);
        RectTransform lblRt = lblObj.AddComponent<RectTransform>();
        lblRt.anchoredPosition = new Vector2(-175f, 0f);
        lblRt.sizeDelta = new Vector2(240f, 36f);
        TextMeshProUGUI lblTmp = lblObj.AddComponent<TextMeshProUGUI>();
        lblTmp.text = label;
        lblTmp.fontSize = 17f;
        lblTmp.color = Color.white;
        lblTmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (inventoryText != null && inventoryText.font != null) lblTmp.font = inventoryText.font;

        // Sağ Yüzde Değeri
        GameObject valObj = new GameObject("ValueText");
        valObj.transform.SetParent(row.transform, false);
        RectTransform valRt = valObj.AddComponent<RectTransform>();
        valRt.anchoredPosition = new Vector2(255f, 0f);
        valRt.sizeDelta = new Vector2(80f, 36f);
        valueLabel = valObj.AddComponent<TextMeshProUGUI>();
        valueLabel.fontSize = 17f;
        valueLabel.color = new Color(0.35f, 1f, 0.65f);
        valueLabel.alignment = TextAlignmentOptions.MidlineRight;
        if (inventoryText != null && inventoryText.font != null) valueLabel.font = inventoryText.font;

        // Slider Gövdesi
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        RectTransform sRt = sliderObj.AddComponent<RectTransform>();
        sRt.anchoredPosition = new Vector2(65f, 0f);
        sRt.sizeDelta = new Vector2(260f, 24f);

        Image bgImg = sliderObj.AddComponent<Image>();
        bgImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.2f);
        faRt.anchorMax = new Vector2(1f, 0.8f);
        faRt.offsetMin = new Vector2(6f, 0f);
        faRt.offsetMax = new Vector2(-6f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fRt = fill.AddComponent<RectTransform>();
        fRt.sizeDelta = Vector2.zero;
        Image fImg = fill.AddComponent<Image>();
        fImg.color = new Color(0.25f, 0.88f, 0.55f, 1f);

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform haRt = handleArea.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform hRt = handle.AddComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(20f, 28f);
        Image hImg = handle.AddComponent<Image>();
        hImg.color = new Color(0.95f, 0.98f, 1f, 1f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fRt;
        slider.handleRect = hRt;
        slider.targetGraphic = hImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(initialVal);
        slider.onValueChanged.AddListener(onChanged);

        return slider;
    }

    private Button CreateActionBtn(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = $"<b>{text}</b>";
        tmp.fontSize = 16.5f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (inventoryText != null && inventoryText.font != null) tmp.font = inventoryText.font;

        return btn;
    }

    private void HandleSettingsHotkey()
    {
        bool tabPressed = false;
        bool hPressed = false;
        bool mPressed = false;
        bool escPressed = false;
        bool pPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) tabPressed = true;
            if (kb.hKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame) hPressed = true;
            if (kb.mKey.wasPressedThisFrame) mPressed = true;
            if (kb.escapeKey.wasPressedThisFrame) escPressed = true;
            if (kb.pKey.wasPressedThisFrame) pPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I)) tabPressed = true;
        if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1)) hPressed = true;
        if (Input.GetKeyDown(KeyCode.M)) mPressed = true;
        if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true;
        if (Input.GetKeyDown(KeyCode.P)) pPressed = true;
#endif

        if (isTutorialOpen)
        {
            bool closeTutorial = false;
#if ENABLE_INPUT_SYSTEM
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame)
                {
                    closeTutorial = true;
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H))
            {
                closeTutorial = true;
            }
#endif
            if (closeTutorial)
            {
                SetTutorialModalOpen(false);
                return;
            }
        }

        if (tabPressed)
        {
            ToggleInventoryWindow();
            return;
        }

        if (hPressed)
        {
            ToggleTutorialModal();
            return;
        }

        if (mPressed)
        {
            ToggleMinimapWindow();
            return;
        }

        if (pPressed)
        {
            ToggleSettingsMenu();
            return;
        }

        if (escPressed)
        {
            if (isTutorialOpen)
            {
                SetTutorialModalOpen(false);
            }
            else if (isSettingsOpen)
            {
                SetSettingsMenuOpen(false);
            }
            else if (isCraftingOpen)
            {
                SetCraftingMenuOpen(false);
            }
            else if (isMinimapOpen)
            {
                SetMinimapWindowOpen(false);
            }
            else if (isInventoryOpen)
            {
                SetInventoryWindowOpen(false);
            }
            else if (isGuideOpen)
            {
                SetGuideWindowOpen(false);
            }
            else if (BuildingPlacementSystem.Instance == null || !BuildingPlacementSystem.Instance.IsBuildModeActive)
            {
                SetSettingsMenuOpen(true);
            }
        }
    }

    public void ToggleInventoryWindow()
    {
        SetInventoryWindowOpen(!isInventoryOpen);
    }

    public void SetInventoryWindowOpen(bool open)
    {
        isInventoryOpen = open;
        GameAudioAndJuiceManager.Instance?.PlayUiClick();

        if (detailedInventoryPanelObj != null)
        {
            detailedInventoryPanelObj.SetActive(isInventoryOpen);
        }
        else if (inventoryText != null && inventoryText.transform.parent != null)
        {
            detailedInventoryPanelObj = inventoryText.transform.parent.gameObject;
            detailedInventoryPanelObj.SetActive(isInventoryOpen);
        }

        if (isInventoryOpen && isGuideOpen)
        {
            SetGuideWindowOpen(false);
        }

        RefreshDisplay();
    }

    public void ToggleGuideWindow()
    {
        ToggleTutorialModal();
    }

    public void SetGuideWindowOpen(bool open)
    {
        SetTutorialModalOpen(open);
    }

    public void ToggleTutorialModal()
    {
        SetTutorialModalOpen(!isTutorialOpen);
    }

    public void SetTutorialModalOpen(bool open)
    {
        EnsureWelcomeTutorialModal();
        isTutorialOpen = open;
        GameAudioAndJuiceManager.Instance?.PlayUiClick();

        if (welcomeTutorialModalObj != null)
        {
            welcomeTutorialModalObj.SetActive(isTutorialOpen);
        }

        if (questGuidePanelObj != null)
        {
            questGuidePanelObj.SetActive(false); // Tam ekran modal varken eski küçük kutuyu kapalı tut
        }

        if (isTutorialOpen)
        {
            if (isInventoryOpen) SetInventoryWindowOpen(false);
            if (isMinimapOpen) SetMinimapWindowOpen(false);
            if (isSettingsOpen) SetSettingsMenuOpen(false);
        }
    }

    public void ToggleMinimapWindow()
    {
        SetMinimapWindowOpen(!isMinimapOpen);
    }

    public void SetMinimapWindowOpen(bool open)
    {
        EnsureMinimapPanel();
        isMinimapOpen = open;
        GameAudioAndJuiceManager.Instance?.PlayUiClick();

        if (minimapPanelObj != null)
        {
            minimapPanelObj.SetActive(isMinimapOpen);
        }

        if (isMinimapOpen)
        {
            SetInventoryWindowOpen(false);
            SetGuideWindowOpen(false);
            UpdateMinimap();
        }
    }

    private void EnsureMinimapPanel()
    {
        if (minimapPanelObj != null) return;

        GameObject panel = new GameObject("MinimapModalPanel");
        panel.transform.SetParent(transform, false);
        minimapPanelObj = panel;

        RectTransform panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(640f, 640f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.09f, 0.14f, 0.97f);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.85f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateMenuLabel(panel.transform, "Title", "<b>[M] ADA RADARI & DÜNYA HARİTASI</b>", 24f, new Vector2(0f, 278f), new Color(0.4f, 0.9f, 1f));

        // [X] Kapatma Butonu
        GameObject closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(panel.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(34f, 34f);

        Image cBg = closeObj.AddComponent<Image>();
        cBg.color = new Color(0.85f, 0.22f, 0.22f, 0.9f);

        Button cBtn = closeObj.AddComponent<Button>();
        cBtn.onClick.AddListener(() => SetMinimapWindowOpen(false));

        GameObject cTxt = new GameObject("X");
        cTxt.transform.SetParent(closeObj.transform, false);
        RectTransform ctRt = cTxt.AddComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI cTmp = cTxt.AddComponent<TextMeshProUGUI>();
        cTmp.text = "<b>X</b>";
        cTmp.fontSize = 18f;
        cTmp.alignment = TextAlignmentOptions.Center;
        cTmp.color = Color.white;

        // Radar Harita Ekranı (Grid Alanı)
        GameObject mapArea = new GameObject("MapRadarArea");
        mapArea.transform.SetParent(panel.transform, false);
        minimapMapAreaRect = mapArea.AddComponent<RectTransform>();
        minimapMapAreaRect.anchorMin = new Vector2(0.5f, 0.5f);
        minimapMapAreaRect.anchorMax = new Vector2(0.5f, 0.5f);
        minimapMapAreaRect.pivot = new Vector2(0.5f, 0.5f);
        minimapMapAreaRect.anchoredPosition = new Vector2(0f, 22f);
        minimapMapAreaRect.sizeDelta = new Vector2(460f, 460f);

        Image mapBg = mapArea.AddComponent<Image>();
        mapBg.color = new Color(0.04f, 0.07f, 0.11f, 1f);

        Outline mapOutline = mapArea.AddComponent<Outline>();
        mapOutline.effectColor = new Color(0.2f, 0.45f, 0.65f, 0.6f);

        // Radar Mesafe Halkaları (50m, 100m, 150m)
        CreateRadarCircle(mapArea.transform, 140f, new Color(0.2f, 0.6f, 0.8f, 0.18f));
        CreateRadarCircle(mapArea.transform, 280f, new Color(0.2f, 0.6f, 0.8f, 0.18f));
        CreateRadarCircle(mapArea.transform, 420f, new Color(0.2f, 0.6f, 0.8f, 0.25f));

        // Sabit Bölge İkonları (Üs, Şehir, Fabrika, Çiftlik, Nehir)
        CreateRadarPin(mapArea.transform, "Pin_Base", "🏠 Üs", new Color(0.4f, 0.9f, 1f));
        CreateRadarPin(mapArea.transform, "Pin_City", "🏛 Şehir", new Color(1f, 0.84f, 0f));
        CreateRadarPin(mapArea.transform, "Pin_Factory", "🏭 Fabrika", new Color(1f, 0.47f, 0.77f));
        CreateRadarPin(mapArea.transform, "Pin_Farm", "🌾 Çiftlik", new Color(0.35f, 0.95f, 0.5f));
        CreateRadarPin(mapArea.transform, "Pin_River", "🌊 Nehir", new Color(0.25f, 0.88f, 0.82f));

        // Oyuncu İkonu (Marker)
        GameObject playerMarkerObj = new GameObject("PlayerMarker");
        playerMarkerObj.transform.SetParent(mapArea.transform, false);
        minimapPlayerMarker = playerMarkerObj.AddComponent<RectTransform>();
        minimapPlayerMarker.sizeDelta = new Vector2(32f, 32f);

        TextMeshProUGUI pTmp = playerMarkerObj.AddComponent<TextMeshProUGUI>();
        pTmp.text = "<b><color=#50FA7B>▲</color></b>";
        pTmp.fontSize = 28f;
        pTmp.alignment = TextAlignmentOptions.Center;

        // Alt Bilgi & Mesafe Paneli
        GameObject infoObj = new GameObject("MinimapInfoText");
        infoObj.transform.SetParent(panel.transform, false);
        RectTransform infoRt = infoObj.AddComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0.5f, 0f);
        infoRt.anchorMax = new Vector2(0.5f, 0f);
        infoRt.pivot = new Vector2(0.5f, 0f);
        infoRt.anchoredPosition = new Vector2(0f, 14f);
        infoRt.sizeDelta = new Vector2(600f, 62f);

        minimapInfoText = infoObj.AddComponent<TextMeshProUGUI>();
        minimapInfoText.fontSize = 14f;
        minimapInfoText.alignment = TextAlignmentOptions.Center;
        minimapInfoText.color = new Color(0.85f, 0.92f, 0.98f);
        minimapInfoText.richText = true;

        panel.SetActive(false);
    }

    private void CreateRadarCircle(Transform parent, float size, Color col)
    {
        GameObject circle = new GameObject("RadarCircle");
        circle.transform.SetParent(parent, false);
        RectTransform rt = circle.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        Image img = circle.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        Outline outline = circle.AddComponent<Outline>();
        outline.effectColor = col;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void CreateRadarPin(Transform parent, string pinName, string label, Color col)
    {
        GameObject pin = new GameObject(pinName);
        pin.transform.SetParent(parent, false);
        RectTransform rt = pin.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 26f);

        TextMeshProUGUI tmp = pin.AddComponent<TextMeshProUGUI>();
        tmp.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(col)}>{label}</color></b>";
        tmp.fontSize = 13f;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private void UpdateMinimap()
    {
        if (terrainGenerator == null) terrainGenerator = FindFirstObjectByType<ProceduralTerrainGenerator>();
        if (playerTransform == null && interactionController != null) playerTransform = interactionController.transform;
        if (playerTransform == null || terrainGenerator == null || minimapMapAreaRect == null) return;

        float mapSpan = Mathf.Max(terrainGenerator.MapWidth, terrainGenerator.MapHeight) * terrainGenerator.TileSize;
        if (mapSpan <= 10f) mapSpan = 200f;

        float radarRadius = 215f; // 460px genişliğinde radar alanının yarıçapı
        Vector3 playerPos = playerTransform.position;

        // Oyuncu marker'ını harita merkezine veya dünya ofsetine göre konumlandır
        float normX = Mathf.Clamp(playerPos.x / (mapSpan * 0.5f), -1f, 1f);
        float normZ = Mathf.Clamp(playerPos.z / (mapSpan * 0.5f), -1f, 1f);
        if (minimapPlayerMarker != null)
        {
            minimapPlayerMarker.anchoredPosition = new Vector2(normX * radarRadius, normZ * radarRadius);
            minimapPlayerMarker.localRotation = Quaternion.Euler(0f, 0f, -playerTransform.eulerAngles.y);
        }

        // Sabit Pinleri Konumlandır
        PositionRadarPin("Pin_Base", terrainGenerator.BaseWorldPosition, mapSpan, radarRadius);
        PositionRadarPin("Pin_City", terrainGenerator.CityCenterWorldPosition, mapSpan, radarRadius);
        PositionRadarPin("Pin_Factory", terrainGenerator.FactoryHubWorldPosition, mapSpan, radarRadius);
        PositionRadarPin("Pin_Farm", terrainGenerator.FarmCenterWorldPosition, mapSpan, radarRadius);
        PositionRadarPin("Pin_River", terrainGenerator.RiverCenterWorldPosition, mapSpan, radarRadius);

        // Mesafe Bilgi Metni
        int dBase = Mathf.RoundToInt(Vector3.Distance(playerPos, terrainGenerator.BaseWorldPosition));
        int dCity = Mathf.RoundToInt(Vector3.Distance(playerPos, terrainGenerator.CityCenterWorldPosition));
        int dFac = Mathf.RoundToInt(Vector3.Distance(playerPos, terrainGenerator.FactoryHubWorldPosition));
        int dFarm = Mathf.RoundToInt(Vector3.Distance(playerPos, terrainGenerator.FarmCenterWorldPosition));

        if (minimapInfoText != null)
        {
            minimapInfoText.text =
                $"<b><color=#50FA7B>Konum: ({Mathf.RoundToInt(playerPos.x)}, {Mathf.RoundToInt(playerPos.z)})</color></b>   |   " +
                $"<color=#40E0D0>🏠 Üs: {dBase}m</color>   |   " +
                $"<color=#FFD700>🏛 Şehir: {dCity}m</color>   |   " +
                $"<color=#FF79C6>🏭 Fabrika: {dFac}m</color>   |   " +
                $"<color=#50FA7B>🌾 Çiftlik: {dFarm}m</color>\n" +
                "<size=12><color=#A0AAB2>[M] veya [ESC] ile Haritayı Kapat</color></size>";
        }
    }

    private void PositionRadarPin(string pinName, Vector3 worldPos, float mapSpan, float radarRadius)
    {
        if (minimapMapAreaRect == null) return;
        Transform pin = minimapMapAreaRect.Find(pinName);
        if (pin == null) return;

        float nx = Mathf.Clamp(worldPos.x / (mapSpan * 0.5f), -1f, 1f);
        float nz = Mathf.Clamp(worldPos.z / (mapSpan * 0.5f), -1f, 1f);
        RectTransform rt = pin.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(nx * radarRadius, nz * radarRadius);
        }
    }

    /// <summary>
    /// Oyuna ilk girişte açılan ve [H] tuşuyla her an erişilebilen
    /// Adanın hikayesini, hedefini ve kontrol rehberini açıklayan şık bilgilendirme modalı.
    /// </summary>
    private void EnsureWelcomeTutorialModal()
    {
        if (welcomeTutorialModalObj != null) return;

        Transform existing = transform.Find("WelcomeTutorialModal");
        if (existing != null)
        {
            welcomeTutorialModalObj = existing.gameObject;
            welcomeTutorialModalObj.SetActive(isTutorialOpen);
            return;
        }

        GameObject modal = new GameObject("WelcomeTutorialModal");
        modal.transform.SetParent(transform, false);
        welcomeTutorialModalObj = modal;

        RectTransform rt = modal.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(780f, 540f);

        Image bg = modal.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.09f, 0.14f, 0.97f);

        Outline outline = modal.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.85f, 0.65f, 0.90f);
        outline.effectDistance = new Vector2(3f, -3f);

        // 1. Başlık
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(modal.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -16f);
        titleRt.sizeDelta = new Vector2(740f, 44f);

        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "<b><size=22><color=#FFE66D>🌱 EKO-MUCİT: ADAYI KURTARMA HEDEFİ & REHBER 🌱</color></size></b>";
        titleTmp.alignment = TextAlignmentOptions.Center;
        if (inventoryText != null && inventoryText.font != null) titleTmp.font = inventoryText.font;

        // 2. Ana Açıklama / Hedef ve Öğretici Metni
        GameObject contentObj = new GameObject("ContentText");
        contentObj.transform.SetParent(modal.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = new Vector2(0f, 15f);
        contentRt.sizeDelta = new Vector2(730f, 385f);

        TextMeshProUGUI contentTmp = contentObj.AddComponent<TextMeshProUGUI>();
        contentTmp.text =
            "<b><size=16><color=#50FA7B>[🌍 ADANIN DURUMU VE NİHAİ HEDEFİN]</color></size></b>\n" +
            "Terk edilmiş ve ağır atıklarla kirlenmiş bir ada ekosistemindesin.\n" +
            "Nihai Görevin: <b>Ada Sağlığını %25'ten %100'e çıkararak</b> adaya temiz doğayı ve hayatı geri kazandırmak!\n\n" +

            "<b><size=15><color=#8BE9FD>[🧹 1. ADIM: ATIKLARI TOPLA VE DÖNÜŞTÜR]</color></size></b>\n" +
            "• Elindeki <b>Çöp Çubuğu</b> ile çevredeki atık öbeklerini <b>[Sol Tık / E]</b> ile topla.\n" +
            "• Üssündeki <b>Hurdalık Atölyesi</b>'nde atıkları <i>Çelik Plaka</i> ve <i>Polimer Blok</i> ara ürünlerine presle.\n\n" +

            "<b><size=15><color=#FFB86C>[⚡ 2. ADIM: VAKUM CİHAZI VE OTOMASYON]</color></size></b>\n" +
            "• Yeterli ara ürün ürettiğinde Atölyeden <b>Vakum Cihazını</b> aç ve <b>[Sağ Tık / Space]</b> ile alan vakumu yap!\n" +
            "• <b>[B]</b> ile <b>Geri Dönüşüm Presi</b>, <b>Konveyör Bant</b> ve <b>Sandık</b> kurarak tam otomasyona geç.\n\n" +

            "<b><size=15><color=#FF79C6>[🏭 3. ADIM: BÖLGESEL KURTARMA & TEMİZ ENERJİ]</color></size></b>\n" +
            "• <b>Fabrika:</b> Bacalara <i>Baca Filtresi</i> tak, zehirli dumanı durdur ve Sanayi Merkezini aktif et!\n" +
            "• <b>Çiftlik:</b> Arızalı traktörleri <i>Tamir Kiti</i> ile onar, tarlaları ekime aç ve ekin yetiştir!\n" +
            "• <b>Nehir:</b> Suya <i>Temizleme Dubası</i> kurarak suyu berraklaştır ve balıkları geri getir!\n" +
            "• <b>Temiz Enerji:</b> Ağaç dik, Güneş Paneli ve Rüzgar Türbini ile enerji üretip halkın itibarını kazan!\n\n" +

            "<b><size=14.5><color=#F1FA8C>[🎮 KONTROLLER]</color></size></b>\n" +
            "<b>[W,A,S,D]:</b> Hareket   |   <b>[Sol Tık / E]:</b> Çubukla Topla   |   <b>[Sağ Tık / Space]:</b> Vakum Çekimi\n" +
            "<b>[B]:</b> İnşa Modu (Scroll ile Yakınlaş)   |   <b>[Tab / I]:</b> Envanter   |   <b>[M]:</b> Harita   |   <b>[H]:</b> Bu Rehberi Aç";

        contentTmp.fontSize = 14f;
        contentTmp.color = new Color(0.92f, 0.95f, 1f);
        contentTmp.alignment = TextAlignmentOptions.TopLeft;
        contentTmp.richText = true;
        if (inventoryText != null && inventoryText.font != null) contentTmp.font = inventoryText.font;

        // 3. Alt Kısımda Büyük Parlak "GÖREVE BAŞLA" Butonu
        GameObject startBtnObj = new GameObject("StartGameBtn");
        startBtnObj.transform.SetParent(modal.transform, false);
        RectTransform startRt = startBtnObj.AddComponent<RectTransform>();
        startRt.anchorMin = new Vector2(0.5f, 0f);
        startRt.anchorMax = new Vector2(0.5f, 0f);
        startRt.pivot = new Vector2(0.5f, 0f);
        startRt.anchoredPosition = new Vector2(0f, 18f);
        startRt.sizeDelta = new Vector2(360f, 48f);

        Image startBg = startBtnObj.AddComponent<Image>();
        startBg.color = new Color(0.18f, 0.75f, 0.45f, 0.95f);

        Outline startOutline = startBtnObj.AddComponent<Outline>();
        startOutline.effectColor = new Color(0.6f, 1f, 0.8f);

        Button startBtn = startBtnObj.AddComponent<Button>();
        startBtn.targetGraphic = startBg;
        startBtn.onClick.AddListener(() => SetTutorialModalOpen(false));

        GameObject startTxtObj = new GameObject("Text");
        startTxtObj.transform.SetParent(startBtnObj.transform, false);
        RectTransform stRt = startTxtObj.AddComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI startTmp = startTxtObj.AddComponent<TextMeshProUGUI>();
        startTmp.text = "<b><size=17><color=#081210>GÖREVE BAŞLA [Space / E / Tıkla]</color></size></b>";
        startTmp.alignment = TextAlignmentOptions.Center;
        if (inventoryText != null && inventoryText.font != null) startTmp.font = inventoryText.font;

        // 4. Sağ Üst Köşe [X] Kapatma Butonu
        GameObject closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(modal.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(34f, 34f);

        Image cBg = closeObj.AddComponent<Image>();
        cBg.color = new Color(0.85f, 0.22f, 0.22f, 0.90f);

        Button cBtn = closeObj.AddComponent<Button>();
        cBtn.onClick.AddListener(() => SetTutorialModalOpen(false));

        GameObject cTxtObj = new GameObject("X");
        cTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform ctRt = cTxtObj.AddComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI cTmp = cTxtObj.AddComponent<TextMeshProUGUI>();
        cTmp.text = "<b>X</b>";
        cTmp.fontSize = 18f;
        cTmp.alignment = TextAlignmentOptions.Center;
        cTmp.color = Color.white;
        if (inventoryText != null && inventoryText.font != null) cTmp.font = inventoryText.font;

        modal.SetActive(isTutorialOpen);
    }

    public void ToggleSettingsMenu()
    {
        SetSettingsMenuOpen(!isSettingsOpen);
    }

    public void SetSettingsMenuOpen(bool open)
    {
        EnsureInGameSettingsMenu();
        isSettingsOpen = open;

        if (isSettingsOpen && isCraftingOpen)
        {
            isCraftingOpen = false;
            if (craftingPanel != null) craftingPanel.SetActive(false);
        }

        if (inGameSettingsPanel != null)
        {
            inGameSettingsPanel.SetActive(isSettingsOpen);
        }

        Time.timeScale = isSettingsOpen ? 0f : 1f;
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        if (isSettingsOpen)
        {
            RefreshSettingsUIValues();
        }
    }

    private void RefreshSettingsUIValues()
    {
        var mgr = GameAudioAndJuiceManager.Instance;
        float master = mgr != null ? mgr.MasterVolume : AudioListener.volume;
        float music = mgr != null ? mgr.MusicVolume : 0.55f;
        float sfx = mgr != null ? mgr.SfxVolume : 0.90f;
        float shake = mgr != null ? mgr.ScreenShakeIntensity : 1.0f;

        if (masterVolSlider != null) masterVolSlider.SetValueWithoutNotify(master);
        if (musicVolSlider != null) musicVolSlider.SetValueWithoutNotify(music);
        if (sfxVolSlider != null) sfxVolSlider.SetValueWithoutNotify(sfx);
        if (shakeSlider != null) shakeSlider.SetValueWithoutNotify(shake);

        if (masterValText != null) masterValText.text = $"%{Mathf.RoundToInt(master * 100f)}";
        if (musicValText != null) musicValText.text = $"%{Mathf.RoundToInt(music * 100f)}";
        if (sfxValText != null) sfxValText.text = $"%{Mathf.RoundToInt(sfx * 100f)}";
        if (shakeValText != null) shakeValText.text = $"%{Mathf.RoundToInt(shake * 100f)}";

        if (qualityBtnText != null)
        {
            string[] qNames = QualitySettings.names;
            int qIdx = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, qNames.Length - 1));
            string qName = (qNames != null && qNames.Length > 0) ? qNames[qIdx] : "Standart";
            qualityBtnText.text = $"<b>Grafik Kalitesi: <color=#8BE9FD>{qName}</color></b>";
        }

        if (fullscreenBtnText != null)
        {
            string fsState = Screen.fullScreen ? "<color=#50FA7B>AÇIK</color>" : "<color=#FFAA00>KAPALI</color>";
            fullscreenBtnText.text = $"<b>Tam Ekran: {fsState}</b>";
        }
    }

    private void OnMasterSliderChanged(float val)
    {
        GameAudioAndJuiceManager.Instance?.SetMasterVolume(val);
        if (masterValText != null) masterValText.text = $"%{Mathf.RoundToInt(val * 100f)}";
    }

    private void OnMusicSliderChanged(float val)
    {
        GameAudioAndJuiceManager.Instance?.SetMusicVolume(val);
        if (musicValText != null) musicValText.text = $"%{Mathf.RoundToInt(val * 100f)}";
    }

    private void OnSfxSliderChanged(float val)
    {
        GameAudioAndJuiceManager.Instance?.SetSfxVolume(val);
        if (sfxValText != null) sfxValText.text = $"%{Mathf.RoundToInt(val * 100f)}";
    }

    private void OnShakeSliderChanged(float val)
    {
        GameAudioAndJuiceManager.Instance?.SetScreenShakeIntensity(val);
        if (shakeValText != null) shakeValText.text = $"%{Mathf.RoundToInt(val * 100f)}";
    }

    private void CycleQualityLevel()
    {
        int count = QualitySettings.names.Length;
        if (count > 0)
        {
            int next = (QualitySettings.GetQualityLevel() + 1) % count;
            QualitySettings.SetQualityLevel(next);
            PlayerPrefs.SetInt("QualitySetting", next);
            PlayerPrefs.Save();
        }
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        RefreshSettingsUIValues();
    }

    private void ToggleFullscreenMode()
    {
        bool next = !Screen.fullScreen;
        Screen.fullScreen = next;
        PlayerPrefs.SetInt("FullscreenSetting", next ? 1 : 0);
        PlayerPrefs.Save();
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        RefreshSettingsUIValues();
    }

    private void TestOomphButton()
    {
        if (GameAudioAndJuiceManager.Instance != null)
        {
            Vector3 pos = playerTransform != null ? playerTransform.position : Vector3.zero;
            GameAudioAndJuiceManager.Instance.TriggerPurifyOrRepairOomph(pos, "OOMPH & SES AKTİF!", new Color(0.35f, 1f, 0.65f), playerTransform);
        }
        ShowNotification("Ses Sentezleyici, Voxel Patlaması ve Kamera Oomph Test Edildi!");
    }

    private void ReturnToMainMenuOrQuit()
    {
        Time.timeScale = 1f;
        GameAudioAndJuiceManager.Instance?.PlayUiClick();

        if (Application.CanStreamedLevelBeLoaded("MainMenu"))
        {
            SceneManager.LoadScene("MainMenu");
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// Ekranın üst kısmına oyuncunun Üs, Şehir Merkezi (TownHall), Fabrika (RecyclingBuilding),
    /// Çiftlik ve Nehir yönlerini kolayca bulmasını sağlayan şık bir Pusula / Yön Gösterge Barı kurar.
    /// </summary>
    private void EnsureCompassBar()
    {
        if (compassText != null) return;

        Transform existing = transform.Find("TopCompassBar");
        if (existing != null)
        {
            compassBarRect = existing.GetComponent<RectTransform>();
            compassText = existing.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        GameObject barObj = new GameObject("TopCompassBar");
        barObj.transform.SetParent(transform, false);

        compassBarRect = barObj.AddComponent<RectTransform>();
        compassBarRect.anchorMin = new Vector2(0.5f, 1f);
        compassBarRect.anchorMax = new Vector2(0.5f, 1f);
        compassBarRect.pivot = new Vector2(0.5f, 1f);
        compassBarRect.anchoredPosition = new Vector2(10f, -14f);
        compassBarRect.sizeDelta = new Vector2(920f, 44f);

        Image bg = barObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.09f, 0.13f, 0.88f);

        Outline outline = barObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.85f, 0.65f, 0.6f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject txtObj = new GameObject("CompassText");
        txtObj.transform.SetParent(barObj.transform, false);

        RectTransform txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(12f, 4f);
        txtRect.offsetMax = new Vector2(-12f, -4f);

        compassText = txtObj.AddComponent<TextMeshProUGUI>();
        compassText.fontSize = 14.5f;
        compassText.alignment = TextAlignmentOptions.Center;
        compassText.color = Color.white;
        compassText.richText = true;
        if (inventoryText != null && inventoryText.font != null)
        {
            compassText.font = inventoryText.font;
        }
    }

    private void EnsureExtraCraftingFooter()
    {
        if (craftingPanel == null || extraRecipesFooterText != null) return;

        Transform existing = craftingPanel.transform.Find("ExtraRecipesFooter");
        if (existing != null)
        {
            extraRecipesFooterText = existing.GetComponent<TextMeshProUGUI>();
            return;
        }

        GameObject footerObj = new GameObject("ExtraRecipesFooter");
        footerObj.transform.SetParent(craftingPanel.transform, false);

        RectTransform rt = footerObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 12f);
        rt.sizeDelta = new Vector2(760f, 54f);

        extraRecipesFooterText = footerObj.AddComponent<TextMeshProUGUI>();
        extraRecipesFooterText.fontSize = 14.5f;
        extraRecipesFooterText.alignment = TextAlignmentOptions.Center;
        extraRecipesFooterText.color = new Color(0.9f, 0.95f, 1f);
        extraRecipesFooterText.richText = true;
        if (inventoryText != null && inventoryText.font != null)
        {
            extraRecipesFooterText.font = inventoryText.font;
        }
    }

    private void EnsureCompactHudAndModals()
    {
        // 1. Ekranın sol üstüne minimalist, derli toplu mini durum kapsülü (Compact HUD) ekle
        if (compactHudBarObj == null)
        {
            Transform existing = transform.Find("CompactHudBar");
            if (existing != null)
            {
                compactHudBarObj = existing.gameObject;
                compactHudText = existing.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                GameObject bar = new GameObject("CompactHudBar");
                bar.transform.SetParent(transform, false);
                compactHudBarObj = bar;

                RectTransform rt = bar.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(22f, -14f);
                rt.sizeDelta = new Vector2(670f, 44f);

                Image bg = bar.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.12f, 0.17f, 0.94f);

                Outline outline = bar.AddComponent<Outline>();
                outline.effectColor = new Color(0.35f, 0.85f, 0.65f, 0.65f);

                Button btn = bar.AddComponent<Button>();
                btn.onClick.AddListener(ToggleInventoryWindow);

                GameObject txtObj = new GameObject("Text");
                txtObj.transform.SetParent(bar.transform, false);
                RectTransform txtRt = txtObj.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.sizeDelta = new Vector2(-16f, 0f);

                compactHudText = txtObj.AddComponent<TextMeshProUGUI>();
                compactHudText.fontSize = 15f;
                compactHudText.alignment = TextAlignmentOptions.Center;
                compactHudText.color = Color.white;
                compactHudText.richText = true;
                if (inventoryText != null && inventoryText.font != null) compactHudText.font = inventoryText.font;
            }
        }

        // 2. Detaylı Envanter Panelini (Sol Taraf) varsayılan olarak kapalı tut
        if (inventoryText != null && inventoryText.transform.parent != null)
        {
            detailedInventoryPanelObj = inventoryText.transform.parent.gameObject;
            detailedInventoryPanelObj.SetActive(isInventoryOpen);

            // Envanter paneline [X] kapatma butonu ekle
            if (detailedInventoryPanelObj.transform.Find("CloseBtn") == null)
            {
                GameObject closeObj = new GameObject("CloseBtn");
                closeObj.transform.SetParent(detailedInventoryPanelObj.transform, false);
                RectTransform closeRt = closeObj.AddComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(1f, 1f);
                closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                closeRt.anchoredPosition = new Vector2(-8f, -8f);
                closeRt.sizeDelta = new Vector2(30f, 30f);

                Image cBg = closeObj.AddComponent<Image>();
                cBg.color = new Color(0.8f, 0.2f, 0.2f, 0.85f);

                Button cBtn = closeObj.AddComponent<Button>();
                cBtn.onClick.AddListener(() => SetInventoryWindowOpen(false));

                GameObject cTxtObj = new GameObject("X");
                cTxtObj.transform.SetParent(closeObj.transform, false);
                RectTransform ctRt = cTxtObj.AddComponent<RectTransform>();
                ctRt.anchorMin = Vector2.zero;
                ctRt.anchorMax = Vector2.one;
                ctRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI cTmp = cTxtObj.AddComponent<TextMeshProUGUI>();
                cTmp.text = "<b>X</b>";
                cTmp.fontSize = 16f;
                cTmp.alignment = TextAlignmentOptions.Center;
                cTmp.color = Color.white;
            }
        }

        // 3. Görev ve Kontroller Rehberi Panelini (Sağ Taraf) bul ve varsayılan olarak kapalı tut
        Transform qp = transform.Find("QuestPanel");
        if (qp != null)
        {
            questGuidePanelObj = qp.gameObject;
            questGuidePanelObj.SetActive(isGuideOpen);

            // Görev rehberine zenginleştirilmiş kontrol bilgisi ve [X] butonu ekle
            TextMeshProUGUI qText = qp.GetComponentInChildren<TextMeshProUGUI>();
            if (qText != null)
            {
                qText.text =
                    "<b><size=18><color=#FFE66D>[GÖREV VE KONTROL REHBERİ]</color></size></b>\n" +
                    "<b>1.</b> Çevredeki çöp yığınlarından [Sol Tık / E] ile atık topla.\n" +
                    "<b>2.</b> <b>[B]</b> ile İnşa Modunu aç: Pres, Sandık, Güneş/Rüzgar paneli kur.\n" +
                    "<b>3.</b> Atölyede <b>[E]</b> ile Vakumunu aç ve geliştir.\n" +
                    "<b>4.</b> Filtre ve Duba üreterek ekosistemi temizle!\n\n" +
                    "<b><size=16><color=#8BE9FD>[KONTROLLER]</color></size></b>\n" +
                    "<b>[W,A,S,D]:</b> Hareket   |   <b>[Sol Tık/E]:</b> Çubukla Topla\n" +
                    "<b>[Sağ Tık/Space]:</b> Vakum Çekimi (Açılınca)\n" +
                    "<b>[B]:</b> İnşa Modu (Scroll ile Kamera Yakınlaşma)\n" +
                    "<b>[Tab]:</b> Detaylı Envanter   |   <b>[H]:</b> Rehber\n" +
                    "<b>[M]:</b> Ada Radarı / Dünya Haritası\n" +
                    "<b>[ESC]:</b> Ayarlar ve Ses Menüsü";
                qText.rectTransform.sizeDelta = new Vector2(340f, 275f);
            }

            RectTransform qpRt = qp.GetComponent<RectTransform>();
            if (qpRt != null)
            {
                qpRt.sizeDelta = new Vector2(380f, 290f);
            }

            if (qp.Find("CloseBtn") == null)
            {
                GameObject closeObj = new GameObject("CloseBtn");
                closeObj.transform.SetParent(qp, false);
                RectTransform closeRt = closeObj.AddComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(1f, 1f);
                closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                closeRt.anchoredPosition = new Vector2(-8f, -8f);
                closeRt.sizeDelta = new Vector2(30f, 30f);

                Image cBg = closeObj.AddComponent<Image>();
                cBg.color = new Color(0.8f, 0.2f, 0.2f, 0.85f);

                Button cBtn = closeObj.AddComponent<Button>();
                cBtn.onClick.AddListener(() => SetGuideWindowOpen(false));

                GameObject cTxtObj = new GameObject("X");
                cTxtObj.transform.SetParent(closeObj.transform, false);
                RectTransform ctRt = cTxtObj.AddComponent<RectTransform>();
                ctRt.anchorMin = Vector2.zero;
                ctRt.anchorMax = Vector2.one;
                ctRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI cTmp = cTxtObj.AddComponent<TextMeshProUGUI>();
                cTmp.text = "<b>X</b>";
                cTmp.fontSize = 16f;
                cTmp.alignment = TextAlignmentOptions.Center;
                cTmp.color = Color.white;
            }
        }
    }

    private void DecorateCraftingCardsWithVisuals()
    {
        if (craftingPanel == null) return;

        // Kart 1: Baca Filtresi Görsel Rozeti
        DecorateSingleCraftCard(craftingPanel.transform.Find("Card1_Filter"), "🏭", "Baca Filtresi", new Color(0.4f, 0.85f, 1f));
        // Kart 2: Süper Kompost Görsel Rozeti
        DecorateSingleCraftCard(craftingPanel.transform.Find("Card2_Compost"), "🌱", "Süper Kompost", new Color(0.35f, 0.95f, 0.5f));
        // Kart 3: Temizleme Dubası Görsel Rozeti
        DecorateSingleCraftCard(craftingPanel.transform.Find("Card3_Buoy"), "🌊", "Temizleme Dubası", new Color(0.3f, 0.9f, 0.95f));
        // Kart 4: Vakum Geliştirme Görsel Rozeti
        DecorateSingleCraftCard(craftingPanel.transform.Find("Card4_VacUpgrade"), "🌀", "Siklonik Vakum", new Color(1f, 0.85f, 0.35f));
        // Kart 5: Sırt Çantası Geliştirme Görsel Rozeti
        DecorateSingleCraftCard(craftingPanel.transform.Find("Card5_BagUpgrade"), "🎒", "Eko-Sırt Çantası", new Color(1f, 0.65f, 0.35f));
    }

    private void DecorateSingleCraftCard(Transform cardTr, string iconSymbol, string modelBadgeTitle, Color themeColor)
    {
        if (cardTr == null) return;
        Transform existing = cardTr.Find("ItemModelBadge");
        if (existing != null) return;

        GameObject badgeObj = new GameObject("ItemModelBadge");
        badgeObj.transform.SetParent(cardTr, false);

        RectTransform rt = badgeObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -22f);
        rt.sizeDelta = new Vector2(240f, 36f);

        Image bg = badgeObj.AddComponent<Image>();
        bg.color = new Color(themeColor.r * 0.25f, themeColor.g * 0.25f, themeColor.b * 0.25f, 0.85f);

        Outline outl = badgeObj.AddComponent<Outline>();
        outl.effectColor = themeColor;

        GameObject txtObj = new GameObject("BadgeText");
        txtObj.transform.SetParent(badgeObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(themeColor)}>{iconSymbol} [{modelBadgeTitle}]</color></b>";
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private void UpdateCompassBar()
    {
        if (compassText == null) return;

        if (playerTransform == null)
        {
            var ic = FindFirstObjectByType<PlayerInteractionController>();
            if (ic != null) playerTransform = ic.transform;
        }
        if (terrainGenerator == null)
        {
            terrainGenerator = FindFirstObjectByType<ProceduralTerrainGenerator>();
        }
        if (playerTransform == null || terrainGenerator == null) return;

        Vector3 p = playerTransform.position;

        string baseStr = FormatCompassTarget("ÜS", "#8BE9FD", p, terrainGenerator.BaseWorldPosition);
        string cityStr = FormatCompassTarget("ŞEHİR (Belediye)", "#FFD700", p, terrainGenerator.CityCenterWorldPosition);
        string facStr = FormatCompassTarget("FABRİKA (Tesis)", "#FF79C6", p, terrainGenerator.FactoryHubWorldPosition);
        string farmStr = FormatCompassTarget("ÇİFTLİK", "#50FA7B", p, terrainGenerator.FarmCenterWorldPosition);
        string riverStr = FormatCompassTarget("NEHİR", "#40E0D0", p, terrainGenerator.RiverCenterWorldPosition);

        compassText.text = $"<b><color=#BD93F9>[PUSULA]</color></b>   {baseStr}   |   {cityStr}   |   {facStr}   |   {farmStr}   |   {riverStr}";
    }

    private string FormatCompassTarget(string label, string hexColor, Vector3 playerPos, Vector3 targetPos)
    {
        Vector3 delta = targetPos - playerPos;
        delta.y = 0f;
        int dist = Mathf.RoundToInt(delta.magnitude);

        if (dist <= 8)
        {
            return $"<color={hexColor}><b>● {label} (Buradasın)</b></color>";
        }

        string arrow = GetIsoCompassArrow(delta);
        return $"<color={hexColor}><b>{arrow} {label}</b></color> <color=#D8DEE9>{dist}m</color>";
    }

    private string GetIsoCompassArrow(Vector3 worldDelta)
    {
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;
        if (Camera.main != null)
        {
            camForward = Camera.main.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude > 0.001f) camForward.Normalize();

            camRight = Camera.main.transform.right;
            camRight.y = 0f;
            if (camRight.sqrMagnitude > 0.001f) camRight.Normalize();
        }

        float x = Vector3.Dot(worldDelta.normalized, camRight);
        float y = Vector3.Dot(worldDelta.normalized, camForward);
        float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;

        if (angle >= -22.5f && angle < 22.5f) return "►";
        if (angle >= 22.5f && angle < 67.5f) return "↗";
        if (angle >= 67.5f && angle < 112.5f) return "▲";
        if (angle >= 112.5f && angle < 157.5f) return "↖";
        if (angle >= -67.5f && angle < -22.5f) return "↘";
        if (angle >= -112.5f && angle < -67.5f) return "▼";
        if (angle >= -157.5f && angle < -112.5f) return "↙";
        return "◄";
    }

    private void OnInventoryUpdated(ItemType type, int newAmount)
    {
        RefreshDisplay();
    }

    private void OnCoinsUpdated(int newAmount)
    {
        RefreshDisplay();
    }

    private void OnEcoUpdated(float val)
    {
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        if (inventory == null) inventory = PlayerInventory.Instance != null ? PlayerInventory.Instance : FindFirstObjectByType<PlayerInventory>();
        if (progression == null) progression = PlayerProgression.Instance != null ? PlayerProgression.Instance : FindFirstObjectByType<PlayerProgression>();
        if (inventory == null) return;

        // 1. Kademe Ham Maddeler
        int scrap = inventory.GetCount(ItemType.ScrapMetal);
        int plastic = inventory.GetCount(ItemType.PlasticWaste);
        int organic = inventory.GetCount(ItemType.OrganicWaste);
        int tire = inventory.GetCount(ItemType.RubberTire);
        int elec = inventory.GetCount(ItemType.ElectronicWaste);

        // 2. Kademe Ara Ürünler
        int steel = inventory.GetCount(ItemType.SteelPlate);
        int polymer = inventory.GetCount(ItemType.PolymerBlock);
        int compost = inventory.GetCount(ItemType.SuperCompost);

        // 3. Kademe İleri Seviye Eko-İcatlar
        int repairKit = inventory.GetCount(ItemType.RepairKit);
        int filter = inventory.GetCount(ItemType.CarbonFilter);
        int buoy = inventory.GetCount(ItemType.RiverCleanerBuoy);
        int cleanGen = inventory.GetCount(ItemType.CleanEnergyGenerator);

        int totalWaste = inventory.TotalWasteCount;
        int capacity = inventory.BackpackCapacity;
        int coins = inventory.Coins;

        float ecoScore = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.GlobalEcoScore : 20f;
        float rep = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CommunityReputation : 10f;
        int followers = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.ActiveFollowersCount : 0;
        int trees = TreePlantation.ActiveTreeCount;

        string capColor = totalWaste >= capacity ? "#FF5555" : (totalWaste >= capacity - 3 ? "#FFAA00" : "#50FA7B");
        string vacTitle = progression != null ? progression.GetVacuumTitle() : "Kilitli (Çöp Çubuğu)";
        string bagTitle = progression != null ? progression.GetBackpackTitle() : "Standart Çanta";

        if (compactHudText != null)
        {
            compactHudText.text =
                $"<b><color=#FFD700>🎒 {totalWaste}/{capacity}</color>   |   <color=#50FA7B>🪙 {coins} Kredi</color>   |   <color=#8BE9FD>🌱 Ada: %{Mathf.RoundToInt(ecoScore)}</color>   |   <color=#FFAA00>[Tab: Çanta]</color> <color=#8BE9FD>[H: Hedef & Rehber]</color> <color=#BD93F9>[M: Harita]</color></b>";
        }

        if (inventoryText != null)
        {
            inventoryText.text =
                $"<b><size=19><color=#FFD700>[ÇANTA] {bagTitle.ToUpper()}</color></size></b> <color={capColor}><b>({totalWaste}/{capacity})</b></color>\n" +
                $"<color=#FFD700>Bakiye: <b>{coins}</b> Eko-Kredi</color>\n" +
                $"<color=#C87D55>[*]</color> Hurda: <b>{scrap}</b>  |  <color=#5BC0EB>Plastik:</color> <b>{plastic}</b>  |  <color=#9BC53D>Biyo:</color> <b>{organic}</b>\n" +
                $"<color=#A0A0A0>[*]</color> Lastik: <b>{tire}</b>  |  <color=#50FA7B>Elektronik:</color> <b>{elec}</b>\n\n" +
                "<b><size=18><color=#8BE9FD>[ARA ÜRÜNLER & EKO-İCATLAR]</color></size></b>\n" +
                $"<color=#D8E2EF>[*]</color> Çelik Plaka: <b>{steel}</b>  |  <color=#59E1FF>Polimer Blok:</color> <b>{polymer}</b>\n" +
                $"<color=#4EBA6F>[*]</color> Süper Kompost: <b>{compost}</b>  |  <color=#FF8C42>Tamir Kiti:</color> <b>{repairKit}</b>\n" +
                $"<color=#A0AAB2>[*]</color> Baca Filtresi: <b>{filter}</b>  |  <color=#FDE74C>Duba:</color> <b>{buoy}</b>\n" +
                $"<color=#66FFE0>[*]</color> Temiz Enerji Jeneratörü: <color=#50FA7B><b>{cleanGen}</b></color>\n\n" +
                "<b><size=17><color=#50FA7B>[ADA EKOSİSTEMİ]</color></size></b>\n" +
                $"<color=#50FA7B>[*]</color> Ada Sağlığı: <b>%{Mathf.RoundToInt(ecoScore)}</b>  |  Ağaçlar: <b>{trees}</b>\n" +
                $"<color=#8BE9FD>[*]</color> İtibar: <b>{Mathf.RoundToInt(rep)}</b>  |  Takipçi: <b>{followers}</b>\n" +
                $"<size=15><color=#8BE9FD>[ARACIN] {vacTitle}</color></size>";
        }

        // Atölye tarif maliyetlerini güncelle (3 Kat Dengelenmiş)
        if (recipe1CostText != null)
        {
            bool canAdv = steel >= 3 && polymer >= 3;
            bool canRaw = scrap >= 9;
            string col = (canAdv || canRaw) ? "#50FA7B" : "#FF5555";
            recipe1CostText.text = $"<color={col}>3 Çelik+3 Polimer (veya 9 Hurda)</color>";
        }

        if (recipe2CostText != null)
        {
            string col = organic >= 9 ? "#50FA7B" : "#FF5555";
            recipe2CostText.text = $"Gereken: <color={col}>{organic}/9 Biyo-Hammadde</color>";
        }

        if (recipe3CostText != null)
        {
            bool canAdv = polymer >= 3 && steel >= 3;
            bool canRaw = plastic >= 6 && scrap >= 6;
            string col = (canAdv || canRaw) ? "#50FA7B" : "#FF5555";
            recipe3CostText.text = $"<color={col}>3 Polimer+3 Çelik (veya 6 Plastik+6 Hurda)</color>";
        }

        if (extraRecipesFooterText != null)
        {
            string bootsCost = progression != null ? progression.GetBootsCostInfo() : "8 Lastik+80 Kredi";
            extraRecipesFooterText.text =
                "<color=#FFD700><b>[EK İCATLAR & YÜKSELTME KISAYOLLARI]</b></color>  " +
                "<b>[6] Tamir Kiti</b> (3 Çelik+3 Polimer)   |   " +
                "<b>[7] Presle</b> (4 Hurda/Plastik)   |   " +
                "<b>[8] Temiz Jeneratör</b> (6 Çelik+6 Polimer)   |   " +
                $"<b>[9] Eko-Bot</b> (<color=#50FA7B>{bootsCost}</color>)";
        }

        if (vacuumUpgradeCostText != null && progression != null)
        {
            string cost = progression.GetVacuumCostInfo();
            bool canAfford = progression.CanUpgradeVacuum();
            string col = canAfford ? "#50FA7B" : (progression.VacuumLevel >= 3 ? "#8BE9FD" : "#FF5555");
            vacuumUpgradeCostText.text = $"<color={col}>{cost}</color>";
        }

        if (backpackUpgradeCostText != null && progression != null)
        {
            string cost = progression.GetBackpackCostInfo();
            bool canAfford = progression.CanUpgradeBackpack();
            string col = canAfford ? "#50FA7B" : (progression.BackpackLevel >= 3 ? "#8BE9FD" : "#FF5555");
            backpackUpgradeCostText.text = $"<color={col}>{cost}</color>";
        }
    }

    private void UpdateInteractionPrompt()
    {
        if (promptText == null) return;
        if (interactionController == null)
            interactionController = FindFirstObjectByType<PlayerInteractionController>();

        if (interactionController == null) return;

        if (isCraftingOpen && !(interactionController.CurrentInteractable is WorkbenchInteractable))
        {
            SetCraftingMenuOpen(false);
        }

        var buildSystem = BuildingPlacementSystem.Instance;
        if (buildSystem != null && buildSystem.IsBuildModeActive)
        {
            if (promptBoxBg != null) promptBoxBg.color = new Color(0.18f, 0.28f, 0.12f, 0.95f);
            var recipe = buildSystem.CurrentRecipe;
            promptText.text = $"<color=#50FA7B><b>[İNŞA: {recipe.displayName}]</b> [1-7] Seç (6:Güneş, 7:Rüzgar) | [R] Döndür | [Sol Tık] Kur | [B] Kapat</color>";
            return;
        }

        if (interactionController.CurrentInteractable != null)
        {
            if (promptBoxBg != null) promptBoxBg.color = new Color(0.12f, 0.28f, 0.16f, 0.95f);
            promptText.text = $"<color=#FFE66D><b>{interactionController.CurrentInteractable.GetInteractionPrompt()}</b></color>";
        }
        else if (interactionController.IsVacuumActive)
        {
            if (promptBoxBg != null) promptBoxBg.color = new Color(0.1f, 0.22f, 0.35f, 0.95f);
            if (inventory != null && inventory.IsBackpackFull)
            {
                promptText.text = "<color=#FF5555><b>[ÇANTA DOLU]</b> Üst pusuladaki <b>[ÜS]</b> yönüne gidip Atölyede üretim/yükseltme yapın!</color>";
            }
            else
            {
                promptText.text = "<color=#66D9EF><b>[VAKUM ÇALIŞIYOR]</b> Hava akımıyla çevredeki atıklar çekiliyor...</color>";
            }
        }
        else
        {
            if (promptBoxBg != null) promptBoxBg.color = new Color(0.08f, 0.1f, 0.12f, 0.88f);
            bool hasVac = PlayerProgression.Instance != null && PlayerProgression.Instance.HasVacuumUnlocked;
            string vacTip = hasVac ? "<b>[Sağ Tık]</b> Vakum  |  " : "";
            
            // Eğer çantada çöp varsa oyuncuya ne yapacağını açıkça göster:
            int totalTrash = inventory != null ? inventory.TotalWasteCount : 0;
            string guideHint = "";
            if (totalTrash >= 10 && terrainGenerator != null && playerTransform != null)
            {
                Vector3 toBase = terrainGenerator.BaseWorldPosition - playerTransform.position;
                toBase.y = 0f;
                string arrow = GetIsoCompassArrow(toBase);
                int dist = Mathf.RoundToInt(toBase.magnitude);
                guideHint = $"<color=#8BE9FD><b>{arrow} ÜS (Atölye): {dist}m</b> (Atıkları dönüştür/üret)</color>  |  ";
            }
            else if (totalTrash > 0)
            {
                guideHint = "<color=#F1FA8C>Çöpleri Üsteki Atölyede işle/dönüştür</color>  |  ";
            }

            bool hasMaterials = inventory != null && (inventory.GetCount(ItemType.OrganicWaste) >= 5 || inventory.GetCount(ItemType.ScrapMetal) >= 5);
            string buildHint = hasMaterials ? "<color=#50FA7B><b>[B] Ağaç Dik & İnşa</b></color>" : "<b>[B]</b> İnşa";

            promptText.text = $"{guideHint}<color=#E0E0E0><b>[Sol Tık/E]</b> Topla | <b>[Space]</b> Zıpla | {vacTip}{buildHint}</color>";
        }
    }

    private void ShowNotification(string message)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
        }
        if (notificationBox != null)
        {
            notificationBox.SetActive(true);
        }
        notificationTimer = 2.6f;
    }

    private void UpdateNotificationTimer()
    {
        if (notificationTimer > 0f)
        {
            notificationTimer -= Time.unscaledDeltaTime;
            if (notificationTimer <= 0f && notificationBox != null)
            {
                notificationBox.SetActive(false);
            }
        }
    }

    private void ToggleCraftingMenu(WorkbenchInteractable workbench)
    {
        SetCraftingMenuOpen(!isCraftingOpen);
    }

    public void SetCraftingMenuOpen(bool open)
    {
        isCraftingOpen = open;
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        if (craftingPanel != null)
        {
            craftingPanel.SetActive(isCraftingOpen);
            RefreshDisplay();
        }
    }

    private void HandleCraftingHotkeys()
    {
        if (!isCraftingOpen || isSettingsOpen) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            CraftCarbonFilter();
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            CraftSuperCompost();
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            CraftRiverBuoy();
        else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
            UpgradeVacuum();
        else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame)
            UpgradeBackpack();
        else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame)
            CraftRepairKit();
        else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame)
            CraftIntermediatePlateOrPolymer();
        else if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame)
            CraftCleanEnergyGenerator();
        else if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame)
            UpgradeBoots();
        else if (kb.eKey.wasPressedThisFrame)
            SetCraftingMenuOpen(false);
#endif
    }

    public void UpgradeBoots()
    {
        if (progression == null) progression = PlayerProgression.Instance != null ? PlayerProgression.Instance : FindFirstObjectByType<PlayerProgression>();
        if (progression != null)
        {
            progression.TryUpgradeBoots();
            RefreshDisplay();
        }
    }

    public void CraftCarbonFilter()
    {
        if (inventory == null) return;

        if (inventory.GetCount(ItemType.SteelPlate) >= 3 && inventory.GetCount(ItemType.PolymerBlock) >= 3)
        {
            inventory.TryCraftRecipe(ItemType.CarbonFilter, ItemType.SteelPlate, 3, ItemType.PolymerBlock, 3);
        }
        else
        {
            inventory.TryCraftRecipe(ItemType.CarbonFilter, ItemType.ScrapMetal, 9, ItemType.ScrapMetal, 0);
        }
        RefreshDisplay();
    }

    public void CraftSuperCompost()
    {
        if (inventory != null)
        {
            inventory.TryCraftRecipe(ItemType.SuperCompost, ItemType.OrganicWaste, 9, ItemType.OrganicWaste, 0);
            RefreshDisplay();
        }
    }

    public void CraftRiverBuoy()
    {
        if (inventory == null) return;

        if (inventory.GetCount(ItemType.PolymerBlock) >= 3 && inventory.GetCount(ItemType.SteelPlate) >= 3)
        {
            inventory.TryCraftRecipe(ItemType.RiverCleanerBuoy, ItemType.PolymerBlock, 3, ItemType.SteelPlate, 3);
        }
        else
        {
            inventory.TryCraftRecipe(ItemType.RiverCleanerBuoy, ItemType.PlasticWaste, 6, ItemType.ScrapMetal, 6);
        }
        RefreshDisplay();
    }

    public void CraftRepairKit()
    {
        if (inventory == null) return;

        if (inventory.GetCount(ItemType.SteelPlate) >= 3 && inventory.GetCount(ItemType.PolymerBlock) >= 3)
        {
            inventory.TryCraftRecipe(ItemType.RepairKit, ItemType.SteelPlate, 3, ItemType.PolymerBlock, 3);
        }
        else
        {
            inventory.TryCraftRecipe(ItemType.RepairKit, ItemType.ScrapMetal, 6, ItemType.PlasticWaste, 3);
        }
        RefreshDisplay();
    }

    public void CraftIntermediatePlateOrPolymer()
    {
        if (inventory == null) return;

        if (inventory.GetCount(ItemType.ScrapMetal) >= 4)
        {
            inventory.TryCraftRecipe(ItemType.SteelPlate, ItemType.ScrapMetal, 4, ItemType.ScrapMetal, 0);
        }
        else if (inventory.GetCount(ItemType.PlasticWaste) >= 4)
        {
            inventory.TryCraftRecipe(ItemType.PolymerBlock, ItemType.PlasticWaste, 4, ItemType.PlasticWaste, 0);
        }
        else
        {
            ShowNotification("Ara ürün preslemek için en az 4 Metal Hurda veya 4 Plastik Atık gerekli!");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(null, "4 HURDA / PLASTİK GEREKLİ!");
        }
        RefreshDisplay();
    }

    public void CraftCleanEnergyGenerator()
    {
        if (inventory == null) return;

        if (inventory.GetCount(ItemType.SteelPlate) >= 6 && inventory.GetCount(ItemType.PolymerBlock) >= 6)
        {
            inventory.TryCraftRecipe(ItemType.CleanEnergyGenerator, ItemType.SteelPlate, 6, ItemType.PolymerBlock, 6);
        }
        else
        {
            inventory.TryCraftRecipe(ItemType.CleanEnergyGenerator, ItemType.ScrapMetal, 12, ItemType.PlasticWaste, 12);
        }
        RefreshDisplay();
    }

    public void UpgradeVacuum()
    {
        if (progression == null) progression = PlayerProgression.Instance != null ? PlayerProgression.Instance : FindFirstObjectByType<PlayerProgression>();
        if (progression != null)
        {
            progression.TryUpgradeVacuum();
            RefreshDisplay();
        }
    }

    public void UpgradeBackpack()
    {
        if (progression == null) progression = PlayerProgression.Instance != null ? PlayerProgression.Instance : FindFirstObjectByType<PlayerProgression>();
        if (progression != null)
        {
            progression.TryUpgradeBackpack();
            RefreshDisplay();
        }
    }

    private void BindButton(ref Button btn, string buttonName, UnityEngine.Events.UnityAction action)
    {
        if (btn == null && craftingPanel != null)
        {
            var allButtons = craftingPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in allButtons)
            {
                if (b.gameObject.name == buttonName)
                {
                    btn = b;
                    break;
                }
            }
        }

        if (btn != null)
        {
            btn.onClick.RemoveListener(action);
            btn.onClick.AddListener(action);
        }
    }
}
