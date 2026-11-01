using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sahneye Giriş Yükleme Ekranı (Scene Loading Screen).
/// - Harita oluşturulurken 'Ekosistem Hazırlanıyor...' başlığı ve şık bir progress bar gösterir.
/// - Dünya, nehir, köprüler ve nesneler tamamen hazır olana kadar ekranı kaplar.
/// - Tamamlandığında yumuşak bir fade-out (solma) animasyonuyla oyunu açar.
/// </summary>
public class SceneLoadingScreen : MonoBehaviour
{
    public static SceneLoadingScreen Instance { get; private set; }

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Slider progressBar;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI percentText;
    private TextMeshProUGUI tipText;
    private RectTransform iconEmblemRect;
    private bool isFadingOut = false;
    private float tipTimer = 0f;
    private int currentTipIndex = 0;

    private static readonly string[] EcoTips = new string[]
    {
        "<b>İpucu:</b> Başlangıçta çöpleri elindeki Çöp Toplama Çubuğu ile [Sol Tık] / [E] seçerek topla.",
        "<b>İpucu:</b> Nehirdeki Temizleme Dubaları zamanla plastik ve organik atıkları kendiliğinden süzer.",
        "<b>İpucu:</b> Fabrika bacalarına Baca Filtresi takarak dumanı temizleyebilir ve fabrikayı işletebilirsin.",
        "<b>İpucu:</b> Atölyede Süper Kompost üreterek tarlaların mahsul verimini katlayabilirsin.",
        "<b>İpucu:</b> Şehir sakinlerinin güvenini kazanıp onları takımına katarak çöp toplama hızını artır!",
        "<b>İpucu:</b> Güneş panelleri ve rüzgar türbinleri inşa ederek adaya pasif temiz enerji ve gelir sağla.",
        "<b>İpucu:</b> Fidan dikerek adanın ekosistemini hızla canlandır ve yeşil alanları çoğalt."
    };

    public static SceneLoadingScreen ShowScreen()
    {
        if (Instance != null) return Instance;

        GameObject root = new GameObject("SceneLoadingScreen");
        Instance = root.AddComponent<SceneLoadingScreen>();
        Instance.BuildUI();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void BuildUI()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Her şeyin en üstünde görünsün

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // Koyu eko-modern arka plan
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.09f, 0.12f, 1f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Merkez İçerik Kutusu
        GameObject contentObj = new GameObject("CenterContent");
        contentObj.transform.SetParent(bgObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(700f, 320f);
        contentRect.anchoredPosition = Vector2.zero;

        // Oyun Başlığı
        CreateText(contentObj.transform, "Title", "<b>ECOBUSTERS</b>", 48f, new Vector2(0f, 90f), new Color(0.35f, 0.95f, 0.65f), FontStyles.Bold);

        // Alt Başlık
        CreateText(contentObj.transform, "Subtitle", "DOĞAYI GERİ KAZAN • ŞEHRİ DÖNÜŞTÜR", 18f, new Vector2(0f, 45f), new Color(0.72f, 0.85f, 0.92f), FontStyles.Normal);

        // Durum Metni (Ne yükleniyor?)
        statusText = CreateText(contentObj.transform, "StatusText", "Ekosistem Hazırlanıyor...", 20f, new Vector2(0f, -15f), new Color(0.92f, 0.96f, 1f), FontStyles.Normal);

        // Progress Bar Dış Çerçevesi
        GameObject barBgObj = new GameObject("ProgressBarBg");
        barBgObj.transform.SetParent(contentObj.transform, false);
        RectTransform barBgRect = barBgObj.AddComponent<RectTransform>();
        barBgRect.sizeDelta = new Vector2(560f, 26f);
        barBgRect.anchoredPosition = new Vector2(0f, -65f);
        Image barBgImg = barBgObj.AddComponent<Image>();
        barBgImg.color = new Color(0.12f, 0.18f, 0.24f, 0.95f);

        // Progress Bar Dolgu Alanı (Fill)
        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(barBgObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-6f, -6f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.24f, 0.88f, 0.52f, 1f);

        progressBar = barBgObj.AddComponent<Slider>();
        progressBar.targetGraphic = fillImg;
        progressBar.fillRect = fillRect;
        progressBar.minValue = 0f;
        progressBar.maxValue = 1f;
        progressBar.value = 0.05f;

        // Yüzde Metni (%0 - %100)
        percentText = CreateText(contentObj.transform, "PercentText", "%5", 16f, new Vector2(0f, -100f), new Color(0.6f, 0.8f, 0.75f), FontStyles.Normal);

        // Dinamik Döngülü Eko-İpuçları Kutusu
        GameObject tipBox = new GameObject("TipBox");
        tipBox.transform.SetParent(contentObj.transform, false);
        RectTransform tipBoxRect = tipBox.AddComponent<RectTransform>();
        tipBoxRect.sizeDelta = new Vector2(660f, 65f);
        tipBoxRect.anchoredPosition = new Vector2(0f, -165f);
        Image tipBg = tipBox.AddComponent<Image>();
        tipBg.color = new Color(0.10f, 0.16f, 0.22f, 0.85f);
        Outline tipOutline = tipBox.AddComponent<Outline>();
        tipOutline.effectColor = new Color(0.35f, 0.85f, 0.65f, 0.5f);

        tipText = CreateText(tipBox.transform, "TipText", EcoTips[0], 15f, Vector2.zero, new Color(0.90f, 0.98f, 0.92f), FontStyles.Normal);
        tipText.rectTransform.sizeDelta = new Vector2(630f, 55f);
    }

    private void Update()
    {
        if (isFadingOut) return;

        // İpucu döngüsü
        tipTimer += Time.unscaledDeltaTime;
        if (tipTimer >= 2.6f)
        {
            tipTimer = 0f;
            currentTipIndex = (currentTipIndex + 1) % EcoTips.Length;
            if (tipText != null) tipText.text = EcoTips[currentTipIndex];
        }

        // Progress bar dolgusuna hafif nabız (pulse) efekti
        if (progressBar != null && progressBar.fillRect != null)
        {
            Image fillImg = progressBar.fillRect.GetComponent<Image>();
            if (fillImg != null)
            {
                float pulse = 0.88f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.12f;
                fillImg.color = new Color(0.24f * pulse, 0.88f * pulse, 0.52f, 1f);
            }
        }
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, Vector2 pos, Color color, FontStyles style)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(650f, 50f);
        rt.anchoredPosition = pos;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = style;
        return tmp;
    }

    public void SetProgress(float progress01, string status)
    {
        if (progressBar != null) progressBar.value = Mathf.Clamp01(progress01);
        if (statusText != null && !string.IsNullOrEmpty(status)) statusText.text = status;
        if (percentText != null) percentText.text = $"%{Mathf.RoundToInt(Mathf.Clamp01(progress01) * 100f)}";
    }

    public void DismissScreen(float fadeDuration = 0.75f)
    {
        if (isFadingOut) return;
        StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        isFadingOut = true;
        SetProgress(1f, "Ekosistem Hazır!");

        yield return new WaitForSeconds(0.15f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            }
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        Destroy(gameObject);
    }
}

