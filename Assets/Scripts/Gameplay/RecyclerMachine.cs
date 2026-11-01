using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Geri Dönüşüm Presi / Fırını (Recycler Machine).
/// - 2x2 karo alan kaplar.
/// - 1. Kademe Ham Maddeleri 2. Kademe Ara Ürünlere ve Eko-Kredi'ye dönüştürür:
///    * Metal Hurda -> Çelik Plaka (SteelPlate) + 12 Eko-Kredi
///    * Plastik Atık -> Polimer Blok (PolymerBlock) + 12 Eko-Kredi
///    * Biyo-Hammadde -> Süper Kompost (SuperCompost) + 12 Eko-Kredi
/// - Hem manuel [E] etkileşimiyle hem de Sandık -> Konveyör Bant -> Pres otomasyonuyla çalışır!
/// </summary>
public class RecyclerMachine : MonoBehaviour, IInteractable
{
    [Header("--- MAKİNE AYARLARI ---")]
    [Tooltip("İşlem süresi (saniye)")]
    [SerializeField] private float processDuration = 2.6f;

    [Tooltip("Eşya çıkış noktası (Otomasyon bandı için)")]
    [SerializeField] private Transform outputPoint;

    [Header("--- MEVCUT DURUM ---")]
    [SerializeField] private bool isProcessing;
    [SerializeField] private float currentProgress;
    [SerializeField] private ItemType currentInputType = ItemType.ScrapMetal;
    [SerializeField] private bool hasFinishedProduct;
    [SerializeField] private int finishedValueCoins = 12;

    private readonly Queue<ItemType> inputQueue = new Queue<ItemType>();
    private Vector3 initialScale;

    public bool IsProcessing => isProcessing;
    public bool HasFinishedProduct => hasFinishedProduct;

    private void Awake()
    {
        initialScale = transform.localScale;
        if (initialScale.sqrMagnitude < 0.01f) initialScale = Vector3.one;
    }

    private void Update()
    {
        if (!isProcessing && !hasFinishedProduct && inputQueue.Count > 0)
        {
            ItemType next = inputQueue.Dequeue();
            StartProcessing(next, 12);
        }

        if (isProcessing)
        {
            float eff = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.FactoryEfficiencyMultiplier : 1f;
            currentProgress += Time.deltaTime * eff;

            // Presleme görsel animasyonu (Squash & Stretch)
            float squash = Mathf.Sin(Time.time * 14f) * 0.045f;
            transform.localScale = new Vector3(
                initialScale.x * (1f + squash),
                initialScale.y * (1f - squash),
                initialScale.z * (1f + squash)
            );

            if (currentProgress >= processDuration)
            {
                transform.localScale = initialScale;
                FinishProcessing();
            }
        }
    }

    public bool CanAcceptFromConveyor()
    {
        return inputQueue.Count < 10;
    }

    public static ItemType GetProcessedOutputType(ItemType rawType)
    {
        switch (rawType)
        {
            case ItemType.ScrapMetal: return ItemType.SteelPlate;
            case ItemType.PlasticWaste: return ItemType.PolymerBlock;
            case ItemType.OrganicWaste: return ItemType.SuperCompost;
            default: return ItemType.SteelPlate;
        }
    }

    public string GetInteractionPrompt()
    {
        if (hasFinishedProduct)
        {
            ItemType outItem = GetProcessedOutputType(currentInputType);
            return $"[E] İşlenmiş Ürünü Al: +1 {ItemDatabase.GetDisplayName(outItem)} & +{finishedValueCoins} Eko-Kredi";
        }

        if (isProcessing)
        {
            int percent = Mathf.RoundToInt((currentProgress / processDuration) * 100f);
            ItemType outItem = GetProcessedOutputType(currentInputType);
            return $"Presleniyor ({ItemDatabase.GetDisplayName(outItem)})... %{percent} (Sırada: {inputQueue.Count})";
        }

        return "[E] Geri Dönüşüm Presi: 2 Ham Atık İşle -> Çelik Plaka / Polimer Blok / Kompost";
    }

    public void Interact(GameObject interactor)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null) return;

        // 1. Bitmiş ürün varsa topla
        if (hasFinishedProduct)
        {
            DeliverFinishedProductToPlayer();
            return;
        }

        // 2. Makineye hammadde besle (Çalışırken bile kuyruğa eklenebilir!)
        if (inventory.HasItem(ItemType.ScrapMetal, 2))
        {
            inventory.TrySpendItem(ItemType.ScrapMetal, 2);
            EnqueueOrStart(ItemType.ScrapMetal, 15);
            inventory.Notify("2 Metal Hurda prese verildi -> Çelik Plaka üretiliyor!");
        }
        else if (inventory.HasItem(ItemType.PlasticWaste, 2))
        {
            inventory.TrySpendItem(ItemType.PlasticWaste, 2);
            EnqueueOrStart(ItemType.PlasticWaste, 15);
            inventory.Notify("2 Plastik Atık eritme haznesine verildi -> Polimer Blok üretiliyor!");
        }
        else if (inventory.HasItem(ItemType.OrganicWaste, 2))
        {
            inventory.TrySpendItem(ItemType.OrganicWaste, 2);
            EnqueueOrStart(ItemType.OrganicWaste, 15);
            inventory.Notify("2 Biyo-Hammadde hazneye verildi -> Süper Kompost üretiliyor!");
        }
        else
        {
            inventory.Notify("Pres için en az 2 Metal Hurda, 2 Plastik Atık veya 2 Biyo-Hammadde gerekli!");
        }
    }

    private void EnqueueOrStart(ItemType inputType, int coinReward)
    {
        if (!isProcessing && !hasFinishedProduct)
        {
            StartProcessing(inputType, coinReward);
        }
        else
        {
            inputQueue.Enqueue(inputType);
        }
    }

    public void StartProcessing(ItemType inputType, int coinReward)
    {
        isProcessing = true;
        currentProgress = 0f;
        currentInputType = inputType;
        finishedValueCoins = coinReward;
    }

    /// <summary>
    /// Taşıma bandından veya Takipçi NPC'den gelen atığı işleme alır.
    /// </summary>
    public void ReceiveItemFromConveyor(ItemType inputType)
    {
        if (!isProcessing && !hasFinishedProduct)
        {
            StartProcessing(inputType, 12);
        }
        else if (inputQueue.Count < 12)
        {
            inputQueue.Enqueue(inputType);
        }
        else
        {
            PlayerInventory.Instance?.AddItem(GetProcessedOutputType(inputType), 1);
        }
    }

    private void FinishProcessing()
    {
        isProcessing = false;
        currentProgress = 0f;
        ItemType producedItem = GetProcessedOutputType(currentInputType);

        // Çıkışta (veya yakında) bir dışarı akan taşıma bandı varsa ara ürünü banda bırak
        Vector3 checkCenter = outputPoint != null ? outputPoint.position : transform.position;
        Collider[] cols = Physics.OverlapSphere(checkCenter, 1.9f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < cols.Length; i++)
        {
            ConveyorBelt belt = cols[i].GetComponentInParent<ConveyorBelt>();
            if (belt != null && !belt.HasCargo)
            {
                Vector3 toMachine = (transform.position - belt.transform.position).normalized;
                if (Vector3.Dot(belt.transform.forward, toMachine) < 0.35f)
                {
                    if (belt.TryAcceptCargo(producedItem))
                    {
                        PlayerInventory.Instance?.AddItem(producedItem, 1);
                        hasFinishedProduct = false;
                        return;
                    }
                }
            }
        }

        // Sırada başka bant eşyası varsa bekletmeden oyuncunun envanterine ara ürünü teslim et ki bant hattı hiç durmasın!
        if (inputQueue.Count > 0 && PlayerInventory.Instance != null)
        {
            DeliverFinishedProductToPlayer();
            return;
        }

        hasFinishedProduct = true;
    }

    private void DeliverFinishedProductToPlayer()
    {
        hasFinishedProduct = false;
        ItemType outItem = GetProcessedOutputType(currentInputType);
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.AddItem(outItem, 1);
            PlayerInventory.Instance.AddCoins(finishedValueCoins);
            PlayerInventory.Instance.Notify($"Pres Üretimi Tamamlandı: +1 {ItemDatabase.GetDisplayName(outItem)} & +{finishedValueCoins} Eko-Kredi!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CollectibleItem collectible = other.GetComponent<CollectibleItem>();
        if (collectible != null && !collectible.IsBeingPulled)
        {
            ItemType type = collectible.itemType;
            Destroy(collectible.gameObject);
            ReceiveItemFromConveyor(type);
        }
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
}
