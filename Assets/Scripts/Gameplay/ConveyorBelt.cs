using UnityEngine;

/// <summary>
/// Factorio tarzı Görsel Taşıma Bandı (Conveyor Belt).
/// - 1x1 karo boyutundadır.
/// - Besleme Sandığından (ShippingBin), Otomatik Vakum Kulesinden veya oyuncudan aldığı hurdaları
///   bant üzerinde 3B görsel paket olarak ileri taşır ve sıradaki Banda / Geri Dönüşüm Presine aktarır.
/// - Üzerine düşen fiziksel CollectibleItem nesnelerini ve oyuncuyu da akış yönünde sürükler.
/// </summary>
public class ConveyorBelt : MonoBehaviour
{
    [Header("--- BANT AYARLARI ---")]
    [Tooltip("Bandın taşıma hızı (m/s)")]
    [SerializeField] private float beltSpeed = 2.8f;

    [Tooltip("Bandın akış yönü (Boş bırakılırsa transform.forward kullanılır)")]
    [SerializeField] private Vector3 beltDirection = Vector3.forward;

    [Header("--- GÖRSEL TAŞIMA DURUMU ---")]
    [SerializeField] private bool hasCargo;
    [SerializeField] private ItemType cargoType;
    [SerializeField] private float cargoProgress;

    private GameObject visualCargoObj;
    private Renderer visualCargoRenderer;
    private Transform arrowTransform;
    private float handoffRetryTimer;

    public Vector3 BeltDirection => transform.forward;
    public float BeltSpeed => beltSpeed;
    public bool HasCargo => hasCargo;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.95f, 0.25f, 0.95f);
            bc.center = new Vector3(0f, 0.12f, 0f);
            bc.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }

        EnsureCargoVisual();
    }

    private void EnsureCargoVisual()
    {
        if (visualCargoObj != null) return;

        Transform existing = transform.Find("VisualCargoParcel");
        if (existing != null)
        {
            visualCargoObj = existing.gameObject;
            visualCargoRenderer = visualCargoObj.GetComponent<Renderer>();
            visualCargoObj.SetActive(false);
            return;
        }

        visualCargoObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualCargoObj.name = "VisualCargoParcel";
        visualCargoObj.transform.SetParent(transform, false);
        visualCargoObj.transform.localScale = new Vector3(0.36f, 0.28f, 0.36f);
        visualCargoObj.transform.localPosition = new Vector3(0f, 0.28f, -0.4f);

        Collider c = visualCargoObj.GetComponent<Collider>();
        if (c != null) Destroy(c);

        visualCargoRenderer = visualCargoObj.GetComponent<Renderer>();
        if (visualCargoRenderer != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh != null)
            {
                visualCargoRenderer.material = new Material(sh);
            }
        }

        visualCargoObj.SetActive(false);
    }

    private void Update()
    {
        if (!hasCargo) return;

        if (visualCargoObj == null) EnsureCargoVisual();

        float efficiency = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.FactoryEfficiencyMultiplier : 1f;
        if (cargoProgress < 1f)
        {
            cargoProgress += Time.deltaTime * (beltSpeed * 0.55f * efficiency);
            cargoProgress = Mathf.Clamp01(cargoProgress);
        }

        if (visualCargoObj != null)
        {
            if (!visualCargoObj.activeSelf) visualCargoObj.SetActive(true);
            float zPos = Mathf.Lerp(-0.42f, 0.42f, cargoProgress);
            float bob = Mathf.Sin(Time.time * 12f) * 0.015f;
            visualCargoObj.transform.localPosition = new Vector3(0f, 0.28f + bob, zPos);
        }

        if (cargoProgress >= 1f)
        {
            handoffRetryTimer -= Time.deltaTime;
            if (handoffRetryTimer <= 0f)
            {
                handoffRetryTimer = 0.15f;
                TryHandoffCargoForward();
            }
        }
    }

    public bool TryAcceptCargo(ItemType type)
    {
        if (hasCargo) return false;

        hasCargo = true;
        cargoType = type;
        cargoProgress = 0f;
        EnsureCargoVisual();

        if (visualCargoObj != null)
        {
            visualCargoObj.SetActive(true);
            visualCargoObj.transform.localPosition = new Vector3(0f, 0.28f, -0.42f);
            if (visualCargoRenderer != null && visualCargoRenderer.material != null)
            {
                Color col = ItemDatabase.GetItemColor(type);
                if (visualCargoRenderer.material.HasProperty("_BaseColor"))
                    visualCargoRenderer.material.SetColor("_BaseColor", col);
                else
                    visualCargoRenderer.material.color = col;
            }
        }

        return true;
    }

    private void TryHandoffCargoForward()
    {
        // Bandın ön ucundaki (1.0m - 1.8m) hedef makine veya sıradaki bandı bul
        Vector3 checkPos = transform.position + transform.forward * 1.35f;
        Collider[] hits = Physics.OverlapSphere(checkPos, 0.85f, ~0, QueryTriggerInteraction.Collide);

        // 1. Öncelik: Geri Dönüşüm Presi (RecyclerMachine)
        for (int i = 0; i < hits.Length; i++)
        {
            RecyclerMachine recycler = hits[i].GetComponentInParent<RecyclerMachine>();
            if (recycler != null && recycler.CanAcceptFromConveyor())
            {
                recycler.ReceiveItemFromConveyor(cargoType);
                ClearCargo();
                return;
            }
        }

        // 2. Öncelik: Sıradaki Taşıma Bandı (ConveyorBelt)
        for (int i = 0; i < hits.Length; i++)
        {
            ConveyorBelt nextBelt = hits[i].GetComponentInParent<ConveyorBelt>();
            if (nextBelt != null && nextBelt != this && !nextBelt.HasCargo)
            {
                if (nextBelt.TryAcceptCargo(cargoType))
                {
                    ClearCargo();
                    return;
                }
            }
        }

        // 3. Öncelik: Çıkış / Satış Sandığı (ShippingBinMachine)
        for (int i = 0; i < hits.Length; i++)
        {
            ShippingBinMachine bin = hits[i].GetComponentInParent<ShippingBinMachine>();
            if (bin != null && !bin.IsFeedingBelt(this))
            {
                bin.ReceiveItemFromConveyor(cargoType);
                ClearCargo();
                return;
            }
        }
    }

    private void ClearCargo()
    {
        hasCargo = false;
        cargoProgress = 0f;
        if (visualCargoObj != null)
            visualCargoObj.SetActive(false);
    }

    private void OnTriggerStay(Collider other)
    {
        // 1. Toplanabilir atık veya hurda taşınması
        CollectibleItem item = other.GetComponent<CollectibleItem>();
        if (item != null)
        {
            if (!item.IsBeingPulled)
            {
                if (!hasCargo && TryAcceptCargo(item.itemType))
                {
                    Destroy(item.gameObject);
                    return;
                }

                Vector3 move = transform.forward * (beltSpeed * Time.deltaTime);
                item.transform.position += move;

                Vector3 localPos = transform.InverseTransformPoint(item.transform.position);
                localPos.x = Mathf.MoveTowards(localPos.x, 0f, 2.5f * Time.deltaTime);
                item.transform.position = transform.TransformPoint(localPos);
            }
            return;
        }

        // 2. Karakter bandın üzerindeyse hafif akıntı kuvveti
        CharacterController25D player = other.GetComponent<CharacterController25D>();
        if (player != null && player.RigidbodyComponent != null)
        {
            Vector3 pushForce = transform.forward * (beltSpeed * 4.5f);
            player.RigidbodyComponent.AddForce(pushForce, ForceMode.Acceleration);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 start = transform.position + Vector3.up * 0.15f;
        Vector3 end = start + transform.forward * 0.6f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.08f);
    }
}
