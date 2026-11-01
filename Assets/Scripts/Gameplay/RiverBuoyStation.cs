using UnityEngine;

/// <summary>
/// Nehir Eko-Müdahale, Atık Yakalama ve Balık/Biyo-Kaynak Dubası (RiverBuoyStation - Buoy.vox).
/// - Nehir tabanındaki ağır metal ve zehirli çamurları arıtmak için su kanalında bulunur.
/// - Oyuncunun ürettiği [RiverCleanerBuoy] (Temizleme Dubası) ile aktive edilir.
/// - Aktive olduktan sonra:
///    1. Nehir suyunu berraklaştırır (Ada Sağlığı ve Nehir Temizliği artar).
///    2. Zamanla akıntıdan gelen Plastik Atıkları ve Nehir Biyo-Hammaddelerini (Balık/Yosun Özü) ağında biriktirir.
///    3. Oyuncu [E] ile dubada biriken plastik ve biyo-materyalleri toplar!
/// </summary>
public class RiverBuoyStation : MonoBehaviour, IInteractable
{
    [Header("--- DUBA DURUMU ---")]
    [SerializeField] private bool isActivated = false;
    [SerializeField] private float purifyRadius = 12f;
    [Tooltip("Suyun üzerinde hafif yüzme/dalgalanma hareketi")]
    [SerializeField] private float bobSpeed = 2.2f;
    [SerializeField] private float bobHeight = 0.08f;

    [Header("--- ATIK YAKALAMA VE BALIK/BİYO-ÜRETİM ---")]
    [SerializeField] private float catchInterval = 8.5f;
    [SerializeField] private int maxCatchCapacity = 8;

    private Vector3 initialPosition;
    private float purifyTimer = 0.5f;
    private float catchTimer = 0f;
    private int caughtPlasticCount = 0;
    private int caughtBioCount = 0;

    public bool IsActivated => isActivated;
    public int TotalCaught => caughtPlasticCount + caughtBioCount;

    private void Start()
    {
        initialPosition = transform.position;
        // Su yüzeyi hizasında yüzmesi için Y yüksekliğini su yüzeyine (-0.25f) yaklaştır
        if (initialPosition.y < -0.45f)
        {
            initialPosition.y = -0.25f;
            transform.position = initialPosition;
        }
    }

    private void Update()
    {
        // Su üzerinde doğal dalgalanma hareketi
        float phase = transform.position.x * 0.25f + transform.position.z * 0.25f;
        transform.position = initialPosition + new Vector3(0f, Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight, 0f);

        if (!isActivated) return;

        // 1. Saniyede 1 kez nehir karolarını temizle (Her karede OverlapSphere çağrısını önler!)
        purifyTimer -= Time.deltaTime;
        if (purifyTimer <= 0f)
        {
            purifyTimer = 1.0f;
            PurifyRiverTiles();
        }

        // 2. Zamanla sudan Plastik Atık ve Biyo-Hammadde (Balık/Su Florası) yakala
        if (TotalCaught < maxCatchCapacity)
        {
            catchTimer += Time.deltaTime;
            if (catchTimer >= catchInterval)
            {
                catchTimer = 0f;
                caughtPlasticCount++;
                caughtBioCount += 2;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (isActivated)
        {
            if (TotalCaught > 0)
            {
                return $"[E] Eko-Duba Ağını Topla (+{caughtPlasticCount} Plastik, +{caughtBioCount} Biyo-Hammadde/Balık)";
            }

            int pct = Mathf.RoundToInt((catchTimer / Mathf.Max(1f, catchInterval)) * 100f);
            return $"Aktif Eko-Duba (Suyu Arıtıyor & Balık/Atık Yakalıyor... %{pct})";
        }

        if (PlayerInventory.Instance != null && PlayerInventory.Instance.GetCount(ItemType.RiverCleanerBuoy) >= 1)
        {
            return "[E] Temizleme Dubasını Kur (1 Temizleme Dubası Kullan)";
        }

        return "Kirli Nehir Kanalı (Atölyede 1 Temizleme Dubası Üretip [E] ile Kur)";
    }

    public void Interact(GameObject interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        if (isActivated)
        {
            if (TotalCaught <= 0)
            {
                inv.Notify("Duba şu an nehir suyunu süzüyor, ağda henüz yeni materyal birikmedi.");
                return;
            }

            int pGiven = 0;
            int bGiven = 0;

            while (caughtPlasticCount > 0 && inv.CanAddItem(ItemType.PlasticWaste, 1))
            {
                inv.AddItem(ItemType.PlasticWaste, 1);
                caughtPlasticCount--;
                pGiven++;
            }

            while (caughtBioCount > 0 && inv.CanAddItem(ItemType.OrganicWaste, 1))
            {
                inv.AddItem(ItemType.OrganicWaste, 1);
                caughtBioCount--;
                bGiven++;
            }

            if (pGiven > 0 || bGiven > 0)
            {
                int bonusCoins = (pGiven + bGiven) * 4;
                inv.AddCoins(bonusCoins);
                EcoSystemManager.Instance?.RegisterWasteCollected(ItemType.OrganicWaste);
                inv.Notify($"Dubadan +{pGiven} Plastik, +{bGiven} Biyo-Hammadde ve +{bonusCoins} Eko-Kredi toplandı!");
                GameAudioAndJuiceManager.Instance?.TriggerHarvestOomph(transform.position, $"+{pGiven} Plastik, +{bGiven} Biyo (+{bonusCoins} Kredi)", new Color(0.25f, 0.9f, 1f), transform);
            }
            return;
        }

        if (inv.RemoveItem(ItemType.RiverCleanerBuoy, 1))
        {
            isActivated = true;
            caughtPlasticCount = 1;
            caughtBioCount = 2; // İlk kurulum bonusu

            if (EcoSystemManager.Instance != null)
            {
                EcoSystemManager.Instance.RegisterRiverCleanerDeployed(transform.position);
            }
            else
            {
                inv.Notify("Temizleme Dubası Kuruldu! Nehir berraklaşıyor.");
            }
            GameAudioAndJuiceManager.Instance?.TriggerPurifyOrRepairOomph(transform.position, "NEHİR DUBASI AKTİF!", new Color(0.2f, 0.95f, 0.9f), transform);
        }
        else
        {
            inv.Notify("Yetersiz malzeme! Atölyede 1 Temizleme Dubası üretmelisin.");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(transform.position, "TEMİZLEME DUBASI GEREKLİ!");
        }
    }

    private void PurifyRiverTiles()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, purifyRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            GroundTile tile = hits[i].GetComponent<GroundTile>();
            if (tile != null && tile.targetPollutionLevel > 0f)
            {
                tile.ModifyPollution(-6f);
            }
        }
    }

    public Vector3 GetPosition() => transform.position;
}
