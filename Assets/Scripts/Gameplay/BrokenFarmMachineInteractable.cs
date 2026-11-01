using UnityEngine;

/// <summary>
/// Çiftlik Bölgesi Görev & Tamir Sistemi: Arızalı Traktör / Değirmen (BrokenFarmMachineInteractable).
/// - Başlangıçta arızalıdır ve siyah motor dumanı çıkarır.
/// - Oyuncu [Tamir Kiti (RepairKit)] (veya 1 Çelik Plaka + 2 Metal Hurda) ile [E] tuşuna basarak tamir eder.
/// - Tamir edildiğinde:
///    1. Siyah duman kesilir, değirmen/traktör çalışmaya başlar.
///    2. Çiftçiler oyuncuya Sürülmüş Tarlaları (FarmTileTilledSOIL) kullanma izni verir!
///    3. Çiftlik ambarında pasif olarak Biyo-Hammadde birikmeye başlar ([E] ile toplanır).
/// </summary>
public class BrokenFarmMachineInteractable : MonoBehaviour, IInteractable
{
    [Header("--- MAKİNE KİMLİĞİ VE DURUMU ---")]
    [SerializeField] private string machineTitle = "Çiftlik Traktörü";
    [SerializeField] private bool isRepaired = false;

    [Header("--- PASİF AMBAR BİYO-ÜRETİMİ ---")]
    [SerializeField] private float bioProductionInterval = 10f;
    [SerializeField] private int maxStoredBio = 10;

    private ParticleSystem brokenSmokeVFX;
    private float productionTimer;
    private int storedBioCount;
    private Vector3 baseScale;
    private string farmerName;

    public bool IsRepaired => isRepaired;

    public void InitializeMachine(string title)
    {
        machineTitle = title;
    }

    private void Start()
    {
        farmerName = TurkishNameGenerator.GetRandomFarmerName();
        baseScale = transform.localScale;
        if (baseScale.sqrMagnitude < 0.01f) baseScale = Vector3.one;

        EnsureInteractCollider();
        if (!isRepaired)
        {
            CreateBrokenSmokeVFX();
        }
    }

    private void EnsureInteractCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(2.2f, 2.0f, 2.2f);
            bc.center = new Vector3(0f, 1.0f, 0f);
        }
    }

    private void CreateBrokenSmokeVFX()
    {
        if (brokenSmokeVFX != null) return;

        GameObject smokeObj = new GameObject("BrokenMachineSmoke");
        smokeObj.transform.SetParent(transform, false);
        smokeObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        smokeObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        brokenSmokeVFX = smokeObj.AddComponent<ParticleSystem>();
        var main = brokenSmokeVFX.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        main.startColor = new Color(0.18f, 0.16f, 0.15f, 0.8f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = brokenSmokeVFX.emission;
        emission.rateOverTime = 6f;

        var shape = brokenSmokeVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.25f;

        var renderer = smokeObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                Material m = new Material(sh);
                m.color = new Color(0.2f, 0.18f, 0.16f, 0.8f);
                renderer.sharedMaterial = m;
            }
        }
    }

    private void Update()
    {
        if (!isRepaired) return;

        // Çalışan motor hafif titreşimi
        float rumble = Mathf.Sin(Time.time * 18f) * 0.012f;
        transform.localScale = new Vector3(baseScale.x * (1f + rumble), baseScale.y * (1f - rumble), baseScale.z * (1f + rumble));

        // Pasif Biyo-Hammadde biriktir
        if (storedBioCount < maxStoredBio)
        {
            float mult = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CropYieldMultiplier : 1f;
            productionTimer += Time.deltaTime * mult;
            if (productionTimer >= bioProductionInterval)
            {
                productionTimer = 0f;
                storedBioCount += 2;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (!isRepaired)
        {
            var inv = PlayerInventory.Instance;
            bool hasKit = inv != null && inv.HasItem(ItemType.RepairKit, 1);
            bool hasParts = inv != null && inv.HasItem(ItemType.SteelPlate, 1) && inv.HasItem(ItemType.ScrapMetal, 2);

            if (hasKit)
                return $"[E] Arızalı {machineTitle}: Tamir Kiti ile Onar (Tarlaları Aç!)";
            if (hasParts)
                return $"[E] Arızalı {machineTitle}: 1 Çelik Plaka + 2 Hurda ile Onar (Tarlaları Aç!)";

            return $"Arızalı {machineTitle} (Onarmak İçin: Atölyede 1 Tamir Kiti Üret veya 1 Çelik Plaka + 2 Hurda Getir)";
        }

        if (storedBioCount > 0)
        {
            return $"[E] {machineTitle} (Aktif): {farmerName}'nın Ayırdığı +{storedBioCount} Biyo-Hammaddeyi Al";
        }

        int pct = Mathf.RoundToInt((productionTimer / Mathf.Max(1f, bioProductionInterval)) * 100f);
        return $"{machineTitle} ({farmerName} İşletiyor | Sürülmüş Tarlalar Açık | Ambar: %{pct})";
    }

    public void Interact(GameObject interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        if (!isRepaired)
        {
            bool repairedNow = false;
            if (inv.TrySpendItem(ItemType.RepairKit, 1))
            {
                repairedNow = true;
            }
            else if (inv.HasItem(ItemType.SteelPlate, 1) && inv.HasItem(ItemType.ScrapMetal, 2))
            {
                inv.TrySpendItem(ItemType.SteelPlate, 1);
                inv.TrySpendItem(ItemType.ScrapMetal, 2);
                repairedNow = true;
            }

            if (repairedNow)
            {
                isRepaired = true;
                storedBioCount = 3; // Çiftçinin teşekkür hediyesi
                if (brokenSmokeVFX != null)
                {
                    brokenSmokeVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                inv.AddCoins(30);
                EcoSystemManager.Instance?.RegisterFarmMachineRepaired(machineTitle);
                if (GameAudioAndJuiceManager.Instance != null)
                {
                    GameAudioAndJuiceManager.Instance.TriggerPurifyOrRepairOomph(transform.position, $"{machineTitle.ToUpper()} ONARILDI! (+30 Kredi)", new Color(0.35f, 1f, 0.5f), transform);
                }
            }
            else
            {
                inv.Notify($"Arızalı {machineTitle} için Atölyede [Tamir Kiti] üretmelisin (veya 1 Çelik Plaka + 2 Metal Hurda)!");
                GameAudioAndJuiceManager.Instance?.PlayErrorSound(transform.position, "TAMİR KİTİ GEREKLİ!");
            }
            return;
        }

        if (storedBioCount > 0)
        {
            int given = 0;
            while (storedBioCount > 0 && inv.CanAddItem(ItemType.OrganicWaste, 1))
            {
                inv.AddItem(ItemType.OrganicWaste, 1);
                storedBioCount--;
                given++;
            }

            if (given > 0)
            {
                inv.AddCoins(given * 3);
                inv.Notify($"Çiftlik ambarından +{given} Biyo-Hammadde ve +{given * 3} Eko-Kredi alındı!");
                GameAudioAndJuiceManager.Instance?.TriggerHarvestOomph(transform.position, $"+{given} Biyo-Hammadde (+{given * 3} Kredi)", new Color(0.45f, 0.95f, 0.35f), transform);
            }
        }
        else
        {
            inv.Notify($"{machineTitle} tarlaları sürüyor! Sürülmüş tarlalarda [E] ile 4 aşamalı ekin yetiştirebilirsin.");
        }
    }

    public Vector3 GetPosition() => transform.position;
}

