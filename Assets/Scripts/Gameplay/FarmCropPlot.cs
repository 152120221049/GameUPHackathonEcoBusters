using UnityEngine;

/// <summary>
/// Çiftlik Bölgesi 4 Aşamalı Ekin Yetiştirme ve Hasat Sistemi (FarmCropPlot).
/// - Sürülmüş tarla karoları (FarmTileTilledSOIL) üzerinde XeehSets 4 büyüme aşamalı
///   voxel modellerini (growth1 -> growth2 -> growth3 -> growth4: Mısır, Domates, Havuç, Pancar) kullanır.
/// - Çiftlikteki arızalı Traktör veya Değirmen tamir edilene kadar çiftçiler tarla kullanımına izin vermez.
/// - Traktör/Değirmen tamir edildiğinde tüm tarlalarda 4 aşamalı büyüme başlar ve [E] ile hasat edilir!
/// </summary>
public class FarmCropPlot : MonoBehaviour, IInteractable
{
    [Header("--- EKİN KİMLİĞİ VE SÜRESİ ---")]
    [SerializeField] private string cropDisplayName = "Eko-Mısır";
    [SerializeField] private float totalGrowthDuration = 18f;

    [Header("--- 4 BÜYÜME AŞAMASI GÖRSELLERİ ---")]
    [SerializeField] private GameObject stage1Obj;
    [SerializeField] private GameObject stage2Obj;
    [SerializeField] private GameObject stage3Obj;
    [SerializeField] private GameObject stage4Obj;

    private int currentStage = 1; // 1: growth1, 2: growth2, 3: growth3, 4: growth4 (Hasada Hazır)
    private float growthProgress = 0f;

    public int CurrentStage => currentStage;
    public bool IsReadyToHarvest => currentStage >= 4;

    public void SetupCropStages(string cropName, GameObject s1, GameObject s2, GameObject s3, GameObject s4)
    {
        cropDisplayName = cropName;
        stage1Obj = s1;
        stage2Obj = s2;
        stage3Obj = s3;
        stage4Obj = s4;

        currentStage = 1;
        growthProgress = 0f;
        RefreshStageVisuals();
    }

    private void Start()
    {
        EnsureInteractCollider();
        RefreshStageVisuals();
    }

    private void EnsureInteractCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.6f, 1.2f, 1.6f);
            bc.center = new Vector3(0f, 0.6f, 0f);
            bc.isTrigger = true;
        }
    }

    private void Update()
    {
        bool unlocked = EcoSystemManager.Instance != null && EcoSystemManager.Instance.IsFarmlandUnlockedForPlayer;
        if (!unlocked) return;

        if (currentStage < 4)
        {
            float yieldMult = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CropYieldMultiplier : 1f;
            growthProgress += (Time.deltaTime * yieldMult) / Mathf.Max(4f, totalGrowthDuration);
            growthProgress = Mathf.Clamp01(growthProgress);

            int newStage = 1;
            if (growthProgress >= 1.0f) newStage = 4;
            else if (growthProgress >= 0.66f) newStage = 3;
            else if (growthProgress >= 0.33f) newStage = 2;

            if (newStage != currentStage)
            {
                currentStage = newStage;
                RefreshStageVisuals();
            }
        }
        else if (stage4Obj != null)
        {
            // Olgun mahsulde hafif parıltı/nefes alma hareketi
            float pulse = 1.0f + Mathf.Sin(Time.time * 4.5f) * 0.04f;
            stage4Obj.transform.localScale = Vector3.one * pulse;
        }
    }

    private void RefreshStageVisuals()
    {
        if (stage1Obj != null) stage1Obj.SetActive(currentStage == 1);
        if (stage2Obj != null) stage2Obj.SetActive(currentStage == 2);
        if (stage3Obj != null) stage3Obj.SetActive(currentStage == 3);
        if (stage4Obj != null)
        {
            stage4Obj.SetActive(currentStage == 4);
            stage4Obj.transform.localScale = Vector3.one;
        }
    }

    public string GetInteractionPrompt()
    {
        bool unlocked = EcoSystemManager.Instance != null && EcoSystemManager.Instance.IsFarmlandUnlockedForPlayer;
        if (!unlocked)
        {
            return $"Çiftçi Tarlası ({cropDisplayName}): Kullanım İzni İçin Çiftlikteki Arızalı Traktör veya Değirmeni Tamir Et!";
        }

        if (currentStage >= 4)
        {
            return $"[E] Olgun {cropDisplayName} Hasat Et (Aşama 4/4 -> +4 Biyo-Hammadde & +15 Eko-Kredi)";
        }

        int pct = Mathf.RoundToInt(growthProgress * 100f);
        return $"{cropDisplayName} Yetişiyor (Aşama {currentStage}/4 - %{pct}) | [E] Süper Kompost / Can Suyu Ver";
    }

    public void Interact(GameObject interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        bool unlocked = EcoSystemManager.Instance != null && EcoSystemManager.Instance.IsFarmlandUnlockedForPlayer;
        if (!unlocked)
        {
            inv.Notify("Çiftçiler henüz bu tarlayı kullanmana izin vermiyor! Önce çiftlik avlusundaki Arızalı Traktörü veya Değirmeni tamir et.");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(transform.position, "ÖNCE TRAKTÖRÜ ONAR!");
            return;
        }

        if (currentStage >= 4)
        {
            if (inv.AddItem(ItemType.OrganicWaste, 4))
            {
                inv.AddCoins(15);
                EcoSystemManager.Instance?.RegisterWasteCollected(ItemType.OrganicWaste);
                inv.Notify($"{cropDisplayName} hasat edildi! (+4 Biyo-Hammadde, +15 Eko-Kredi) Yeni tohumlar ekildi.");
                GameAudioAndJuiceManager.Instance?.TriggerHarvestOomph(transform.position, $"HASAT: +4 Biyo-Madde (+15 Kredi)", new Color(0.4f, 1f, 0.4f), transform);

                // Yeniden Aşama 1 (growth1) olarak ekim döngüsünü başlat
                currentStage = 1;
                growthProgress = 0f;
                RefreshStageVisuals();
            }
            return;
        }

        // Büyüme aşamasındayken Süper Kompost varsa anında olgunlaştır, yoksa hafif hızlandır
        if (inv.HasItem(ItemType.SuperCompost, 1))
        {
            inv.TrySpendItem(ItemType.SuperCompost, 1);
            growthProgress = 1.0f;
            currentStage = 4;
            RefreshStageVisuals();
            EcoSystemManager.Instance?.RegisterSuperCompostApplied(transform.position);
            inv.Notify($"{cropDisplayName} tarlasına Süper Kompost verildi! Mahsul anında olgunlaştı (Aşama 4/4).");
            GameAudioAndJuiceManager.Instance?.TriggerPurifyOrRepairOomph(transform.position, "SÜPER KOMPOST ETKİSİ!", new Color(0.25f, 1f, 0.5f), transform);
        }
        else
        {
            growthProgress = Mathf.Clamp01(growthProgress + 0.35f);
            inv.Notify($"{cropDisplayName} sulandı ve bakımı yapıldı! Büyüme hızlandı (Aşama {currentStage}/4).");
            GameAudioAndJuiceManager.Instance?.TriggerBuildOrPlantOomph(transform.position, $"BAKIM YAPILDI (Aşama {currentStage}/4)", new Color(0.35f, 0.85f, 1f), transform);
        }
    }

    public Vector3 GetPosition() => transform.position;
}

