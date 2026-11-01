using UnityEngine;

/// <summary>
/// Fabrika Bölgesi Ana Geri Dönüşüm Tesisi (FactoryRecyclingHub - RecyclingBuilding.vox).
/// - Fabrika bacalarına Baca Filtresi (AirFilter.vox) takıldıkça aktifleşir ve güçlenir.
/// - Aktif olduğunda oyuncuya:
///    1. Toplu Endüstriyel Üretim (Ham maddeleri yüksek verimle Çelik Plaka / Polimer Blok / Süper Kompost'a ve Eko-Kredi'ye çevirme)
///    2. Periyodik Sanayi Sevkiyat Ödülü (+1 Çelik Plaka, +1 Polimer Blok, +20 Eko-Kredi)
///    3. Temiz Enerji Jeneratörü kurulum desteği sunar.
/// </summary>
public class FactoryRecyclingHub : MonoBehaviour, IInteractable
{
    [Header("--- MERKEZ TESİS AYARLARI ---")]
    [SerializeField] private float passiveShipmentInterval = 18f;

    private float shipmentTimer;
    private bool hasShipmentReady = true;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(4.5f, 3.5f, 4.5f);
            bc.center = new Vector3(0f, 1.75f, 0f);
        }
    }

    private void Update()
    {
        bool operational = EcoSystemManager.Instance != null && EcoSystemManager.Instance.IsFactoryHubOperational;
        if (!operational) return;

        if (!hasShipmentReady)
        {
            float eff = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.FactoryEfficiencyMultiplier : 1f;
            shipmentTimer += Time.deltaTime * eff;
            if (shipmentTimer >= passiveShipmentInterval)
            {
                shipmentTimer = 0f;
                hasShipmentReady = true;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        bool operational = EcoSystemManager.Instance != null && EcoSystemManager.Instance.IsFactoryHubOperational;
        if (!operational)
        {
            return "Merkez Geri Dönüşüm Tesisi [KAPALI - Aktif Etmek İçin Fabrika Bacasına AirFilter (Baca Filtresi) Tak!]";
        }

        var inv = PlayerInventory.Instance;
        if (inv != null && inv.HasItem(ItemType.CleanEnergyGenerator, 1))
        {
            return "[E] Merkez Geri Dönüşüm Tesisi: Temiz Enerji Jeneratörünü Şebekeye Bağla (+120 Eko-Kredi & +25 İtibar)";
        }

        if (hasShipmentReady)
        {
            return "[E] Merkez Geri Dönüşüm Tesisi: Sanayi Sevkiyatını Al (+1 Çelik Plaka, +1 Polimer Blok, +25 Eko-Kredi) & Toplu İşle";
        }

        return "[E] Merkez Geri Dönüşüm Tesisi: Ham Maddeleri Toplu İşle & Eko-Kredi İhracatı Yap";
    }

    public void Interact(GameObject interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        bool operational = EcoSystemManager.Instance != null && EcoSystemManager.Instance.IsFactoryHubOperational;
        if (!operational)
        {
            inv.Notify("Merkez Tesis yoğun duman yüzünden kilitli! Önce yakındaki Fabrika Bacasına [Baca Filtresi] takmalısın.");
            return;
        }

        // 1. Temiz Enerji Jeneratörü varsa kur
        if (inv.TrySpendItem(ItemType.CleanEnergyGenerator, 1))
        {
            inv.AddCoins(120);
            EcoSystemManager.Instance?.RegisterCleanEnergyInstalled();
            return;
        }

        bool didAnything = false;

        // 2. Hazır sanayi sevkiyatı varsa ver
        if (hasShipmentReady)
        {
            hasShipmentReady = false;
            shipmentTimer = 0f;
            inv.AddItem(ItemType.SteelPlate, 1);
            inv.AddItem(ItemType.PolymerBlock, 1);
            inv.AddCoins(25);
            didAnything = true;
        }

        // 3. Çantadaki ham maddeleri toplu olarak ara ürüne + Eko-Kredi'ye dönüştür
        int batchCount = 0;
        while (inv.HasItem(ItemType.ScrapMetal, 2) && batchCount < 4)
        {
            inv.TrySpendItem(ItemType.ScrapMetal, 2);
            inv.AddItem(ItemType.SteelPlate, 1);
            inv.AddCoins(20);
            batchCount++;
        }

        while (inv.HasItem(ItemType.PlasticWaste, 2) && batchCount < 8)
        {
            inv.TrySpendItem(ItemType.PlasticWaste, 2);
            inv.AddItem(ItemType.PolymerBlock, 1);
            inv.AddCoins(20);
            batchCount++;
        }

        while (inv.HasItem(ItemType.OrganicWaste, 2) && batchCount < 12)
        {
            inv.TrySpendItem(ItemType.OrganicWaste, 2);
            inv.AddItem(ItemType.SuperCompost, 1);
            inv.AddCoins(20);
            batchCount++;
        }

        if (batchCount > 0)
        {
            inv.Notify($"Merkez Tesiste {batchCount} parti ham madde toplu işlendi! (+Ara Ürünler & +{batchCount * 20} Eko-Kredi)");
        }
        else if (didAnything)
        {
            inv.Notify("Merkez Tesis Sanayi Sevkiyatı alındı: +1 Çelik Plaka, +1 Polimer Blok ve +25 Eko-Kredi!");
        }
        else
        {
            int pct = Mathf.RoundToInt((shipmentTimer / Mathf.Max(1f, passiveShipmentInterval)) * 100f);
            inv.Notify($"Toplu işleme için en az 2 Hurda/Plastik/Biyo-Madde getir! (Yeni Sanayi Sevkiyatı: %{pct})");
        }
    }

    public Vector3 GetPosition() => transform.position;
}

