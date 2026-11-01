using UnityEngine;

/// <summary>
/// Türkçe NPC İsim Üreteci.
/// Şehir ve çiftlikte doğan Kadın, Erkek, Çocuk ve Çiftçi NPC'lere
/// zengin, doğal Türkçe isimler atar.
/// </summary>
public static class TurkishNameGenerator
{
    private static readonly string[] FemaleNames = new string[]
    {
        "Aylin", "Zeynep", "Elif", "Defne", "Leyla", "Ceren", "Selin", "Aslı",
        "Damla", "Yağmur", "Ece", "Melis", "İrem", "Derya", "Gül", "Buse",
        "Hande", "Gizem", "Bahar", "Deniz", "Pelin", "Simge", "Ezgi", "Gamze"
    };

    private static readonly string[] MaleNames = new string[]
    {
        "Barış", "Deniz", "Emre", "Kerem", "Mert", "Ozan", "Can", "Efe",
        "Kaan", "Tolga", "Burak", "Onur", "Arda", "Murat", "Sinan", "Serkan",
        "Alp", "Doruk", "Koray", "Cem", "Umut", "Volkan", "Yiğit", "Bora"
    };

    private static readonly string[] ChildNames = new string[]
    {
        "Küçük Ege", "Can", "Ali", "Ada", "Maya", "Nil", "Rüzgar", "Pamir",
        "Batu", "Derin", "Lara", "Atlas", "Toprak", "Mavi", "Güneş", "Çınar"
    };

    private static readonly string[] FarmerNames = new string[]
    {
        "Hasan Usta", "Salih Çiftçi", "Mahmut Dayı", "Recep Ağa", "Cemil Usta",
        "Kadir Çiftçi", "Osman Dayı", "Rıza Usta", "Seyfi Dayı", "Halil Ağa"
    };

    public static string GetRandomFemaleName()
    {
        return FemaleNames[Random.Range(0, FemaleNames.Length)];
    }

    public static string GetRandomMaleName()
    {
        return MaleNames[Random.Range(0, MaleNames.Length)];
    }

    public static string GetRandomChildName()
    {
        return ChildNames[Random.Range(0, ChildNames.Length)];
    }

    public static string GetRandomFarmerName()
    {
        return FarmerNames[Random.Range(0, FarmerNames.Length)];
    }
}

