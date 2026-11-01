using UnityEngine;

/// <summary>
/// Eko-Mucit Tam Ekonomi Eşya Türleri (3 Kademeli Üretim Zinciri).
/// 1. Kademe (Ham Maddeler): Metal Hurda, Plastik Atık, Biyo-Hammadde
/// 2. Kademe (Ara Ürünler): Çelik Plaka, Polimer Blok, Süper Kompost
/// 3. Kademe (İleri Seviye Eko-İcatlar): Tamir Kiti, Baca Filtresi, Temizleme Dubası, Temiz Enerji Jeneratörü
/// </summary>
public enum ItemType
{
    // --- 1. KADEME: TOPLANABİLİR HAM MADDELER ---
    ScrapMetal = 0,         // Metal Hurda (Fabrika & Şehir)
    PlasticWaste = 1,       // Plastik Şişe / Atık (Nehir & Sokaklar)
    OrganicWaste = 2,       // Biyo-Hammadde / Tohum Özü (Tarla, Ağaçlar & Balık Noktaları)
    RubberTire = 3,         // Eski Araba Lastiği / Kauçuk (Tire.vox - Şehir ve Sanayi yolları)
    ElectronicWaste = 4,    // Elektronik Atık / Devre Kartı (Electronicwaste.vox - Fabrika & Şehir Binaları)

    // --- 2. KADEME: İŞLENMİŞ ARA ÜRÜNLER & EKO-İCATLAR ---
    CarbonFilter = 10,          // Endüstriyel Baca Filtresi (AirFilter.vox - Fabrika Bacaları için)
    SuperCompost = 11,          // Süper Biyo-Kompost (Tarla İstasyonları ve Fidanlar için)
    RiverCleanerBuoy = 12,      // Nehir Arıtma Dubası (Buoy.vox - Nehir Kanalları için)
    SteelPlate = 13,            // Çelik Plaka (Metal Hurda'nın preslenmiş ara ürünü)
    PolymerBlock = 14,          // Polimer Blok (Plastik Atık'ın preslenmiş ara ürünü)
    RepairKit = 15,             // Makine Tamir Kiti (Çiftlikteki Traktör ve Değirmenleri onarmak için)
    CleanEnergyGenerator = 16   // Temiz Enerji Jeneratörü (TownHall ve Fabrika Merkez Tesisi için)
}

/// <summary>
/// Eşyaların Türkçe isimlerini ve arayüz renklerini tutan yardımcı veritabanı.
/// </summary>
public static class ItemDatabase
{
    public static string GetDisplayName(ItemType type)
    {
        switch (type)
        {
            case ItemType.ScrapMetal: return "Metal Hurda";
            case ItemType.PlasticWaste: return "Plastik Atık";
            case ItemType.OrganicWaste: return "Biyo-Hammadde";
            case ItemType.RubberTire: return "Eski Lastik";
            case ItemType.ElectronicWaste: return "Elektronik Atık";
            case ItemType.SteelPlate: return "Çelik Plaka";
            case ItemType.PolymerBlock: return "Polimer Blok";
            case ItemType.SuperCompost: return "Süper Kompost";
            case ItemType.RepairKit: return "Tamir Kiti";
            case ItemType.CarbonFilter: return "Baca Filtresi";
            case ItemType.RiverCleanerBuoy: return "Temizleme Dubası";
            case ItemType.CleanEnergyGenerator: return "Temiz Enerji Jeneratörü";
            default: return type.ToString();
        }
    }

    public static Color GetFallbackColor(ItemType type) => GetItemColor(type);

    public static Color GetItemColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.ScrapMetal: return new Color(0.75f, 0.45f, 0.3f);          // Paslı Bakır/Demir
            case ItemType.PlasticWaste: return new Color(0.25f, 0.75f, 0.95f);       // Parlak Camgöbeği Mavi
            case ItemType.OrganicWaste: return new Color(0.45f, 0.85f, 0.3f);        // Canlı Yeşil
            case ItemType.RubberTire: return new Color(0.18f, 0.18f, 0.2f);          // Mat Siyah/Kauçuk
            case ItemType.ElectronicWaste: return new Color(0.3f, 0.95f, 0.65f);     // Neon Yeşil/Elektronik Devre
            case ItemType.SteelPlate: return new Color(0.78f, 0.84f, 0.92f);         // Parlak Çelik
            case ItemType.PolymerBlock: return new Color(0.35f, 0.88f, 1.0f);        // Yoğun Polimer Mavisi
            case ItemType.SuperCompost: return new Color(0.2f, 0.95f, 0.4f);         // Zümrüt Yeşili
            case ItemType.RepairKit: return new Color(1.0f, 0.55f, 0.2f);            // Turuncu Alet Çantası
            case ItemType.CarbonFilter: return new Color(0.85f, 0.9f, 0.95f);        // Gümüş/Metalik
            case ItemType.RiverCleanerBuoy: return new Color(1f, 0.85f, 0.2f);       // Duba Sarısı
            case ItemType.CleanEnergyGenerator: return new Color(0.4f, 1.0f, 0.85f); // Turkuaz Enerji
            default: return Color.white;
        }
    }

    public static bool IsRawWaste(ItemType type)
    {
        return type == ItemType.ScrapMetal || type == ItemType.PlasticWaste || type == ItemType.OrganicWaste ||
               type == ItemType.RubberTire || type == ItemType.ElectronicWaste;
    }

    public static int GetEcoCreditValue(ItemType type)
    {
        switch (type)
        {
            case ItemType.ScrapMetal: return 5;
            case ItemType.PlasticWaste: return 5;
            case ItemType.OrganicWaste: return 5;
            case ItemType.RubberTire: return 8;
            case ItemType.ElectronicWaste: return 12;
            case ItemType.SteelPlate: return 15;
            case ItemType.PolymerBlock: return 15;
            case ItemType.SuperCompost: return 18;
            case ItemType.RepairKit: return 35;
            case ItemType.CarbonFilter: return 35;
            case ItemType.RiverCleanerBuoy: return 35;
            case ItemType.CleanEnergyGenerator: return 90;
            default: return 10;
        }
    }
}
