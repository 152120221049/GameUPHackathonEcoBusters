using System;
using UnityEngine;

/// <summary>
/// Ekolojik Sistem ve İklim Döngüsü Yöneticisi (Stardew + Factorio Ekosistemi).
/// - Anlık büyü yerine zamanla yayılan kademeli iklim/çevre iyileşmesini yönetir.
/// - Bölgesel kirlilik (Fabrika, Taşra/Tarım, Nehir) ve Genel Ada Eko-Puanını hesaplar.
/// - Oyuncuya düz para vermek yerine SİSTEMİK ÖDÜLLER sunar:
///    * Tarım İyileşmesi: Hasat ve biyo-hammadde üretim hızı %150-%250 artar.
///    * Fabrika İyileşmesi: Makineler daha verimli çalışır, işleme hızları iki katına çıkar.
///    * Şehir Desteği: Topluluk güveni ve takipçi/gönüllü NPC'lerin bize katılması tetiklenir.
/// </summary>
public class EcoSystemManager : MonoBehaviour
{
    private static EcoSystemManager instance;
    public static EcoSystemManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<EcoSystemManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("EcoSystemManager");
                    instance = go.AddComponent<EcoSystemManager>();
                }
            }
            return instance;
        }
    }

    [Header("--- GENEL ADA EKO-DURUMU ---")]
    [Tooltip("Genel Ada Sağlığı (0% = Tamamen Zehirli, 100% = Yemyeşil Doğa)")]
    [Range(0f, 100f)]
    [SerializeField] private float islandEcoScore = 20f;

    [Header("--- BÖLGESEL SAĞLIK SEVİYELERİ ---")]
    [Tooltip("Tarım Arazisi Verimliliği (0 = Çorak/Kurak, 100 = Zengin Toprak)")]
    [Range(0f, 100f)]
    [SerializeField] private float farmlandFertility = 30f;

    [Tooltip("Fabrika Hava Kalitesi (0 = Yoğun Zehirli Duman, 100 = Filtrelenmiş Temiz Enerji)")]
    [Range(0f, 100f)]
    [SerializeField] private float factoryAirQuality = 15f;

    [Tooltip("Nehir Temizlik Oranı (0 = Ağır Metal Balçığı, 100 = Berrak Su)")]
    [Range(0f, 100f)]
    [SerializeField] private float riverPurity = 25f;

    [Header("--- TOPLULUK VE TAKİPÇİ DESTEĞİ ---")]
    [Tooltip("Halkın Eko-Mucit'e olan inancı ve desteği (0-100)")]
    [Range(0f, 100f)]
    [SerializeField] private float communityReputation = 10f;

    [Tooltip("Bize katılan aktif takipçi/gönüllü sayısı")]
    [SerializeField] private int activeFollowersCount = 0;

    [Header("--- BÖLGESEL İLERLEME SAYAÇLARI ---")]
    [SerializeField] private int repairedFarmMachinesCount = 0;
    [SerializeField] private int filteredChimneysCount = 0;
    [SerializeField] private int deployedBuoysCount = 0;
    [SerializeField] private int cleanEnergyInstalledCount = 0;

    private float passiveTickTimer;

    // Sistemik Verimlilik Çarpanları
    public float CropYieldMultiplier => 1f + (farmlandFertility / 100f) * 1.5f; // 1.0x - 2.5x
    public float FactoryEfficiencyMultiplier => 1f + (factoryAirQuality / 100f) * 1.0f; // 1.0x - 2.0x
    public float GlobalEcoScore => islandEcoScore;
    public float FarmlandFertility => farmlandFertility;
    public float FactoryAirQuality => factoryAirQuality;
    public float RiverPurity => riverPurity;
    public float CommunityReputation => communityReputation;
    public int ActiveFollowersCount => activeFollowersCount;

    public int RepairedFarmMachinesCount => repairedFarmMachinesCount;
    public int FilteredChimneysCount => filteredChimneysCount;
    public int DeployedBuoysCount => deployedBuoysCount;
    public int CleanEnergyInstalledCount => cleanEnergyInstalledCount;
    public bool IsFarmlandUnlockedForPlayer => repairedFarmMachinesCount > 0 || farmlandFertility >= 45f;
    public bool IsFactoryHubOperational => filteredChimneysCount > 0 || factoryAirQuality >= 35f;

    // Canlı Bildirim Olayları
    public event Action<float> OnEcoScoreChanged;
    public event Action<float> OnReputationChanged;
    public event Action<string> OnDistrictRestored;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        RecalculateGlobalEcoScore();
    }

    private void Update()
    {
        // Zamanla yavaş ve doğal bir eko-döngü dengesi (statik ağaç sayacı ile 1 saniyede bir çalışır, FindObjectsByType çağrılmaz!)
        int treeCount = TreePlantation.ActiveTreeCount;
        if (treeCount > 0)
        {
            passiveTickTimer -= Time.deltaTime;
            if (passiveTickTimer <= 0f)
            {
                passiveTickTimer = 1.0f;
                float naturalHeal = treeCount * 0.15f;
                farmlandFertility = Mathf.Clamp(farmlandFertility + naturalHeal, 0f, 100f);
                factoryAirQuality = Mathf.Clamp(factoryAirQuality + naturalHeal * 0.5f, 0f, 100f);
                RecalculateGlobalEcoScore();
            }
        }
    }

    /// <summary>
    /// Fabrika bacasına filtre takıldığında çağrılır.
    /// </summary>
    public void RegisterChimneyFilterInstalled(Vector3 worldPos)
    {
        filteredChimneysCount++;
        factoryAirQuality = Mathf.Clamp(factoryAirQuality + 25f, 0f, 100f);
        communityReputation = Mathf.Clamp(communityReputation + 15f, 0f, 100f);
        RecalculateGlobalEcoScore();

        OnDistrictRestored?.Invoke("Fabrika bacasına eko-filtre takıldı! Duman temizleniyor ve Merkez Geri Dönüşüm Tesisi aktifleşti.");
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Notify("Fabrika Filtrelendi! Sanayi İşleme Hızı Arttı & Merkez Tesis Açıldı.");
        }
    }

    /// <summary>
    /// Taşraya veya tarlaya süper kompost uygulandığında çağrılır.
    /// </summary>
    public void RegisterSuperCompostApplied(Vector3 worldPos)
    {
        farmlandFertility = Mathf.Clamp(farmlandFertility + 25f, 0f, 100f);
        communityReputation = Mathf.Clamp(communityReputation + 15f, 0f, 100f);
        RecalculateGlobalEcoScore();

        OnDistrictRestored?.Invoke("Toprağa süper kompost aşılandı! Toprak canlanıyor ve mahsul bereketi arttı.");
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Notify("Toprak Canlandı! Tarım & Biyo-Madde Üretimi Arttı.");
        }
    }

    /// <summary>
    /// Çiftlikteki arızalı traktör veya değirmen tamir edildiğinde çağrılır.
    /// </summary>
    public void RegisterFarmMachineRepaired(string machineTitle)
    {
        repairedFarmMachinesCount++;
        farmlandFertility = Mathf.Clamp(farmlandFertility + 20f, 0f, 100f);
        communityReputation = Mathf.Clamp(communityReputation + 18f, 0f, 100f);
        RecalculateGlobalEcoScore();

        OnDistrictRestored?.Invoke($"{machineTitle} tamir edildi! Çiftçiler sürülmüş tarlaları ekime açtı ve ambar üretimi başladı.");
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Notify($"{machineTitle} Tamir Edildi! Sürülmüş Tarlalar Ekime Açıldı (+18 İtibar)");
        }
    }

    /// <summary>
    /// Nehir su kanalına temizleme dubası yerleştirildiğinde çağrılır.
    /// </summary>
    public void RegisterRiverCleanerDeployed(Vector3 worldPos)
    {
        deployedBuoysCount++;
        riverPurity = Mathf.Clamp(riverPurity + 25f, 0f, 100f);
        communityReputation = Mathf.Clamp(communityReputation + 15f, 0f, 100f);
        RecalculateGlobalEcoScore();

        OnDistrictRestored?.Invoke("Nehir temizleme dubası devreye girdi! Toksik balçık çözülüyor ve balıkçılık/biyo-noktalar açılıyor.");
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Notify("Nehir Berraklaştı! Duba Atık Yakalıyor & Kıyı Balık Noktaları Açıldı.");
        }
    }

    /// <summary>
    /// Şehir merkezine veya adaya Temiz Enerji (Güneş Paneli, Rüzgar Türbini, vb.) kurulduğunda çağrılır.
    /// </summary>
    public void RegisterCleanEnergyInstalled(string sourceName = "Temiz Enerji")
    {
        cleanEnergyInstalledCount++;
        factoryAirQuality = Mathf.Clamp(factoryAirQuality + 8f, 0f, 100f);
        farmlandFertility = Mathf.Clamp(farmlandFertility + 6f, 0f, 100f);
        riverPurity = Mathf.Clamp(riverPurity + 6f, 0f, 100f);
        communityReputation = Mathf.Clamp(communityReputation + 15f, 0f, 100f);
        RecalculateGlobalEcoScore();

        OnDistrictRestored?.Invoke($"{sourceName} kuruldu! Ada temiz enerjiyle canlanıyor.");
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Notify($"{sourceName} Kuruldu! (+Ada Sağlığı, +İtibar)");
        }
    }

    public void ModifyEcoScore(float delta)
    {
        islandEcoScore = Mathf.Clamp(islandEcoScore + delta, 0f, 100f);
        OnEcoScoreChanged?.Invoke(islandEcoScore);
    }

    /// <summary>
    /// Fidan dikildiğinde veya büyüdüğünde ada sağlığını destekler.
    /// </summary>
    public void RegisterTreeGrowthBonus(float fertilityBonus, float airBonus)
    {
        farmlandFertility = Mathf.Clamp(farmlandFertility + fertilityBonus, 0f, 100f);
        factoryAirQuality = Mathf.Clamp(factoryAirQuality + airBonus, 0f, 100f);
        communityReputation = Mathf.Clamp(communityReputation + 2f, 0f, 100f);
        RecalculateGlobalEcoScore();
    }

    /// <summary>
    /// Yerden çöp veya atık toplandıkça adanın temizlik skoru azar azar artar.
    /// </summary>
    public void RegisterWasteCollected(ItemType type)
    {
        islandEcoScore = Mathf.Clamp(islandEcoScore + 0.15f, 0f, 100f);
        communityReputation = Mathf.Clamp(communityReputation + 0.2f, 0f, 100f);
        OnEcoScoreChanged?.Invoke(islandEcoScore);
        OnReputationChanged?.Invoke(communityReputation);
    }

    public void RegisterFollowerRecruited()
    {
        activeFollowersCount++;
        OnReputationChanged?.Invoke(communityReputation);
    }

    private void RecalculateGlobalEcoScore()
    {
        islandEcoScore = (farmlandFertility * 0.35f) + (factoryAirQuality * 0.35f) + (riverPurity * 0.30f);
        OnEcoScoreChanged?.Invoke(islandEcoScore);
        OnReputationChanged?.Invoke(communityReputation);
    }
}
