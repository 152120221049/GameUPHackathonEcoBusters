using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Unity Editöründe tek tıkla Ana Menü Canvas'ını ve tüm panellerini
/// New Input System uyumlu olarak oluşturan araç.
/// </summary>
public static class MainMenuBuilderEditor
{
    [MenuItem("Tools/Eko-Mucit/Create Complete Main Menu UI", false, 10)]
    public static void CreateMainMenuUI()
    {
        // 1. EventSystem kontrolü (New Input System Uyumlu)
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            eventSystem = esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esObj.AddComponent<InputSystemUIInputModule>();
#else
            esObj.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }
        else
        {
#if ENABLE_INPUT_SYSTEM
            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Object.DestroyImmediate(standalone);
            }
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#endif
        }

        // 2. Canvas Oluştur
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        MainMenuManager menuManager = canvasObj.AddComponent<MainMenuManager>();

        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Main Menu Canvas");

        // 3. Ana Menü Paneli
        GameObject mainMenuPanel = CreatePanel(canvasObj.transform, "MainMenuPanel", new Color(0.08f, 0.1f, 0.12f, 0.85f));
        CreateText(mainMenuPanel.transform, "TitleText", "EKO-MUCİT", 72, new Vector2(0, 320), Color.green);
        CreateText(mainMenuPanel.transform, "SubTitleText", "Taşlaşmış Topraklar & Derme Çatma İcatlar", 28, new Vector2(0, 240), Color.white);

        GameObject btnPlay = CreateButton(mainMenuPanel.transform, "Btn_Play", "OYUNA BAŞLA", new Vector2(0, 80), new Color(0.18f, 0.65f, 0.35f));
        GameObject btnSettings = CreateButton(mainMenuPanel.transform, "Btn_Settings", "AYARLAR", new Vector2(0, -10), new Color(0.2f, 0.4f, 0.6f));
        GameObject btnHowToPlay = CreateButton(mainMenuPanel.transform, "Btn_HowToPlay", "NASIL OYNANIR?", new Vector2(0, -100), new Color(0.5f, 0.4f, 0.2f));
        GameObject btnQuit = CreateButton(mainMenuPanel.transform, "Btn_Quit", "ÇIKIŞ", new Vector2(0, -190), new Color(0.7f, 0.2f, 0.2f));

        // 4. Ayarlar Paneli
        GameObject settingsPanel = CreatePanel(canvasObj.transform, "SettingsPanel", new Color(0.08f, 0.1f, 0.12f, 0.95f));
        CreateText(settingsPanel.transform, "SettingsTitle", "AYARLAR", 56, new Vector2(0, 380), Color.white);

        // Ses Başlığı ve Slider'lar
        CreateText(settingsPanel.transform, "AudioHeader", "--- SES AYARLARI ---", 32, new Vector2(0, 260), Color.yellow);
        CreateText(settingsPanel.transform, "Lbl_Master", "Genel Ses (Master):", 24, new Vector2(-220, 200), Color.white);
        Slider masterSlider = CreateSlider(settingsPanel.transform, "Slider_Master", new Vector2(140, 200));

        CreateText(settingsPanel.transform, "Lbl_Music", "Müzik:", 24, new Vector2(-220, 140), Color.white);
        Slider musicSlider = CreateSlider(settingsPanel.transform, "Slider_Music", new Vector2(140, 140));

        CreateText(settingsPanel.transform, "Lbl_Sfx", "Ses Efektleri (SFX):", 24, new Vector2(-220, 80), Color.white);
        Slider sfxSlider = CreateSlider(settingsPanel.transform, "Slider_SFX", new Vector2(140, 80));

        // Grafik Başlığı ve Kontroller
        CreateText(settingsPanel.transform, "GraphicsHeader", "--- GRAFİK AYARLARI ---", 32, new Vector2(0, -10), Color.yellow);
        CreateText(settingsPanel.transform, "Lbl_Quality", "Grafik Kalitesi:", 24, new Vector2(-220, -70), Color.white);
        TMP_Dropdown qualityDropdown = CreateDropdown(settingsPanel.transform, "Dropdown_Quality", new Vector2(140, -70));

        CreateText(settingsPanel.transform, "Lbl_Resolution", "Çözünürlük:", 24, new Vector2(-220, -140), Color.white);
        TMP_Dropdown resolutionDropdown = CreateDropdown(settingsPanel.transform, "Dropdown_Resolution", new Vector2(140, -140));

        Toggle fullscreenToggle = CreateToggle(settingsPanel.transform, "Toggle_Fullscreen", "Tam Ekran Modu", new Vector2(0, -220));

        GameObject btnSettingsBack = CreateButton(settingsPanel.transform, "Btn_SettingsBack", "GERİ DÖN", new Vector2(0, -340), new Color(0.3f, 0.3f, 0.3f));
        settingsPanel.SetActive(false);

        // 5. Nasıl Oynanır Paneli
        GameObject howToPlayPanel = CreatePanel(canvasObj.transform, "HowToPlayPanel", new Color(0.08f, 0.1f, 0.12f, 0.95f));
        CreateText(howToPlayPanel.transform, "HowToPlayTitle", "NASIL OYNANIR?", 56, new Vector2(0, 380), Color.white);

        string instructions = "• W, A, S, D: İzometrik Dünyada Hareket Et\n" +
                              "• Sol Shift: Koşma\n" +
                              "• Fare Tekerleği: Kamerayı Yakınlaştır / Uzaklaştır\n\n" +
                              "HİKAYE & HEDEF:\n" +
                              "1. Fabrika doğayı kirletirken, tarlalar verimsizleşiyor ve kasaba arada kalıyor.\n" +
                              "2. Derme çatma bir Eko-Mucit olarak etraftan hurda ve organik atıkları topla!\n" +
                              "3. Atölyende baca filtreleri, süper kompost ve nehir dubaları üret.\n" +
                              "4. Şehrin refahını ve ekolojik dengeyi koruyarak yeşil bir zafer elde et!";

        CreateText(howToPlayPanel.transform, "InstructionsText", instructions, 24, new Vector2(0, 30), Color.white, new Vector2(900, 500));
        GameObject btnHowToPlayBack = CreateButton(howToPlayPanel.transform, "Btn_HowToPlayBack", "GERİ DÖN", new Vector2(0, -340), new Color(0.3f, 0.3f, 0.3f));
        howToPlayPanel.SetActive(false);

        // 6. Serialized Referansları Bağla
        SerializedObject so = new SerializedObject(menuManager);
        so.FindProperty("mainMenuPanel").objectReferenceValue = mainMenuPanel;
        so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
        so.FindProperty("howToPlayPanel").objectReferenceValue = howToPlayPanel;

        so.FindProperty("loadGameSceneOnPlay").boolValue = true;
        so.FindProperty("gameSceneName").stringValue = "SampleScene";

        so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
        so.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
        so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;

        so.FindProperty("qualityDropdown").objectReferenceValue = qualityDropdown;
        so.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDropdown;
        so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
        so.ApplyModifiedProperties();

        // Buton Event Bağlantıları
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnPlay.GetComponent<Button>().onClick, menuManager.PlayGame);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnSettings.GetComponent<Button>().onClick, menuManager.OpenSettings);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnHowToPlay.GetComponent<Button>().onClick, menuManager.OpenHowToPlay);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnQuit.GetComponent<Button>().onClick, menuManager.QuitGame);

        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnSettingsBack.GetComponent<Button>().onClick, menuManager.OpenMainMenu);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btnHowToPlayBack.GetComponent<Button>().onClick, menuManager.OpenMainMenu);

        Selection.activeGameObject = canvasObj;
        Debug.Log("Eko-Mucit: New Input System uyumlu Ana Menü Canvas'ı başarıyla oluşturuldu!");
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Image img = panel.AddComponent<Image>();
        img.color = color;
        return panel;
    }

    private static GameObject CreateText(Transform parent, string name, string text, float fontSize, Vector2 anchoredPos, Color color, Vector2? size = null)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size ?? new Vector2(800, 100);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return textObj;
    }

    private static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(340, 65);

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = bgColor * 1.25f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return btnObj;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        RectTransform rt = sliderObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(280, 24);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0, 0.25f);
        faRt.anchorMax = new Vector2(1, 0.75f);
        faRt.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fRt = fill.AddComponent<RectTransform>();
        fRt.sizeDelta = Vector2.zero;
        Image fImg = fill.AddComponent<Image>();
        fImg.color = new Color(0.2f, 0.8f, 0.4f, 1f);
        slider.fillRect = fRt;

        return slider;
    }

    private static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 anchoredPos)
    {
        GameObject ddObj = new GameObject(name);
        ddObj.transform.SetParent(parent, false);
        RectTransform rt = ddObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(280, 40);

        Image img = ddObj.AddComponent<Image>();
        img.color = new Color(0.18f, 0.2f, 0.24f);
        TMP_Dropdown dropdown = ddObj.AddComponent<TMP_Dropdown>();

        GameObject captionText = new GameObject("Caption Text");
        captionText.transform.SetParent(ddObj.transform, false);
        RectTransform ctRt = captionText.AddComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.sizeDelta = new Vector2(-20, 0);

        TextMeshProUGUI tmp = captionText.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 20;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        dropdown.captionText = tmp;

        return dropdown;
    }

    private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        GameObject toggleObj = new GameObject(name);
        toggleObj.transform.SetParent(parent, false);
        RectTransform rt = toggleObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(260, 40);

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(toggleObj.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.sizeDelta = new Vector2(30, 30);
        bgRt.anchoredPosition = new Vector2(-100, 0);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);
        toggle.targetGraphic = bgImg;

        GameObject check = new GameObject("Checkmark");
        check.transform.SetParent(bg.transform, false);
        RectTransform cRt = check.AddComponent<RectTransform>();
        cRt.sizeDelta = new Vector2(20, 20);
        Image cImg = check.AddComponent<Image>();
        cImg.color = Color.green;
        toggle.graphic = cImg;

        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(toggleObj.transform, false);
        RectTransform lblRt = lbl.AddComponent<RectTransform>();
        lblRt.anchoredPosition = new Vector2(30, 0);
        lblRt.sizeDelta = new Vector2(200, 40);
        TextMeshProUGUI tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return toggle;
    }
}
