using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun topladığı atıkları, eko-icatları ve çanta kapasitesini yöneten envanter sistemi.
/// - Toplam çanta kapasitesi (Backpack Capacity) ve tür başına istif limiti barındırır.
/// - Çanta dolduğunda atıkların gereksiz çekilmesini ve birikmesini önleyen kontroller içerir.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("--- ÇANTA KAPASİTESİ VE SINIRLAR ---")]
    [Tooltip("Ham atıklar (Hurda, Plastik, Lastik, Elektronik, Biyo-Atık) için toplam sırt çantası taşıma kapasitesi")]
    [SerializeField] private int backpackCapacity = 25;

    [Tooltip("Her bir eşya türünden maksimum taşınabilecek istif sınırı")]
    [SerializeField] private int maxStackPerItem = 99;

    [Header("--- EKONOMİ / PARA (EKO-KREDİ) ---")]
    [Tooltip("Oyuncunun alışveriş ve inşaat için kullanacağı para birimi")]
    [SerializeField] private int coins = 50;

    [Header("--- MEVCUT ENVANTER (SALT OKUNUR / TEST İÇİN) ---")]
    [SerializeField] private int scrapMetalCount;
    [SerializeField] private int plasticWasteCount;
    [SerializeField] private int organicWasteCount;
    [SerializeField] private int rubberTireCount;
    [SerializeField] private int electronicWasteCount;
    [Space(5)]
    [SerializeField] private int steelPlateCount;
    [SerializeField] private int polymerBlockCount;
    [SerializeField] private int superCompostCount;
    [Space(5)]
    [SerializeField] private int repairKitCount;
    [SerializeField] private int carbonFilterCount;
    [SerializeField] private int riverBuoyCount;
    [SerializeField] private int cleanEnergyGeneratorCount;

    private readonly Dictionary<ItemType, int> items = new Dictionary<ItemType, int>();

    /// <summary>
    /// Envanter değiştiğinde tetiklenir (Eşya Türü, Yeni Miktar).
    /// </summary>
    public event Action<ItemType, int> OnInventoryChanged;

    /// <summary>
    /// Ekranda bilgi/uyarı mesajı göstermek için tetiklenir.
    /// </summary>
    public event Action<string> OnInventoryNotification;

    public int BackpackCapacity => backpackCapacity;
    public int MaxStackPerItem => maxStackPerItem;
    public int Coins => coins;

    /// <summary>
    /// Para miktarı değiştiğinde tetiklenir (Yeni Bakiye).
    /// </summary>
    public event Action<int> OnCoinsChanged;

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        OnCoinsChanged?.Invoke(coins);
        Notify($"+{amount} Eko-Kredi (Bakiye: {coins})");
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (coins < amount)
        {
            Notify($"Yetersiz Eko-Kredi! Gereken: {amount} (Mevcut: {coins})");
            return false;
        }

        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        Notify($"-{amount} Eko-Kredi (Kalan: {coins})");
        return true;
    }

    /// <summary>
    /// Çantadaki toplam ham atık sayısı (Hurda + Plastik + Biyo-Atık + Lastik + Elektronik).
    /// </summary>
    public int TotalWasteCount =>
        GetCount(ItemType.ScrapMetal) +
        GetCount(ItemType.PlasticWaste) +
        GetCount(ItemType.OrganicWaste) +
        GetCount(ItemType.RubberTire) +
        GetCount(ItemType.ElectronicWaste);

    /// <summary>
    /// Çanta ham atık kapasitesine ulaştı mı?
    /// </summary>
    public bool IsBackpackFull => TotalWasteCount >= backpackCapacity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (backpackCapacity < 25)
        {
            backpackCapacity = 25;
        }

        foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
        {
            items[type] = 0;
        }

        SyncFromInspector();
    }

    private void SyncFromInspector()
    {
        items[ItemType.ScrapMetal] = scrapMetalCount;
        items[ItemType.PlasticWaste] = plasticWasteCount;
        items[ItemType.OrganicWaste] = organicWasteCount;
        items[ItemType.RubberTire] = rubberTireCount;
        items[ItemType.ElectronicWaste] = electronicWasteCount;
        items[ItemType.SteelPlate] = steelPlateCount;
        items[ItemType.PolymerBlock] = polymerBlockCount;
        items[ItemType.SuperCompost] = superCompostCount;
        items[ItemType.RepairKit] = repairKitCount;
        items[ItemType.CarbonFilter] = carbonFilterCount;
        items[ItemType.RiverCleanerBuoy] = riverBuoyCount;
        items[ItemType.CleanEnergyGenerator] = cleanEnergyGeneratorCount;
    }

    private void SyncToInspector()
    {
        scrapMetalCount = GetCount(ItemType.ScrapMetal);
        plasticWasteCount = GetCount(ItemType.PlasticWaste);
        organicWasteCount = GetCount(ItemType.OrganicWaste);
        rubberTireCount = GetCount(ItemType.RubberTire);
        electronicWasteCount = GetCount(ItemType.ElectronicWaste);
        steelPlateCount = GetCount(ItemType.SteelPlate);
        polymerBlockCount = GetCount(ItemType.PolymerBlock);
        superCompostCount = GetCount(ItemType.SuperCompost);
        repairKitCount = GetCount(ItemType.RepairKit);
        carbonFilterCount = GetCount(ItemType.CarbonFilter);
        riverBuoyCount = GetCount(ItemType.RiverCleanerBuoy);
        cleanEnergyGeneratorCount = GetCount(ItemType.CleanEnergyGenerator);
    }

    /// <summary>
    /// Çantada belirtilen eşya için yeterli yer olup olmadığını kontrol eder.
    /// Ham atıklar için toplam çanta kapasitesini de hesaba katar.
    /// </summary>
    public bool CanAddItem(ItemType type, int amount = 1)
    {
        if (amount <= 0) return true;

        // Ham atık kontrolü: Toplam çanta kapasitesini aşamaz
        if (IsRawWaste(type))
        {
            if (TotalWasteCount + amount > backpackCapacity)
                return false;
        }

        return GetCount(type) + amount <= maxStackPerItem;
    }

    /// <summary>
    /// Envantere eşya ekler. Kapasite doluysa false döner.
    /// </summary>
    public bool AddItem(ItemType type, int amount = 1)
    {
        if (amount <= 0) return false;

        if (!CanAddItem(type, amount))
        {
            if (IsRawWaste(type) && TotalWasteCount + amount > backpackCapacity)
            {
                Notify($"Çanta Dolu! ({TotalWasteCount}/{backpackCapacity}) [Atölyede İcat Üret veya Yükselt]");
            }
            else
            {
                Notify($"{ItemDatabase.GetDisplayName(type)} istif sınırı dolu! (Maks: {maxStackPerItem})");
            }
            return false;
        }

        int current = GetCount(type);
        int newTotal = current + amount;
        items[type] = newTotal;
        SyncToInspector();

        OnInventoryChanged?.Invoke(type, newTotal);
        Notify($"+{amount} {ItemDatabase.GetDisplayName(type)}");
        return true;
    }

    /// <summary>
    /// Belirtilen eşyadan yeterli miktarda var mı kontrol eder.
    /// </summary>
    public bool HasItem(ItemType type, int requiredAmount = 1)
    {
        return GetCount(type) >= requiredAmount;
    }

    /// <summary>
    /// Eşyayı harcar. Yeterli miktarda yoksa false döner.
    /// </summary>
    public bool TrySpendItem(ItemType type, int amount = 1)
    {
        if (!HasItem(type, amount))
            return false;

        int newTotal = GetCount(type) - amount;
        items[type] = newTotal;
        SyncToInspector();

        OnInventoryChanged?.Invoke(type, newTotal);
        return true;
    }

    /// <summary>
    /// TrySpendItem ile aynı işlevi gören kısayol metodu.
    /// </summary>
    public bool RemoveItem(ItemType type, int amount = 1)
    {
        return TrySpendItem(type, amount);
    }

    /// <summary>
    /// İki farklı malzeme gerektiren üretim tarifleri için harcama ve ürün ekleme yapar.
    /// </summary>
    public bool TryCraftRecipe(ItemType resultItem, ItemType ingredient1, int count1, ItemType ingredient2, int count2)
    {
        if (!HasItem(ingredient1, count1) || !HasItem(ingredient2, count2))
        {
            Notify($"Yetersiz malzeme! Gereken: {count1} {ItemDatabase.GetDisplayName(ingredient1)} + {count2} {ItemDatabase.GetDisplayName(ingredient2)}");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(null, "YETERSİZ MALZEME!");
            return false;
        }

        TrySpendItem(ingredient1, count1);
        if (count2 > 0) TrySpendItem(ingredient2, count2);

        AddItem(resultItem, 1);
        Notify($"Üretildi: {ItemDatabase.GetDisplayName(resultItem)}!");
        GameAudioAndJuiceManager.Instance?.TriggerCraftOrUpgradeOomph(ItemDatabase.GetDisplayName(resultItem));
        return true;
    }

    /// <summary>
    /// Önce 2. kademe işlenmiş ara ürünlerle (Çelik Plaka, Polimer Blok, Süper Kompost),
    /// yoksa doğrudan 1. kademe ham maddelerle üretim yapmayı dener.
    /// </summary>
    public bool TryCraftFlexible(
        ItemType resultItem,
        ItemType intermediate1, int intCount1,
        ItemType intermediate2, int intCount2,
        ItemType raw1, int rawCount1,
        ItemType raw2, int rawCount2)
    {
        bool canUseIntermediates = (intCount1 > 0 || intCount2 > 0) &&
                                   (intCount1 <= 0 || HasItem(intermediate1, intCount1)) &&
                                   (intCount2 <= 0 || HasItem(intermediate2, intCount2));

        if (canUseIntermediates)
        {
            if (intCount1 > 0) TrySpendItem(intermediate1, intCount1);
            if (intCount2 > 0) TrySpendItem(intermediate2, intCount2);
            AddItem(resultItem, 1);
            Notify($"Üretildi: {ItemDatabase.GetDisplayName(resultItem)}! (İşlenmiş Ara Ürünlerle)");
            return true;
        }

        bool canUseRaw = (rawCount1 > 0 || rawCount2 > 0) &&
                         (rawCount1 <= 0 || HasItem(raw1, rawCount1)) &&
                         (rawCount2 <= 0 || HasItem(raw2, rawCount2));

        if (canUseRaw)
        {
            if (rawCount1 > 0) TrySpendItem(raw1, rawCount1);
            if (rawCount2 > 0) TrySpendItem(raw2, rawCount2);
            AddItem(resultItem, 1);
            Notify($"Üretildi: {ItemDatabase.GetDisplayName(resultItem)}!");
            return true;
        }

        Notify($"Yetersiz malzeme! ({ItemDatabase.GetDisplayName(resultItem)})");
        return false;
    }

    public int GetCount(ItemType type)
    {
        return items.TryGetValue(type, out int count) ? count : 0;
    }

    public void SetBackpackCapacity(int newCapacity)
    {
        backpackCapacity = Mathf.Max(5, newCapacity);
        OnInventoryChanged?.Invoke(ItemType.ScrapMetal, GetCount(ItemType.ScrapMetal));
        Notify($"Sırt Çantası Genişletildi! Yeni Kapasite: {backpackCapacity}");
    }

    public void Notify(string message)
    {
        OnInventoryNotification?.Invoke(message);
    }

    public static bool IsRawWaste(ItemType type)
    {
        return type == ItemType.ScrapMetal ||
               type == ItemType.PlasticWaste ||
               type == ItemType.OrganicWaste;
    }
}
