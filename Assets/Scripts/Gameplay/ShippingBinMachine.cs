using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Otomasyon Besleme Sandığı & Eko-Tüccar Kasası (Shipping Bin & Hopper Crate).
/// - Yanına bir Konveyör Bant (ConveyorBelt) veya Geri Dönüşüm Presi kurulduğunda:
///    * "Besleme Sandığı (Hopper)" olarak çalışır: Oyuncu [E] ile ham hurdaları sandığa doldurur,
///      sandık içindeki hurdaları sırayla banda bırakır ve bant üzerinde görsel olarak prese taşınır!
/// - Yanında çıkış bandı yoksa veya banttan kendisine ürün gelirse:
///    * "Satış Sandığı" olarak çalışır: Fazla malzemeleri ve ara ürünleri Eko-Kredi'ye çevirir!
/// </summary>
public class ShippingBinMachine : MonoBehaviour, IInteractable
{
    [Header("--- SATIŞ FİYATLARI (EKO-KREDİ) ---")]
    [SerializeField] private int metalScrapPrice = 5;
    [SerializeField] private int plasticWastePrice = 5;
    [SerializeField] private int organicWastePrice = 5;
    [SerializeField] private int steelPlatePrice = 18;
    [SerializeField] private int polymerBlockPrice = 18;
    [SerializeField] private int carbonFilterPrice = 35;
    [SerializeField] private int superCompostPrice = 25;
    [SerializeField] private int riverBuoyPrice = 45;

    private readonly Queue<ItemType> hopperQueue = new Queue<ItemType>();
    private float feedTimer;
    private ConveyorBelt cachedOutputBelt;

    public int QueuedCount => hopperQueue.Count;

    private void Update()
    {
        if (hopperQueue.Count == 0) return;

        feedTimer -= Time.deltaTime;
        if (feedTimer <= 0f)
        {
            feedTimer = 0.65f;
            TryFeedAdjacentBeltOrMachine();
        }
    }

    public bool IsFeedingBelt(ConveyorBelt belt)
    {
        return belt != null && belt == cachedOutputBelt;
    }

    private ConveyorBelt FindOutgoingBelt()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 1.85f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            ConveyorBelt belt = hits[i].GetComponentInParent<ConveyorBelt>();
            if (belt != null)
            {
                // Bandın yönü sandığın merkezine doğru DEĞİLSE (yani sandıktan dışarı doğru akıyorsa) çıkış bandıdır
                Vector3 toBin = (transform.position - belt.transform.position).normalized;
                if (Vector3.Dot(belt.transform.forward, toBin) < 0.45f)
                {
                    cachedOutputBelt = belt;
                    return belt;
                }
            }
        }
        cachedOutputBelt = null;
        return null;
    }

    private void TryFeedAdjacentBeltOrMachine()
    {
        if (hopperQueue.Count == 0) return;

        ConveyorBelt outBelt = FindOutgoingBelt();
        if (outBelt != null && !outBelt.HasCargo)
        {
            ItemType nextItem = hopperQueue.Dequeue();
            outBelt.TryAcceptCargo(nextItem);
            return;
        }

        // Yanında doğrudan Geri Dönüşüm Presi varsa ona da besleyebilir
        Collider[] hits = Physics.OverlapSphere(transform.position, 2.2f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            RecyclerMachine recycler = hits[i].GetComponentInParent<RecyclerMachine>();
            if (recycler != null && recycler.CanAcceptFromConveyor())
            {
                ItemType nextItem = hopperQueue.Dequeue();
                recycler.ReceiveItemFromConveyor(nextItem);
                return;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        ConveyorBelt outBelt = FindOutgoingBelt();
        if (outBelt != null)
        {
            return $"[E] Otomasyon Besleme Sandığı: Hurdaları Banda Yükle (Haznedeki: {hopperQueue.Count})";
        }

        if (hopperQueue.Count > 0)
        {
            return $"[E] Sandık (Haznede {hopperQueue.Count} Atık Var - Yanına Bant Kur veya Satış Yap)";
        }

        return "[E] Eko-Sandık (Bant Hattına Besle veya Fazla Ürünleri Sat)";
    }

    public void Interact(GameObject interactor)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null) return;

        ConveyorBelt outBelt = FindOutgoingBelt();
        if (outBelt != null)
        {
            // Otomasyon Modu: Çantadaki ham maddeleri sandık haznesine doldur (Bantla Prese gidecek!)
            int loaded = 0;
            ItemType[] rawTypes = { ItemType.ScrapMetal, ItemType.PlasticWaste, ItemType.OrganicWaste };
            foreach (var raw in rawTypes)
            {
                while (inventory.HasItem(raw, 1) && loaded < 8 && hopperQueue.Count < 20)
                {
                    inventory.TrySpendItem(raw, 1);
                    hopperQueue.Enqueue(raw);
                    loaded++;
                }
            }

            if (loaded > 0)
            {
                feedTimer = 0.05f;
                inventory.Notify($"{loaded} adet ham atık Besleme Sandığına yüklendi! Bant üzerinden Prese taşınıyor...");
                return;
            }
            else if (hopperQueue.Count > 0)
            {
                inventory.Notify($"Sandık şu an {hopperQueue.Count} adet atığı banda besliyor!");
                return;
            }
        }

        // Satış Modu: İşlenmiş ürünleri veya fazla ham atıkları Eko-Kredi'ye çevir
        int earnedTotal = 0;

        if (inventory.HasItem(ItemType.SteelPlate, 1))
        {
            inventory.TrySpendItem(ItemType.SteelPlate, 1);
            earnedTotal += steelPlatePrice;
        }
        if (inventory.HasItem(ItemType.PolymerBlock, 1))
        {
            inventory.TrySpendItem(ItemType.PolymerBlock, 1);
            earnedTotal += polymerBlockPrice;
        }
        if (inventory.HasItem(ItemType.CarbonFilter, 1))
        {
            inventory.TrySpendItem(ItemType.CarbonFilter, 1);
            earnedTotal += carbonFilterPrice;
        }
        if (inventory.HasItem(ItemType.SuperCompost, 1))
        {
            inventory.TrySpendItem(ItemType.SuperCompost, 1);
            earnedTotal += superCompostPrice;
        }
        if (inventory.HasItem(ItemType.RiverCleanerBuoy, 1))
        {
            inventory.TrySpendItem(ItemType.RiverCleanerBuoy, 1);
            earnedTotal += riverBuoyPrice;
        }

        if (inventory.HasItem(ItemType.ScrapMetal, 3))
        {
            inventory.TrySpendItem(ItemType.ScrapMetal, 3);
            earnedTotal += metalScrapPrice * 3;
        }
        if (inventory.HasItem(ItemType.PlasticWaste, 3))
        {
            inventory.TrySpendItem(ItemType.PlasticWaste, 3);
            earnedTotal += plasticWastePrice * 3;
        }
        if (inventory.HasItem(ItemType.OrganicWaste, 3))
        {
            inventory.TrySpendItem(ItemType.OrganicWaste, 3);
            earnedTotal += organicWastePrice * 3;
        }

        if (earnedTotal > 0)
        {
            inventory.AddCoins(earnedTotal);
            inventory.Notify($"Satış Başarılı! +{earnedTotal} Eko-Kredi kasaya eklendi.");
        }
        else
        {
            inventory.Notify("Bant beslemek için sandığın yanına Konveyör Bant kurun veya satış için en az 3 atık / 1 ara ürün getirin!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CollectibleItem item = other.GetComponent<CollectibleItem>();
        if (item != null && !item.IsBeingPulled)
        {
            ItemType type = item.itemType;
            Destroy(item.gameObject);
            ReceiveItemFromConveyor(type);
        }
    }

    public void ReceiveItemFromConveyor(ItemType type)
    {
        // Eğer sandığın çıkışında başka bir bant varsa aktarmaya devam et, yoksa sat!
        ConveyorBelt outBelt = FindOutgoingBelt();
        if (outBelt != null && hopperQueue.Count < 20)
        {
            hopperQueue.Enqueue(type);
            return;
        }

        int price = ItemDatabase.GetEcoCreditValue(type);
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.AddCoins(price);
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
}
