using System;
using UnityEngine;

/// <summary>
/// Eko-Mucit Karakter ve Ekipman Geliştirme (Mini-Progression) Sistemi.
/// - Vakum/Mıknatıs aletini geliştirir (Menzil ve Çekim Hızı artar).
/// - Sırt çantasını büyütür (Taşıma kapasitesi artar).
/// - Hareket botlarını güçlendirir (Koşu hızı artar).
/// - Hurdalık Atölyesi (Workbench) üzerinden malzemelerle yükseltilebilir.
/// </summary>
public class PlayerProgression : MonoBehaviour
{
    public static PlayerProgression Instance { get; private set; }

    [Header("--- EKİPMAN SEVİYELERİ ---")]
    [Tooltip("Vakum cihazı açıldı mı? (Başlangıçta false: Çöp Toplama Çubuğu kullanılır)")]
    [SerializeField] private bool hasVacuumUnlocked = false;

    [Tooltip("Vakum/Mıknatıs Aleti Seviyesi (0: Kilitli, 1: Prototip, 2: Siklonik, 3: Manyetik Vorteks)")]
    [Range(0, 3)]
    [SerializeField] private int vacuumLevel = 0;

    [Tooltip("Sırt Çantası Seviyesi (1 - 4)")]
    [Range(1, 4)]
    [SerializeField] private int backpackLevel = 1;

    [Tooltip("Eko-Koşu Botları Seviyesi (1 - 3)")]
    [Range(1, 3)]
    [SerializeField] private int bootsLevel = 1;

    public bool HasVacuumUnlocked => hasVacuumUnlocked;
    public int VacuumLevel => vacuumLevel;
    public int BackpackLevel => backpackLevel;
    public int BootsLevel => bootsLevel;

    public event Action OnProgressionUpdated;

    private PlayerInteractionController interactionController;
    private PlayerInventory inventory;
    private CharacterController25D characterController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        CacheComponents();
        ApplyAllStats();
    }

    private void CacheComponents()
    {
        if (interactionController == null)
            interactionController = GetComponent<PlayerInteractionController>() ?? FindFirstObjectByType<PlayerInteractionController>();

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>() ?? PlayerInventory.Instance ?? FindFirstObjectByType<PlayerInventory>();

        if (characterController == null)
            characterController = GetComponent<CharacterController25D>() ?? FindFirstObjectByType<CharacterController25D>();
    }

    /// <summary>
    /// Mevcut seviyelere göre karakter ve ekipman parametrelerini günceller.
    /// </summary>
    public void ApplyAllStats()
    {
        CacheComponents();

        // 1. Vakum İstatistikleri
        float vRadius = hasVacuumUnlocked ? 5.0f : 0f;
        float vSpeed = hasVacuumUnlocked ? 6.5f : 0f;

        if (vacuumLevel == 2)
        {
            vRadius = 7.5f;
            vSpeed = 10.0f;
        }
        else if (vacuumLevel >= 3)
        {
            vRadius = 11.0f;
            vSpeed = 15.0f;
        }

        if (interactionController != null)
        {
            interactionController.SetVacuumStats(vRadius, vSpeed);
        }

        var anim = GetComponent<VoxelCharacterAnimator>() ?? FindFirstObjectByType<VoxelCharacterAnimator>();
        if (anim != null)
        {
            anim.SetEquipmentVisualMode(hasVacuumUnlocked, interactionController != null && interactionController.IsVacuumActive);
        }

        // 2. Çanta Kapasitesi (Dengelenmiş Kapasite: 25 -> 45 -> 70 -> 100)
        int bCapacity = 25;
        if (backpackLevel == 2) bCapacity = 45;
        else if (backpackLevel == 3) bCapacity = 70;
        else if (backpackLevel >= 4) bCapacity = 100;

        if (inventory != null)
        {
            inventory.SetBackpackCapacity(bCapacity);
        }

        // 3. Bot / Hız Çarpanı (1.0x -> 1.25x -> 1.50x)
        float speedMult = 1.0f;
        if (bootsLevel == 2) speedMult = 1.25f;
        else if (bootsLevel >= 3) speedMult = 1.50f;

        if (characterController != null)
        {
            characterController.SetSpeedMultiplier(speedMult);
        }

        OnProgressionUpdated?.Invoke();
    }

    // --- VAKUM YÜKSELTME VE KİLİT AÇMA ---
    public bool CanUpgradeVacuum()
    {
        if (vacuumLevel >= 3 || inventory == null) return false;

        if (!hasVacuumUnlocked || vacuumLevel == 0)
        {
            // Vakum Üretme / Açma (3 Kat Dengelendi): 3 Çelik Plaka + 3 Polimer Blok + 2 Elektronik Atık (veya 18 Hurda + 18 Plastik + 2 Elektronik)
            bool adv = inventory.HasItem(ItemType.SteelPlate, 3) && inventory.HasItem(ItemType.PolymerBlock, 3) && inventory.HasItem(ItemType.ElectronicWaste, 2);
            bool raw = inventory.HasItem(ItemType.ScrapMetal, 18) && inventory.HasItem(ItemType.PlasticWaste, 18) && inventory.HasItem(ItemType.ElectronicWaste, 2);
            return adv || raw;
        }
        else if (vacuumLevel == 1)
        {
            // Seviye 2 Gereksinimi: 6 Çelik Plaka + 6 Polimer Blok + 4 Elektronik Atık + 150 Eko-Kredi
            return inventory.HasItem(ItemType.SteelPlate, 6) && inventory.HasItem(ItemType.PolymerBlock, 6) &&
                   inventory.HasItem(ItemType.ElectronicWaste, 4) && inventory.Coins >= 150;
        }
        else if (vacuumLevel == 2)
        {
            // Seviye 3 Gereksinimi: 10 Çelik Plaka + 10 Polimer Blok + 2 Baca Filtresi + 250 Eko-Kredi
            return inventory.HasItem(ItemType.SteelPlate, 10) && inventory.HasItem(ItemType.PolymerBlock, 10) &&
                   inventory.HasItem(ItemType.CarbonFilter, 2) && inventory.Coins >= 250;
        }

        return false;
    }

    public bool TryUpgradeVacuum()
    {
        if (vacuumLevel >= 3)
        {
            inventory?.Notify("Vakum zaten maksimum seviyede!");
            return false;
        }

        if (!CanUpgradeVacuum())
        {
            inventory?.Notify($"Yetersiz malzeme veya kredi! {GetVacuumCostInfo()}");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(null, "YETERSİZ MALZEME!");
            return false;
        }

        if (!hasVacuumUnlocked || vacuumLevel == 0)
        {
            if (inventory.HasItem(ItemType.SteelPlate, 3) && inventory.HasItem(ItemType.PolymerBlock, 3))
            {
                inventory.TrySpendItem(ItemType.SteelPlate, 3);
                inventory.TrySpendItem(ItemType.PolymerBlock, 3);
                inventory.TrySpendItem(ItemType.ElectronicWaste, 2);
            }
            else
            {
                inventory.TrySpendItem(ItemType.ScrapMetal, 18);
                inventory.TrySpendItem(ItemType.PlasticWaste, 18);
                inventory.TrySpendItem(ItemType.ElectronicWaste, 2);
            }

            hasVacuumUnlocked = true;
            vacuumLevel = 1;
            inventory.Notify("Vakum Cihazı Üretildi: Prototip Hurda Emici (Sv.1)! [Sağ Tık / Space]");
        }
        else if (vacuumLevel == 1)
        {
            inventory.TrySpendItem(ItemType.SteelPlate, 6);
            inventory.TrySpendItem(ItemType.PolymerBlock, 6);
            inventory.TrySpendItem(ItemType.ElectronicWaste, 4);
            inventory.TrySpendCoins(150);
            vacuumLevel = 2;
            inventory.Notify("Vakum Yükseltildi: Siklonik Eko-Çekici! (Menzil: 7.5m, Hız: 10m/s)");
        }
        else if (vacuumLevel == 2)
        {
            inventory.TrySpendItem(ItemType.SteelPlate, 10);
            inventory.TrySpendItem(ItemType.PolymerBlock, 10);
            inventory.TrySpendItem(ItemType.CarbonFilter, 2);
            inventory.TrySpendCoins(250);
            vacuumLevel = 3;
            inventory.Notify("Vakum Maksimum Seviye: Manyetik Vorteks Türbini! (Menzil: 11m, Fırtına Çekişi!)");
        }

        ApplyAllStats();
        GameAudioAndJuiceManager.Instance?.TriggerCraftOrUpgradeOomph(GetVacuumTitle());
        return true;
    }

    public string GetVacuumCostInfo()
    {
        if (!hasVacuumUnlocked || vacuumLevel == 0) return "3 Çelik + 3 Polimer + 2 Elektronik (veya 18 Hurda+18 Plastik+2 Elektronik)";
        if (vacuumLevel == 1) return "6 Çelik + 6 Polimer + 4 Elektronik + 150 Kredi";
        if (vacuumLevel == 2) return "10 Çelik + 10 Polimer + 2 Filtre + 250 Kredi";
        return "Maksimum Seviye";
    }

    // --- ÇANTA YÜKSELTME ---
    public bool CanUpgradeBackpack()
    {
        if (backpackLevel >= 4 || inventory == null) return false;

        if (backpackLevel == 1)
        {
            // Seviye 2 Gereksinimi (3 Kat Dengelendi): 8 Hurda + 8 Plastik veya 50 Kredi
            return (inventory.HasItem(ItemType.ScrapMetal, 8) && inventory.HasItem(ItemType.PlasticWaste, 8)) || inventory.Coins >= 50;
        }
        else if (backpackLevel == 2)
        {
            // Seviye 3 Gereksinimi: 3 Çelik Plaka + 3 Polimer Blok (veya 18 Hurda + 18 Plastik + 100 Kredi)
            bool adv = inventory.HasItem(ItemType.SteelPlate, 3) && inventory.HasItem(ItemType.PolymerBlock, 3);
            bool raw = inventory.HasItem(ItemType.ScrapMetal, 18) && inventory.HasItem(ItemType.PlasticWaste, 18) && inventory.Coins >= 100;
            return adv || raw;
        }
        else if (backpackLevel == 3)
        {
            // Seviye 4 Gereksinimi: 6 Çelik Plaka + 6 Polimer Blok + 3 Süper Kompost + 200 Kredi
            return inventory.HasItem(ItemType.SteelPlate, 6) &&
                   inventory.HasItem(ItemType.PolymerBlock, 6) &&
                   inventory.HasItem(ItemType.SuperCompost, 3) &&
                   inventory.Coins >= 200;
        }

        return false;
    }

    public bool TryUpgradeBackpack()
    {
        if (backpackLevel >= 4)
        {
            inventory?.Notify("Çanta zaten maksimum boyutta!");
            return false;
        }

        if (!CanUpgradeBackpack())
        {
            inventory?.Notify($"Yetersiz malzeme! {GetBackpackCostInfo()}");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(null, "YETERSİZ MALZEME!");
            return false;
        }

        if (backpackLevel == 1)
        {
            if (inventory.HasItem(ItemType.ScrapMetal, 8) && inventory.HasItem(ItemType.PlasticWaste, 8))
            {
                inventory.TrySpendItem(ItemType.ScrapMetal, 8);
                inventory.TrySpendItem(ItemType.PlasticWaste, 8);
            }
            else
            {
                inventory.TrySpendCoins(50);
            }
            backpackLevel = 2;
            inventory.Notify("Çanta Yükseltildi: Genişletilmiş Eko-Çanta (Kapasite: 45 Yuva)!");
        }
        else if (backpackLevel == 2)
        {
            if (inventory.HasItem(ItemType.SteelPlate, 3) && inventory.HasItem(ItemType.PolymerBlock, 3))
            {
                inventory.TrySpendItem(ItemType.SteelPlate, 3);
                inventory.TrySpendItem(ItemType.PolymerBlock, 3);
            }
            else
            {
                inventory.TrySpendItem(ItemType.ScrapMetal, 18);
                inventory.TrySpendItem(ItemType.PlasticWaste, 18);
                inventory.TrySpendCoins(100);
            }
            backpackLevel = 3;
            inventory.Notify("Çanta Yükseltildi: Takviyeli Geri Dönüşüm Çantası (Kapasite: 70 Yuva)!");
        }
        else if (backpackLevel == 3)
        {
            inventory.TrySpendItem(ItemType.SteelPlate, 6);
            inventory.TrySpendItem(ItemType.PolymerBlock, 6);
            inventory.TrySpendItem(ItemType.SuperCompost, 3);
            inventory.TrySpendCoins(200);
            backpackLevel = 4;
            inventory.Notify("Çanta Maksimum Boyut: Kuantum Sıkıştırmalı Hazne (Kapasite: 100 Yuva)!");
        }

        ApplyAllStats();
        GameAudioAndJuiceManager.Instance?.TriggerCraftOrUpgradeOomph(GetBackpackTitle());
        return true;
    }

    public string GetBackpackCostInfo()
    {
        if (backpackLevel == 1) return "8 Hurda + 8 Plastik (veya 50 Kredi)";
        if (backpackLevel == 2) return "3 Çelik + 3 Polimer (veya 18 Hurda+18 Plastik+100 Kredi)";
        if (backpackLevel == 3) return "6 Çelik + 6 Polimer + 3 Kompost + 200 Kredi";
        return "Maksimum Seviye";
    }

    // --- EKO-KOŞU BOTLARI YÜKSELTME ---
    public bool CanUpgradeBoots()
    {
        if (bootsLevel >= 3 || inventory == null) return false;

        if (bootsLevel == 1)
        {
            return inventory.HasItem(ItemType.RubberTire, 8) && inventory.Coins >= 80;
        }
        else if (bootsLevel == 2)
        {
            return inventory.HasItem(ItemType.RubberTire, 16) && inventory.HasItem(ItemType.PolymerBlock, 3) && inventory.Coins >= 180;
        }
        return false;
    }

    public bool TryUpgradeBoots()
    {
        if (bootsLevel >= 3)
        {
            inventory?.Notify("Botlar zaten maksimum seviyede!");
            return false;
        }

        if (!CanUpgradeBoots())
        {
            inventory?.Notify($"Yetersiz malzeme! {GetBootsCostInfo()}");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(null, "YETERSİZ MALZEME!");
            return false;
        }

        if (bootsLevel == 1)
        {
            inventory.TrySpendItem(ItemType.RubberTire, 8);
            inventory.TrySpendCoins(80);
            bootsLevel = 2;
            inventory.Notify("Botlar Yükseltildi: Kauçuk Eko-Koşu Botları (+%25 Hız)!");
        }
        else if (bootsLevel == 2)
        {
            inventory.TrySpendItem(ItemType.RubberTire, 16);
            inventory.TrySpendItem(ItemType.PolymerBlock, 3);
            inventory.TrySpendCoins(180);
            bootsLevel = 3;
            inventory.Notify("Botlar Maksimum Seviye: Manyetik Süper Koşu Botları (+%50 Hız)!");
        }

        ApplyAllStats();
        GameAudioAndJuiceManager.Instance?.TriggerCraftOrUpgradeOomph(GetBootsTitle());
        return true;
    }

    public string GetBootsCostInfo()
    {
        if (bootsLevel == 1) return "8 Lastik + 80 Kredi";
        if (bootsLevel == 2) return "16 Lastik + 3 Polimer + 180 Kredi";
        return "Maksimum Seviye";
    }

    public string GetBootsTitle()
    {
        return bootsLevel switch
        {
            1 => "Standart Ayakkabı",
            2 => "Kauçuk Eko-Botlar (+%25 Hız)",
            _ => "Manyetik Süper Botlar (+%50 Hız)"
        };
    }

    public string GetVacuumTitle()
    {
        if (!hasVacuumUnlocked || vacuumLevel == 0) return "Kilitli (Çöp Çubuğu Kullanılıyor)";
        return vacuumLevel switch
        {
            1 => "Prototip Hurda Emici (Sv.1)",
            2 => "Siklonik Eko-Çekici (Sv.2)",
            _ => "Manyetik Vorteks Türbini (Sv.3)"
        };
    }

    public string GetBackpackTitle()
    {
        return backpackLevel switch
        {
            1 => "Standart Çanta (25 Yuva)",
            2 => "Genişletilmiş Çanta (45 Yuva)",
            3 => "Takviyeli Çanta (70 Yuva)",
            _ => "Kuantum Eko-Hazne (100 Yuva)"
        };
    }
}

