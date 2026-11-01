using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Unity Editöründe tek tıkla sahneye ekran sınırlarına tam oturan (taşmayan),
/// kolayca düzenlenebilir Gameplay HUD Canvas'ını ve panellerini oluşturan araç.
/// </summary>
public static class GameplayHUDBuilderEditor
{
    [MenuItem("Tools/Eko-Mucit/Create Gameplay HUD In Scene", false, 11)]
    public static void CreateGameplayHUDInScene()
    {
        // 1. EventSystem Kontrolü
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

        // Varsa eski HUD'ı temizle
        var existing = GameObject.Find("GameplayHUDCanvas");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        // 2. Canvas Oluştur
        GameObject canvasObj = new GameObject("GameplayHUDCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        GameplayHUD hudManager = canvasObj.AddComponent<GameplayHUD>();

        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Gameplay HUD");

        // 3. SOL ÜST: Envanter / Çanta Paneli
        GameObject invPanel = CreatePanel(canvasObj.transform, "InventoryPanel", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(350, 300), new Color(0.08f, 0.1f, 0.13f, 0.92f));
        TextMeshProUGUI invText = CreateTMP(invPanel.transform, "InventoryText",
            "<b><size=20><color=#FFD700>[ÇANTA] (0/15)</color></size></b>\n" +
            "<color=#C87D55>[*]</color> Metal Hurda: <color=#FFFFFF><b>0</b></color>\n" +
            "<color=#5BC0EB>[*]</color> Plastik Şişe: <color=#FFFFFF><b>0</b></color>\n" +
            "<color=#9BC53D>[*]</color> Biyo-Hammadde: <color=#FFFFFF><b>0</b></color>\n\n" +
            "<b><size=20><color=#50FA7B>[+] EKO-İCATLAR</color></size></b>\n" +
            "<color=#A0AAB2>[*]</color> Baca Filtresi: <color=#50FA7B><b>0</b></color>\n" +
            "<color=#4EBA6F>[*]</color> Süper Kompost: <color=#50FA7B><b>0</b></color>\n" +
            "<color=#FDE74C>[*]</color> Temizleme Dubası: <color=#50FA7B><b>0</b></color>\n\n" +
            "<size=16><color=#8BE9FD>[VAKUM] Prototip Hurda Emici (Sv.1)</color></size>",
            18, TextAlignmentOptions.TopLeft, new Vector2(18, -18), new Vector2(314, 264));

        // 4. SAĞ ÜST: Görev Rehberi
        GameObject questPanel = CreatePanel(canvasObj.transform, "QuestPanel", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(360, 200), new Color(0.08f, 0.1f, 0.13f, 0.92f));
        CreateTMP(questPanel.transform, "QuestText",
            "<b><size=20><color=#FFE66D>[GÖREV REHBERİ]</color></size></b>\n\n" +
            "<b>1.</b> Çevreden <b>Hurda</b> ve <b>Şişe</b> vakumla.\n" +
            "<b>2.</b> <b>[B]</b> ile İnşa Modunu aç: Pres ve Sandık kur!\n" +
            "<b>3.</b> Atölyede <b>[E]</b> ile Vakumunu güçlendir.\n" +
            "<b>4.</b> Eko-İcatlar üreterek kirliliği durdur!",
            16, TextAlignmentOptions.TopLeft, new Vector2(18, -18), new Vector2(324, 164));

        // 5. ALT ORTA: Etkileşim ve Vakum Kartı
        GameObject promptBox = CreatePanel(canvasObj.transform, "PromptPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 45), new Vector2(720, 56), new Color(0.08f, 0.1f, 0.12f, 0.90f));
        Image promptBg = promptBox.GetComponent<Image>();
        TextMeshProUGUI promptText = CreateTMP(promptBox.transform, "PromptText", "<color=#E0E0E0><b>[Sol Tık / Space]</b> Vakumla Çek   |   <b>[E]</b> Etkileşim   |   <b>[B]</b> İnşa Modu</color>", 19, TextAlignmentOptions.Center, Vector2.zero, new Vector2(700, 50));

        // 6. ÜST ORTA: Bildirim (Toast)
        GameObject notifBox = CreatePanel(canvasObj.transform, "NotificationBox", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(460, 52), new Color(0.12f, 0.24f, 0.16f, 0.95f));
        TextMeshProUGUI notifText = CreateTMP(notifBox.transform, "NotificationText", "+1 Metal Hurda", 22, TextAlignmentOptions.Center, Vector2.zero, new Vector2(440, 46));
        notifText.color = new Color(0.4f, 1f, 0.6f);
        notifBox.SetActive(false);

        // 7. ORTA: Hurdalık Atölyesi ve Ekipman Geliştirme (Crafting Paneli)
        GameObject craftingPanel = CreatePanel(canvasObj.transform, "CraftingPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020, 620), new Color(0.07f, 0.09f, 0.12f, 0.98f));
        CreateTMP(craftingPanel.transform, "Title", "<b><size=30><color=#50FA7B>[ATÖLYE] HURDALIK VE EKO-GELİŞTİRME</color></size></b>", 28, TextAlignmentOptions.Center, new Vector2(0, 265), new Vector2(850, 45));
        CreateTMP(craftingPanel.transform, "SubTitle", "<color=#CCCCCC>Topladığın hurdalarla eko-icatlar yap [1,2,3] veya aletlerini güçlendir [4,5]!</color>", 18, TextAlignmentOptions.Center, new Vector2(0, 230), new Vector2(850, 30));

        // --- ÜST SIRA: 3 ADET EKO-İCAT KARTI ---
        // Kart 1: Baca Filtresi
        GameObject card1 = CreatePanel(craftingPanel.transform, "Card1_Filter", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-310, 85), new Vector2(280, 220), new Color(0.12f, 0.15f, 0.2f, 1f));
        CreateTMP(card1.transform, "C1_Title", "<b><color=#FFFFFF>[1] Baca Filtresi</color></b>", 20, TextAlignmentOptions.Center, new Vector2(0, 75), new Vector2(260, 30));
        CreateTMP(card1.transform, "C1_Desc", "<color=#AAAAAA>Fabrika bacalarına takılarak zehirli dumanı temizler.</color>", 15, TextAlignmentOptions.Center, new Vector2(0, 25), new Vector2(250, 55));
        TextMeshProUGUI c1Cost = CreateTMP(card1.transform, "C1_Cost", "Gereken: 3 Hurda", 16, TextAlignmentOptions.Center, new Vector2(0, -30), new Vector2(260, 30));
        GameObject btnObj1 = CreateButton(card1.transform, "Btn_Craft1", "ÜRET [1]", new Vector2(0, -70), new Vector2(160, 36), new Color(0.2f, 0.65f, 0.35f), hudManager.CraftCarbonFilter);

        // Kart 2: Biyo-Kompost
        GameObject card2 = CreatePanel(craftingPanel.transform, "Card2_Compost", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 85), new Vector2(280, 220), new Color(0.12f, 0.15f, 0.2f, 1f));
        CreateTMP(card2.transform, "C2_Title", "<b><color=#FFFFFF>[2] Biyo-Kompost</color></b>", 20, TextAlignmentOptions.Center, new Vector2(0, 75), new Vector2(260, 30));
        CreateTMP(card2.transform, "C2_Desc", "<color=#AAAAAA>Zehirli ve kurumuş tarla toprağını canlandırır.</color>", 15, TextAlignmentOptions.Center, new Vector2(0, 25), new Vector2(250, 55));
        TextMeshProUGUI c2Cost = CreateTMP(card2.transform, "C2_Cost", "Gereken: 3 Biyo-Madde", 16, TextAlignmentOptions.Center, new Vector2(0, -30), new Vector2(260, 30));
        GameObject btnObj2 = CreateButton(card2.transform, "Btn_Craft2", "ÜRET [2]", new Vector2(0, -70), new Vector2(160, 36), new Color(0.2f, 0.65f, 0.35f), hudManager.CraftSuperCompost);

        // Kart 3: Nehir Dubası
        GameObject card3 = CreatePanel(craftingPanel.transform, "Card3_Buoy", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(310, 85), new Vector2(280, 220), new Color(0.12f, 0.15f, 0.2f, 1f));
        CreateTMP(card3.transform, "C3_Title", "<b><color=#FFFFFF>[3] Nehir Dubası</color></b>", 20, TextAlignmentOptions.Center, new Vector2(0, 75), new Vector2(260, 30));
        CreateTMP(card3.transform, "C3_Desc", "<color=#AAAAAA>Nehir ve kanallardaki yüzen atıkları süzüp toplar.</color>", 15, TextAlignmentOptions.Center, new Vector2(0, 25), new Vector2(250, 55));
        TextMeshProUGUI c3Cost = CreateTMP(card3.transform, "C3_Cost", "Gereken: 2 Şişe + 2 Hurda", 16, TextAlignmentOptions.Center, new Vector2(0, -30), new Vector2(260, 30));
        GameObject btnObj3 = CreateButton(card3.transform, "Btn_Craft3", "ÜRET [3]", new Vector2(0, -70), new Vector2(160, 36), new Color(0.2f, 0.65f, 0.35f), hudManager.CraftRiverBuoy);

        // --- ALT SIRA: 2 ADET EKİPMAN GELİŞTİRME KARTI ---
        // Kart 4: Vakum Güçlendirme
        GameObject card4 = CreatePanel(craftingPanel.transform, "Card4_Vacuum", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-235, -135), new Vector2(430, 160), new Color(0.14f, 0.18f, 0.24f, 1f));
        CreateTMP(card4.transform, "C4_Title", "<b><color=#8BE9FD>[4] Vakum Güçlendir</color></b>", 20, TextAlignmentOptions.Center, new Vector2(0, 52), new Vector2(410, 28));
        CreateTMP(card4.transform, "C4_Desc", "<color=#AAAAAA>Çekim menzilini ve hızını artırır (5m -> 7.5m -> 11m).</color>", 15, TextAlignmentOptions.Center, new Vector2(0, 15), new Vector2(410, 28));
        TextMeshProUGUI c4Cost = CreateTMP(card4.transform, "C4_Cost", "Gereken: 6 Hurda + 4 Şişe", 16, TextAlignmentOptions.Center, new Vector2(0, -20), new Vector2(410, 28));
        GameObject btnObj4 = CreateButton(card4.transform, "Btn_UpgradeVac", "GÜÇLENDİR [4]", new Vector2(0, -55), new Vector2(190, 34), new Color(0.15f, 0.5f, 0.7f), hudManager.UpgradeVacuum);

        // Kart 5: Çanta Genişletme
        GameObject card5 = CreatePanel(craftingPanel.transform, "Card5_Backpack", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(235, -135), new Vector2(430, 160), new Color(0.14f, 0.18f, 0.24f, 1f));
        CreateTMP(card5.transform, "C5_Title", "<b><color=#FFD700>[5] Çantayı Genişlet</color></b>", 20, TextAlignmentOptions.Center, new Vector2(0, 52), new Vector2(410, 28));
        CreateTMP(card5.transform, "C5_Desc", "<color=#AAAAAA>Sırt çantası atık kapasitesini büyütür (15 -> 30 -> 60).</color>", 15, TextAlignmentOptions.Center, new Vector2(0, 15), new Vector2(410, 28));
        TextMeshProUGUI c5Cost = CreateTMP(card5.transform, "C5_Cost", "Gereken: 6 Hurda + 5 Şişe", 16, TextAlignmentOptions.Center, new Vector2(0, -20), new Vector2(410, 28));
        GameObject btnObj5 = CreateButton(card5.transform, "Btn_UpgradeBag", "GENİŞLET [5]", new Vector2(0, -55), new Vector2(190, 34), new Color(0.75f, 0.55f, 0.15f), hudManager.UpgradeBackpack);

        CreateTMP(craftingPanel.transform, "CloseInfo", "<color=#FFE66D>Kapatmak için <b>[E]</b> veya <b>[ESC]</b> tuşuna basabilirsiniz</color>", 17, TextAlignmentOptions.Center, new Vector2(0, -270), new Vector2(850, 30));
        craftingPanel.SetActive(false);

        // 8. Serialized Referansları Bağla
        SerializedObject so = new SerializedObject(hudManager);
        so.FindProperty("inventoryText").objectReferenceValue = invText;
        so.FindProperty("promptBoxBg").objectReferenceValue = promptBg;
        so.FindProperty("promptText").objectReferenceValue = promptText;
        so.FindProperty("notificationBox").objectReferenceValue = notifBox;
        so.FindProperty("notificationText").objectReferenceValue = notifText;
        so.FindProperty("craftingPanel").objectReferenceValue = craftingPanel;

        so.FindProperty("recipe1CostText").objectReferenceValue = c1Cost;
        so.FindProperty("recipe2CostText").objectReferenceValue = c2Cost;
        so.FindProperty("recipe3CostText").objectReferenceValue = c3Cost;
        so.FindProperty("vacuumUpgradeCostText").objectReferenceValue = c4Cost;
        so.FindProperty("backpackUpgradeCostText").objectReferenceValue = c5Cost;

        so.FindProperty("btnCraftFilter").objectReferenceValue = btnObj1.GetComponent<Button>();
        so.FindProperty("btnCraftCompost").objectReferenceValue = btnObj2.GetComponent<Button>();
        so.FindProperty("btnCraftBuoy").objectReferenceValue = btnObj3.GetComponent<Button>();
        so.FindProperty("btnUpgradeVacuum").objectReferenceValue = btnObj4.GetComponent<Button>();
        so.FindProperty("btnUpgradeBackpack").objectReferenceValue = btnObj5.GetComponent<Button>();
        so.ApplyModifiedProperties();

        Selection.activeGameObject = canvasObj;
        Debug.Log("Eko-Mucit: Gameplay HUD Canvas'ı (Ekipman Geliştirme Destekli) sahneye başarıyla eklendi!");
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color bgColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = panel.AddComponent<Image>();
        img.color = bgColor;
        return panel;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, TextAlignmentOptions align, Vector2 anchoredPos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = align;
        return tmp;
    }

    private static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = bgColor * 1.25f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false; // Tıklamaların buton tarafından engelsiz algılanmasını sağlar

        return btnObj;
    }
}
