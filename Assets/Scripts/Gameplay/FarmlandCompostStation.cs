using UnityEngine;

/// <summary>
/// Taşra / Tarım Bölgesi Eko-Müdahale İstasyonu: Çorak Tarla & Toprak Islah Noktası.
/// - Başlangıçta besinsiz ve kurak olan tarlaya [SuperCompost] (Süper Kompost) aşılanır.
/// - Zamanla toprak zenginleşir, çiftçilerle olan anlaşma canlanır ve ekstra Biyo-Hammadde üretimi başlar.
/// </summary>
public class FarmlandCompostStation : MonoBehaviour, IInteractable
{
    [Header("--- KOMPOST DURUMU ---")]
    [SerializeField] private bool isComposted = false;
    [SerializeField] private float effectRadius = 9f;
    [Tooltip("Kompost sonrası her ne kadar sürede bir biyo-ürün hasadı vereceği")]
    [SerializeField] private float harvestInterval = 30f;

    private float harvestTimer;
    private float purifyTimer = 0.8f;
    private bool hasHarvestReady;

    private void Update()
    {
        if (isComposted)
        {
            purifyTimer -= Time.deltaTime;
            if (purifyTimer <= 0f)
            {
                purifyTimer = 1.0f;
                PurifySurroundings();
            }

            if (!hasHarvestReady)
            {
                // EcoSystemManager'ın tarım bereket çarpanı ile daha hızlı mahsul üretir
                float boost = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CropYieldMultiplier : 1f;
                harvestTimer += Time.deltaTime * boost;

                if (harvestTimer >= harvestInterval)
                {
                    hasHarvestReady = true;
                }
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (!isComposted)
        {
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.GetCount(ItemType.SuperCompost) >= 1)
            {
                return "[E] Süper Kompost Aşıla (1 Kompost Kullan)";
            }
            return "Çorak Tarla Toprağı (1 Süper Kompost Gerekir)";
        }

        if (hasHarvestReady)
        {
            return "[E] Bereketli Mahsulü Hasat Et (+4 Biyo-Hammadde)";
        }

        return "Verimli Eko-Tarla (Biyo-Hammadde Yetişiyor...)";
    }

    public void Interact(GameObject interactor)
    {
        if (!isComposted)
        {
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.RemoveItem(ItemType.SuperCompost, 1))
            {
                isComposted = true;
                harvestTimer = 0f;

                if (EcoSystemManager.Instance != null)
                {
                    EcoSystemManager.Instance.RegisterSuperCompostApplied(transform.position);
                }
                else
                {
                    PlayerInventory.Instance.Notify("Süper Kompost Aşılandı! Toprak canlanıyor.");
                }
            }
            else
            {
                PlayerInventory.Instance?.Notify("Yetersiz malzeme! Atölyede 1 Süper Kompost üretmelisin.");
            }
            return;
        }

        if (hasHarvestReady)
        {
            if (PlayerInventory.Instance != null)
            {
                if (PlayerInventory.Instance.AddItem(ItemType.OrganicWaste, 4))
                {
                    hasHarvestReady = false;
                    harvestTimer = 0f;
                    PlayerInventory.Instance.Notify("Verimli Mahsul Toplandı! (+4 Biyo-Hammadde)");
                }
            }
        }
    }

    private void PurifySurroundings()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, effectRadius);
        foreach (var hit in hits)
        {
            GroundTile tile = hit.GetComponent<GroundTile>();
            if (tile != null && tile.targetPollutionLevel > 0f)
            {
                tile.ModifyPollution(-4f * Time.deltaTime);
            }
        }
    }

    public Vector3 GetPosition() => transform.position;
}

