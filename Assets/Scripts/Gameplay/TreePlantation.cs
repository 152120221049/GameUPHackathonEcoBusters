using UnityEngine;

/// <summary>
/// Eko-Fidan ve 3 Aşamalı Ağaçlandırma Sistemi (TreePlantation).
/// - Oyuncunun diktiği fidanlar 3 belirgin aşamada büyür:
///    1. Aşama: Fidan (Sapling - %0..%45)
///    2. Aşama: Genç Ağaç (Young Tree - %45..%99)
///    3. Aşama: Yetişkin Ağaç (Mature Tree - %100)
/// - Her aşama geçişinde Ada Sağlığını (EcoSystemManager) kalıcı olarak artırır.
/// - Çevresindeki kirli karoları periyodik olarak (1 saniyede bir, sıfır takılma ile) temizler.
/// - Yetişkin ağaçlar periyodik olarak Biyo-Hammadde (Organik Meyve/Yaprak) üretir ([E] ile hasat edilir).
/// </summary>
public class TreePlantation : MonoBehaviour, IInteractable
{
    public static int ActiveTreeCount { get; private set; }

    [Header("--- BÜYÜME (3 AŞAMA) VE HASAT ---")]
    [Tooltip("Fidanın tam ağaç olma süresi (saniye)")]
    [SerializeField] private float growDuration = 16f;
    [Tooltip("Olgun ağaçta yeni meyve oluşum süresi (saniye)")]
    [SerializeField] private float fruitInterval = 14f;
    [Tooltip("Temizleme yarıçapı")]
    [SerializeField] private float purifyRadius = 5.5f;

    private float growthProgress;
    private float fruitTimer;
    private float purifyTimer = 0.8f;
    private int currentStage = 1; // 1: Fidan, 2: Genç Ağaç, 3: Yetişkin Ağaç
    private bool hasFruit;
    private Vector3 fullScale;

    public int CurrentStage => currentStage;
    public bool IsMature => currentStage >= 3;

    private void OnEnable()
    {
        ActiveTreeCount++;
    }

    private void OnDisable()
    {
        ActiveTreeCount = Mathf.Max(0, ActiveTreeCount - 1);
    }

    private void Start()
    {
        fullScale = transform.localScale;
        if (fullScale.sqrMagnitude < 0.01f) fullScale = Vector3.one;

        currentStage = 1;
        growthProgress = 0f;
        transform.localScale = fullScale * 0.35f; // 1. Aşama: Fidan olarak başla

        EcoSystemManager.Instance?.RegisterTreeGrowthBonus(1.5f, 1.0f);
    }

    private void Update()
    {
        if (currentStage < 3)
        {
            growthProgress += Time.deltaTime / Mathf.Max(1f, growDuration);
            growthProgress = Mathf.Clamp01(growthProgress);

            if (growthProgress >= 0.45f && currentStage == 1)
            {
                currentStage = 2; // 2. Aşama: Genç Ağaç
                EcoSystemManager.Instance?.RegisterTreeGrowthBonus(2.5f, 1.5f);
            }
            else if (growthProgress >= 1.0f && currentStage < 3)
            {
                currentStage = 3; // 3. Aşama: Yetişkin Ağaç
                hasFruit = true;
                EcoSystemManager.Instance?.RegisterTreeGrowthBonus(4.0f, 3.0f);
                PlayerInventory.Instance?.Notify("Diktiğin Eko-Fidan yetişkin bir ağaca dönüştü! (+Ada Sağlığı)");
            }

            float stageScale = currentStage == 1
                ? Mathf.Lerp(0.35f, 0.62f, growthProgress / 0.45f)
                : (currentStage == 2 ? Mathf.Lerp(0.65f, 0.95f, (growthProgress - 0.45f) / 0.55f) : 1.0f);

            transform.localScale = Vector3.Lerp(transform.localScale, fullScale * stageScale, 6f * Time.deltaTime);
        }
        else
        {
            // Yetişkin ağaç hafifçe nefes alır ve periyodik meyve verir
            float pulse = hasFruit ? Mathf.Sin(Time.time * 4.5f) * 0.035f : Mathf.Sin(Time.time * 1.8f) * 0.015f;
            transform.localScale = fullScale * (1.0f + pulse);

            if (!hasFruit)
            {
                fruitTimer += Time.deltaTime;
                if (fruitTimer >= fruitInterval)
                {
                    fruitTimer = 0f;
                    hasFruit = true;
                }
            }
        }

        // Her karede değil, saniyede 1 kez çevreyi temizle (Mikro-takılmaları önler!)
        purifyTimer -= Time.deltaTime;
        if (purifyTimer <= 0f)
        {
            purifyTimer = 1.0f;
            PurifySurroundings();
        }
    }

    private void PurifySurroundings()
    {
        float activeRadius = purifyRadius * (currentStage / 3f);
        Collider[] hits = Physics.OverlapSphere(transform.position, activeRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            GroundTile tile = hits[i].GetComponent<GroundTile>();
            if (tile != null && tile.pollutionLevel > 0f)
            {
                tile.ModifyPollution(-4f);
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (currentStage < 3)
        {
            string stageName = currentStage == 1 ? "Aşama 1/3: Fidan" : "Aşama 2/3: Genç Ağaç";
            int percent = Mathf.RoundToInt(growthProgress * 100f);
            return $"Eko-Ağaç ({stageName} - %{percent}) | [E] Organik Besin Ver (Hızlı Büyüt)";
        }

        if (hasFruit)
        {
            return "[E] Yetişkin Eko-Ağaçtan Biyo-Hammadde Hasat Et (+3 Biyo-Hammadde, +5 Eko-Kredi)";
        }

        int fruitPct = Mathf.RoundToInt((fruitTimer / Mathf.Max(1f, fruitInterval)) * 100f);
        return $"Yetişkin Eko-Ağaç (Hava ve Toprağı Temizliyor... Yeni Hasat: %{fruitPct})";
    }

    public void Interact(GameObject interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        if (currentStage < 3)
        {
            if (inv.TrySpendItem(ItemType.OrganicWaste, 1))
            {
                growthProgress = Mathf.Clamp01(growthProgress + 0.55f);
                inv.Notify("Fidana organik besin verildi! Hızla büyüyor.");
            }
            else
            {
                inv.Notify("Fidan doğal olarak büyüyor. Hızlandırmak için 1 Biyo-Hammadde gerekir.");
            }
            return;
        }

        if (hasFruit)
        {
            if (inv.AddItem(ItemType.OrganicWaste, 3))
            {
                hasFruit = false;
                fruitTimer = 0f;
                inv.AddCoins(5);
                EcoSystemManager.Instance?.RegisterWasteCollected(ItemType.OrganicWaste);
                inv.Notify("Temiz meyveler hasat edildi! (+3 Biyo-Hammadde, +5 Eko-Kredi)");
                GameAudioAndJuiceManager.Instance?.TriggerHarvestOomph(transform.position, "+3 Biyo-Hammadde (+5 Kredi)", new Color(0.35f, 1f, 0.45f), transform);
            }
        }
        else
        {
            inv.Notify("Eko-Ağaç şu an oksijen üretiyor, yeni meyveler olgunlaşıyor.");
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
}
