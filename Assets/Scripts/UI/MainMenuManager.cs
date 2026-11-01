using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Eko-Mucit Ana Menü ve Ayarlar Sistemi.
/// Menü panelleri arası geçiş, sahne yükleme, canlı ses/grafik ayarları
/// ve New Input System uyumlu EventSystem yönetimini içerir.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("--- PANELLER ---")]
    [Tooltip("Ana menü butonlarının bulunduğu panel")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("Ayarlar paneli")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Nasıl oynanır / Kontroller paneli")]
    [SerializeField] private GameObject howToPlayPanel;

    [Header("--- SAHNE YÖNETİMİ ---")]
    [Tooltip("Oyna butonuna basınca yeni sahne yüklensin mi?")]
    [SerializeField] private bool loadGameSceneOnPlay = true;
    [Tooltip("Yüklenecek oyun sahnesinin adı")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("--- SES AYARLARI KONTROLLERİ ---")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("--- GRAFİK AYARLARI KONTROLLERİ ---")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private Resolution[] availableResolutions;

    private const string PREF_MASTER_VOL = "MasterVol";
    private const string PREF_MUSIC_VOL = "MusicVol";
    private const string PREF_SFX_VOL = "SfxVol";
    private const string PREF_QUALITY = "QualitySetting";
    private const string PREF_FULLSCREEN = "FullscreenSetting";

    private void Awake()
    {
        FixEventSystemInputModule();
        AutoBindMenuElements();
    }

    private void Start()
    {
        InitializePanels();
        InitializeAudioSettings();
        InitializeGraphicsSettings();
        EnsureMainMenuMusic();
        ApplyJuicyButtonStyling();
    }

    /// <summary>
    /// Menü butonlarına asset gelene kadar modern, canlı, hover animasyonlu ve gölgeli şık bir görünüm kazandırır.
    /// </summary>
    private void ApplyJuicyButtonStyling()
    {
        StyleButton("Btn_Play", new Color(0.15f, 0.68f, 0.38f), new Color(0.22f, 0.82f, 0.46f), "BAŞLA");
        StyleButton("Btn_Settings", new Color(0.18f, 0.45f, 0.72f), new Color(0.25f, 0.58f, 0.88f), "AYARLAR");
        StyleButton("Btn_HowToPlay", new Color(0.78f, 0.52f, 0.18f), new Color(0.92f, 0.65f, 0.25f), "REHBER");
        StyleButton("Btn_Quit", new Color(0.72f, 0.22f, 0.22f), new Color(0.88f, 0.30f, 0.30f), "ÇIKIŞ");
        StyleButton("Btn_SettingsBack", new Color(0.28f, 0.32f, 0.38f), new Color(0.38f, 0.44f, 0.52f), "GERİ");
        StyleButton("Btn_HowToPlayBack", new Color(0.28f, 0.32f, 0.38f), new Color(0.38f, 0.44f, 0.52f), "GERİ");
    }

    private void StyleButton(string btnName, Color baseColor, Color highlightColor, string prefixTag)
    {
        Button btn = FindComponentByName<Button>(btnName);
        if (btn == null) return;

        // 1. Canlı renk geçişi (Color Tint)
        ColorBlock cb = btn.colors;
        cb.normalColor = baseColor;
        cb.highlightedColor = highlightColor;
        cb.pressedColor = baseColor * 0.75f;
        cb.selectedColor = highlightColor;
        cb.fadeDuration = 0.12f;
        btn.colors = cb;

        // 2. Boyut ve yumuşak kenarlık hissi
        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(360f, 62f);
        }

        // 3. Buton Arka Planına Canlı Kontur / Çerçeve (Outline efekti)
        Outline outline = btn.GetComponent<Outline>() ?? btn.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        // 4. Yazı Tipi ve Formatlama
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.fontStyle = FontStyles.Bold;
            tmp.fontSize = 24f;
            tmp.color = Color.white;
            tmp.characterSpacing = 3.5f;

            // Yazıya hafif derinlik gölgesi
            Outline textOutline = tmp.GetComponent<Outline>() ?? tmp.gameObject.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // 5. Hover & Click Juice Animasyonu (Üzerine gelince büyüme ve tıklama hissi)
        if (btn.GetComponent<JuicyMenuButtonHover>() == null)
        {
            btn.gameObject.AddComponent<JuicyMenuButtonHover>();
        }
    }

    private void EnsureMainMenuMusic()
    {
        if (GameAudioAndJuiceManager.Instance != null)
        {
            GameAudioAndJuiceManager.Instance.PlayMainMenuMusic();
        }
        else
        {
            StartCoroutine(WaitForAudioAndPlayMenuMusic());
        }
    }

    private System.Collections.IEnumerator WaitForAudioAndPlayMenuMusic()
    {
        yield return null;
        if (GameAudioAndJuiceManager.Instance != null)
        {
            GameAudioAndJuiceManager.Instance.PlayMainMenuMusic();
        }
    }

    // Otomatik diorama üretimi kaldırıldı; kullanıcı sahneye kendi dioramasını yerleştirir.
    private void EnsureMainMenuDiorama()
    {
    }

    /// <summary>
    /// Sahnede Inspector bağlantıları kopmuşsa veya eksikse buton, panel ve slider'ları isimlerinden otomatik bulup bağlar.
    /// </summary>
    private void AutoBindMenuElements()
    {
        if (mainMenuPanel == null)
        {
            var t = transform.Find("MainMenuPanel");
            if (t != null) mainMenuPanel = t.gameObject;
        }
        if (settingsPanel == null)
        {
            var t = transform.Find("SettingsPanel");
            if (t != null) settingsPanel = t.gameObject;
        }
        if (howToPlayPanel == null)
        {
            var t = transform.Find("HowToPlayPanel");
            if (t != null) howToPlayPanel = t.gameObject;
        }

        // Butonları çalışma zamanında garantili bağla
        BindButtonByName("Btn_Play", PlayGame);
        BindButtonByName("Btn_Settings", OpenSettings);
        BindButtonByName("Btn_HowToPlay", OpenHowToPlay);
        BindButtonByName("Btn_Quit", QuitGame);
        BindButtonByName("Btn_SettingsBack", OpenMainMenu);
        BindButtonByName("Btn_HowToPlayBack", OpenMainMenu);

        // Slider'ları bul ve etkileşimli (handle/targetGraphic) olduğundan emin ol
        if (masterVolumeSlider == null) masterVolumeSlider = FindComponentByName<Slider>("Slider_Master");
        if (musicVolumeSlider == null) musicVolumeSlider = FindComponentByName<Slider>("Slider_Music");
        if (sfxVolumeSlider == null) sfxVolumeSlider = FindComponentByName<Slider>("Slider_SFX");

        EnsureSliderInteractive(masterVolumeSlider);
        EnsureSliderInteractive(musicVolumeSlider);
        EnsureSliderInteractive(sfxVolumeSlider);

        if (qualityDropdown == null) qualityDropdown = FindComponentByName<TMP_Dropdown>("Dropdown_Quality");
        if (resolutionDropdown == null) resolutionDropdown = FindComponentByName<TMP_Dropdown>("Dropdown_Resolution");
        if (fullscreenToggle == null) fullscreenToggle = FindComponentByName<Toggle>("Toggle_Fullscreen");
    }

    private void BindButtonByName(string btnName, UnityEngine.Events.UnityAction action)
    {
        Button btn = FindComponentByName<Button>(btnName);
        if (btn != null)
        {
            btn.onClick.RemoveListener(action);
            btn.onClick.AddListener(action);
        }
    }

    private T FindComponentByName<T>(string objName) where T : Component
    {
        T[] all = GetComponentsInChildren<T>(true);
        foreach (var item in all)
        {
            if (item.gameObject.name == objName) return item;
        }
        return null;
    }

    private void EnsureSliderInteractive(Slider slider)
    {
        if (slider == null) return;
        slider.interactable = true;

        if (slider.targetGraphic == null)
        {
            Image bgImg = slider.GetComponentInChildren<Image>();
            if (bgImg != null) slider.targetGraphic = bgImg;
        }

        if (slider.handleRect == null)
        {
            Transform handleArea = slider.transform.Find("Handle Slide Area");
            if (handleArea == null)
            {
                GameObject hsa = new GameObject("Handle Slide Area");
                hsa.transform.SetParent(slider.transform, false);
                RectTransform hsaRt = hsa.AddComponent<RectTransform>();
                hsaRt.anchorMin = Vector2.zero;
                hsaRt.anchorMax = Vector2.one;
                hsaRt.offsetMin = new Vector2(8f, 0f);
                hsaRt.offsetMax = new Vector2(-8f, 0f);

                GameObject handle = new GameObject("Handle");
                handle.transform.SetParent(hsa.transform, false);
                RectTransform hRt = handle.AddComponent<RectTransform>();
                hRt.sizeDelta = new Vector2(22f, 30f);
                Image hImg = handle.AddComponent<Image>();
                hImg.color = new Color(0.95f, 0.98f, 1f, 1f);

                slider.handleRect = hRt;
                slider.targetGraphic = hImg;
            }
        }
    }

    /// <summary>
    /// New Input System ile eski StandaloneInputModule çakışmasını otomatik çözer.
    /// </summary>
    private void FixEventSystemInputModule()
    {
#if ENABLE_INPUT_SYSTEM
        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            eventSystem = esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
            {
                DestroyImmediate(standaloneModule);
            }

            var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
#endif
    }

    #region Panel Geçişleri
    public void OpenMainMenu()
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
    }

    public void OpenHowToPlay()
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(true);
    }

    private void InitializePanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
    }
    #endregion

    #region Ana Menü Butonları
    public void PlayGame()
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        Time.timeScale = 1f;

        bool isInDifferentScene = !string.IsNullOrEmpty(gameSceneName) &&
                                 SceneManager.GetActiveScene().name != gameSceneName;

        if (loadGameSceneOnPlay || isInDifferentScene)
        {
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
        }
        else
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        }
    }

    public void QuitGame()
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        Debug.Log("Oyun kapatılıyor...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    #endregion

    #region Ses Ayarları
    private void InitializeAudioSettings()
    {
        float masterVol = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.85f);
        float musicVol = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.55f);
        float sfxVol = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.90f);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(masterVol);
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(musicVol);
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(sfxVol);
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        AudioListener.volume = masterVol;
        GameAudioAndJuiceManager.Instance?.LoadSavedSettings();
        GameAudioAndJuiceManager.Instance?.ApplyAudioVolumes();
    }

    public void SetMasterVolume(float volume)
    {
        if (GameAudioAndJuiceManager.Instance != null)
        {
            GameAudioAndJuiceManager.Instance.SetMasterVolume(volume);
        }
        else
        {
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(PREF_MASTER_VOL, volume);
            PlayerPrefs.Save();
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (GameAudioAndJuiceManager.Instance != null)
        {
            GameAudioAndJuiceManager.Instance.SetMusicVolume(volume);
        }
        else
        {
            PlayerPrefs.SetFloat(PREF_MUSIC_VOL, volume);
            PlayerPrefs.Save();
        }
    }

    public void SetSfxVolume(float volume)
    {
        if (GameAudioAndJuiceManager.Instance != null)
        {
            GameAudioAndJuiceManager.Instance.SetSfxVolume(volume);
        }
        else
        {
            PlayerPrefs.SetFloat(PREF_SFX_VOL, volume);
            PlayerPrefs.Save();
        }
    }
    #endregion

    #region Grafik ve Ekran Ayarları
    private void InitializeGraphicsSettings()
    {
        int savedQuality = Mathf.Clamp(PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        QualitySettings.SetQualityLevel(savedQuality);

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            List<string> options = new List<string>(QualitySettings.names);
            qualityDropdown.AddOptions(options);
            qualityDropdown.SetValueWithoutNotify(savedQuality);
            qualityDropdown.RefreshShownValue();
            qualityDropdown.onValueChanged.RemoveListener(SetQuality);
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }

        bool isFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = isFullscreen;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(isFullscreen);
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        if (resolutionDropdown != null)
        {
            availableResolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();

            List<string> resOptions = new List<string>();
            int currentResIndex = 0;

            for (int i = 0; i < availableResolutions.Length; i++)
            {
                string option = $"{availableResolutions[i].width} x {availableResolutions[i].height} @{availableResolutions[i].refreshRateRatio.value:0}Hz";
                resOptions.Add(option);

                if (availableResolutions[i].width == Screen.currentResolution.width &&
                    availableResolutions[i].height == Screen.currentResolution.height)
                {
                    currentResIndex = i;
                }
            }

            if (resOptions.Count > 0)
            {
                resolutionDropdown.AddOptions(resOptions);
                resolutionDropdown.SetValueWithoutNotify(currentResIndex);
                resolutionDropdown.RefreshShownValue();
                resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
                resolutionDropdown.onValueChanged.AddListener(SetResolution);
            }
        }
    }

    public void SetQuality(int qualityIndex)
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        QualitySettings.SetQualityLevel(qualityIndex);
        PlayerPrefs.SetInt(PREF_QUALITY, qualityIndex);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetResolution(int resolutionIndex)
    {
        GameAudioAndJuiceManager.Instance?.PlayUiClick();
        if (availableResolutions != null && resolutionIndex >= 0 && resolutionIndex < availableResolutions.Length)
        {
            Resolution res = availableResolutions[resolutionIndex];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }
    }
    #endregion
}

/// <summary>
/// Menü butonlarına üzerine gelindiğinde (Hover) tatlı büyüme (scale-up) ve
/// tıklandığında elastik basılma (punch-down) hissi katan yardımcı component.
/// </summary>
public class JuicyMenuButtonHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
{
    private Vector3 originalScale;
    private Vector3 targetScale;
    private float smoothSpeed = 14f;

    private void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void OnEnable()
    {
        transform.localScale = originalScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * smoothSpeed);
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale * 1.055f;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale * 0.94f;
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        targetScale = originalScale * 1.055f;
    }
}
