using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Otomatik Vakum Kulesi (Auto-Vacuum Turret).
/// - 1x1 karo boyutundadır ([B] İnşa Modunda [5] tuşuyla kurulur).
/// - Çevresindeki (9m) atıkları otomatik olarak tespit edip vakumlar.
/// - Yanına bir Konveyör Bant (ConveyorBelt) veya Geri Dönüşüm Presi kurulduğunda,
///   çektiği hurdaları doğrudan banda bırakarak tam otomatik fabrika hattı oluşturur!
/// - Yanında bant yoksa topladığı atıkları kendi haznesinde biriktirir ([E] ile alınır).
/// </summary>
public class AutoVacuumTurret : MonoBehaviour, IInteractable
{
    [Header("--- VAKUM KULESİ AYARLARI ---")]
    [SerializeField] private float suctionRadius = 9.5f;
    [SerializeField] private float pullSpeed = 7.5f;
    [SerializeField] private int storageCapacity = 15;
    [SerializeField] private Transform turretHead;

    private readonly List<ItemType> storedItems = new List<ItemType>();
    private CollectibleItem currentTarget;
    private float scanTimer;
    private float feedTimer;

    private void Awake()
    {
        BuildVisualsIfNeeded();
    }

    private void BuildVisualsIfNeeded()
    {
        if (transform.childCount == 0)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            GameObject basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePedestal.name = "TurretBase";
            basePedestal.transform.SetParent(transform, false);
            basePedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            basePedestal.transform.localScale = new Vector3(0.85f, 0.7f, 0.85f);
            Destroy(basePedestal.GetComponent<Collider>());
            if (sh != null)
            {
                Material m = new Material(sh);
                m.color = new Color(0.22f, 0.28f, 0.35f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color);
                basePedestal.GetComponent<Renderer>().material = m;
            }

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "TurretHead";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            head.transform.localScale = new Vector3(0.55f, 0.45f, 0.75f);
            Destroy(head.GetComponent<Collider>());
            if (sh != null)
            {
                Material m = new Material(sh);
                m.color = new Color(0.2f, 0.85f, 0.95f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color);
                head.GetComponent<Renderer>().material = m;
            }

            turretHead = head.transform;
        }
        else if (turretHead == null)
        {
            turretHead = transform.Find("TurretHead") ?? transform.GetChild(0);
        }

        if (GetComponent<Collider>() == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.9f, 1.3f, 0.9f);
            bc.center = new Vector3(0f, 0.65f, 0f);
        }
    }

    private void Update()
    {
        // 1. Haznede eşya varsa yanındaki banda veya prese aktar
        if (storedItems.Count > 0)
        {
            feedTimer -= Time.deltaTime;
            if (feedTimer <= 0f)
            {
                feedTimer = 0.55f;
                TryOutputToAdjacentBeltOrMachine();
            }
        }

        // 2. Hedef atık varsa kendine çek
        if (currentTarget != null)
        {
            if (!currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                return;
            }

            Vector3 dirToTarget = currentTarget.transform.position - transform.position;
            dirToTarget.y = 0f;
            if (turretHead != null && dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget.normalized, Vector3.up);
                turretHead.rotation = Quaternion.Slerp(turretHead.rotation, targetRot, 10f * Time.deltaTime);
            }

            Vector3 pullPos = transform.position + Vector3.up * 0.8f;
            currentTarget.transform.position = Vector3.MoveTowards(currentTarget.transform.position, pullPos, pullSpeed * Time.deltaTime);

            if (Vector3.Distance(currentTarget.transform.position, pullPos) < 0.45f)
            {
                ItemType type = currentTarget.itemType;
                Destroy(currentTarget.gameObject);
                currentTarget = null;

                storedItems.Add(type);
                EcoSystemManager.Instance?.RegisterWasteCollected(type);
                TryOutputToAdjacentBeltOrMachine();
            }
            return;
        }

        // 3. Yeni hedef tara (0.75 saniyede bir, performans dostu)
        if (storedItems.Count < storageCapacity)
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.75f;
                FindNextScrapTarget();
            }
        }

        if (turretHead != null)
        {
            turretHead.Rotate(Vector3.up, 25f * Time.deltaTime, Space.Self);
        }
    }

    private void FindNextScrapTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, suctionRadius, ~0, QueryTriggerInteraction.Collide);
        float bestDist = float.MaxValue;
        CollectibleItem best = null;

        for (int i = 0; i < hits.Length; i++)
        {
            CollectibleItem item = hits[i].GetComponent<CollectibleItem>();
            if (item != null && !item.IsBeingPulled && item.gameObject.activeInHierarchy)
            {
                float d = (item.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = item;
                }
            }
        }

        currentTarget = best;
    }

    private void TryOutputToAdjacentBeltOrMachine()
    {
        if (storedItems.Count == 0) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, 1.85f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            ConveyorBelt belt = hits[i].GetComponentInParent<ConveyorBelt>();
            if (belt != null && !belt.HasCargo)
            {
                ItemType item = storedItems[0];
                if (belt.TryAcceptCargo(item))
                {
                    storedItems.RemoveAt(0);
                    return;
                }
            }

            RecyclerMachine recycler = hits[i].GetComponentInParent<RecyclerMachine>();
            if (recycler != null && recycler.CanAcceptFromConveyor())
            {
                ItemType item = storedItems[0];
                storedItems.RemoveAt(0);
                recycler.ReceiveItemFromConveyor(item);
                return;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (storedItems.Count > 0)
        {
            return $"[E] Otomatik Vakum Kulesi: Haznedeki {storedItems.Count} Atığı Al (Veya Yanına Bant Kur)";
        }
        return $"Otomatik Vakum Kulesi (Çevreyi Tarıyor... Menzil: {suctionRadius}m)";
    }

    public void Interact(GameObject interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        if (storedItems.Count == 0)
        {
            inv.Notify("Vakum Kulesi çevredeki atıkları otomatik çekiyor. Yanına Konveyör Bant kurarak prese bağlayabilirsin!");
            return;
        }

        int collected = 0;
        for (int i = storedItems.Count - 1; i >= 0; i--)
        {
            if (inv.AddItem(storedItems[i], 1))
            {
                storedItems.RemoveAt(i);
                collected++;
            }
            else
            {
                break;
            }
        }

        if (collected > 0)
        {
            inv.Notify($"Vakum Kulesinden {collected} adet atık çantaya alındı!");
        }
    }

    public Vector3 GetPosition() => transform.position;
}

