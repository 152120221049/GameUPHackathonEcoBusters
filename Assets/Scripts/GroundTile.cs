using System;
using UnityEngine;

/// <summary>
/// Zemin tipleri enum tanımı.
/// </summary>
public enum GroundType
{
    Empty,          // Boş / Harita dışı
    City,           // Şehir bölgesi
    Farmland,       // Taşra / Tarım arazisi
    Factory,        // Fabrika bölgesi
    Transition,     // Bozkır / Ara bağlantı zeminleri
    PlayerBase,     // Şehre yakın oyuncu üssü ve otomasyon alanı
    River,          // Nehir / Su kanalı (-1 kotu)
    Bridge,         // Nehir üzerindeki köprü geçişi (+1 kotu)
    BridgeRamp,     // Nehir kıyısındaki köprü rampası (0 -> +1 eğimi)
    Road            // Asfalt / Taş ana bağlantı yolu
}

/// <summary>
/// Haritada oluşturulan her bir tile hücresine eklenen bilgi bileşeni.
/// Dinamik kirlilik (Pollution), kot/yükseklik farkı, ağaç dikme, bina yerleştirme (Grid-Snap)
/// ve etkileşimler için temel oluşturur.
/// Kirlilik ve iyileşme geçişleri anlık değil, ekolojik gerçekliğe uygun olarak yavaş ve organik lerp ile gerçekleşir.
/// </summary>
[SelectionBase]
public class GroundTile : MonoBehaviour
{
    [Header("Tile Özellikleri")]
    public GroundType groundType;
    public Vector2Int gridCoordinate;
    [Tooltip("Bu karonun kot / yükseklik seviyesi (Örn: Nehir = -1, Zemin = 0, Tepe = +1)")]
    public float elevation = 0f;

    [Tooltip("Bu tile üzerine ağaç dikilebilir veya otomasyon kurulabilir mi?")]
    public bool isBuildable = true;

    [Tooltip("Bu tile üzerinde şu an bir nesne (bina, ağaç, makine, çöp vb.) var mı?")]
    public bool isOccupied = false;

    [Header("--- DİNAMİK KİRLİLİK VE DOĞAL İYİLEŞME ---")]
    [Tooltip("Mevcut kirlilik seviyesi (0 = Temiz, 100 = Aşırı Kirlenmiş / Zehirli)")]
    [Range(0f, 100f)]
    public float pollutionLevel = 0f;

    [Tooltip("Karonun varmaya çalıştığı hedef kirlilik")]
    [Range(0f, 100f)]
    public float targetPollutionLevel = 0f;

    [Tooltip("Doğal iyileşme/kirlenme geçiş hızı (birim/saniye)")]
    [SerializeField] private float transitionSpeed = 12f;

    public bool IsPolluted => pollutionLevel >= 50f;

    private Renderer tileRenderer;
    private Color originalColor = Color.white;
    private bool hasColorProperty;
    private int colorPropertyId;
    private MaterialPropertyBlock propBlock;
    private float lastAppliedPollution = -1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    // Bölgeye özel zehirli tonlar
    private static readonly Color DefaultPollutedColor = new Color(0.28f, 0.22f, 0.16f); // Zehirli toprak
    private static readonly Color RiverPollutedColor = new Color(0.35f, 0.42f, 0.18f);   // Çamurlu zehirli nehir
    private static readonly Color FactoryPollutedColor = new Color(0.20f, 0.18f, 0.18f); // İs bağlamış fabrika zemini

    public void Initialize(GroundType type, Vector2Int coordinate, bool buildable, float tileElevation = 0f)
    {
        groundType = type;
        gridCoordinate = coordinate;
        isBuildable = buildable;
        elevation = tileElevation;
        isOccupied = false;
        hasColorProperty = false;

        tileRenderer = GetComponent<Renderer>();
        if (tileRenderer == null)
            tileRenderer = GetComponentInChildren<Renderer>();

        if (tileRenderer != null && tileRenderer.sharedMaterial != null)
        {
            Material sharedMat = tileRenderer.sharedMaterial;
            if (sharedMat.HasProperty(BaseColorId))
            {
                hasColorProperty = true;
                colorPropertyId = BaseColorId;
                originalColor = sharedMat.GetColor(BaseColorId);
            }
            else if (sharedMat.HasProperty(LegacyColorId))
            {
                hasColorProperty = true;
                colorPropertyId = LegacyColorId;
                originalColor = sharedMat.GetColor(LegacyColorId);
            }
        }

        // Fabrika zemini mantıksal olarak yüksek kirlilikle başlar
        if (groundType == GroundType.Factory)
        {
            pollutionLevel = 75f;
            targetPollutionLevel = 75f;
        }
        else if (groundType == GroundType.River)
        {
            pollutionLevel = 50f; // Nehir başlangıçta hafif bulanık
            targetPollutionLevel = 50f;
        }
        else
        {
            pollutionLevel = 0f;
            targetPollutionLevel = 0f;
        }

        lastAppliedPollution = -1f;
        UpdatePollutionVisuals(instant: true);
    }

    private void Update()
    {
        // Hedefe doğru yavaş ve organik geçiş (iklim/çevre anında değişmez)
        if (Mathf.Abs(pollutionLevel - targetPollutionLevel) > 0.1f)
        {
            pollutionLevel = Mathf.MoveTowards(pollutionLevel, targetPollutionLevel, transitionSpeed * Time.deltaTime);
            UpdatePollutionVisuals(instant: false);
        }
    }

    /// <summary>
    /// Kirlilik ekler veya temizler (negatif değer temizler, geçiş yavaş olur)
    /// </summary>
    public void ModifyPollution(float amount)
    {
        targetPollutionLevel = Mathf.Clamp(targetPollutionLevel + amount, 0f, 100f);
    }

    /// <summary>
    /// Kirlilik seviyesini hedefler (instant = true ise anında uygular, false ise organik lerp)
    /// </summary>
    public void SetPollution(float level, bool instant = false)
    {
        targetPollutionLevel = Mathf.Clamp(level, 0f, 100f);
        if (instant)
        {
            pollutionLevel = targetPollutionLevel;
            UpdatePollutionVisuals(instant: true);
        }
    }

    private void UpdatePollutionVisuals(bool instant = false)
    {
        if (tileRenderer == null || !hasColorProperty) return;

        // Performans optimizasyonu: Sadece renk farkı anlamlıysa MaterialPropertyBlock uygula
        if (!instant && Mathf.Abs(pollutionLevel - lastAppliedPollution) < 0.8f) return;
        lastAppliedPollution = pollutionLevel;

        if (propBlock == null)
            propBlock = new MaterialPropertyBlock();

        float t = pollutionLevel / 100f;
        Color dirtyColor = groundType switch
        {
            GroundType.River => RiverPollutedColor,
            GroundType.Factory => FactoryPollutedColor,
            _ => DefaultPollutedColor
        };

        Color currentColor = Color.Lerp(originalColor, dirtyColor, t);

        tileRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(colorPropertyId, currentColor);
        tileRenderer.SetPropertyBlock(propBlock);
    }
}
