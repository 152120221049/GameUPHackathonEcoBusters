using UnityEngine;

/// <summary>
/// Sahnedeki toplanabilir hurda ve atık nesnesi.
/// - Yerde tamamen sabit durur (idle dönme/zıplama yoktur).
/// - Vakum aktifken oyuncuya doğru ivmelenerek uçar.
/// - Çanta doluysa veya vakum bırakıldığında havada asılı kalmaz;
///   oyuncunun üzerinden dışarı itilerek yerçekimiyle zemine doğal bir şekilde düşer.
/// - Belirli bir batmışlık (gömülme) derinliği aralığında yere oturur.
/// </summary>
public class CollectibleItem : MonoBehaviour, IInteractable
{
    [Header("--- EŞYA VE DEĞER BİLGİSİ ---")]
    [Tooltip("Bu atığın türü")]
    public ItemType itemType = ItemType.ScrapMetal;
    public ItemType ItemType => itemType;
    [Tooltip("Toplandığında çantaya kaç adet ekleneceği")]
    public int amount = 1;

    [Header("--- VAKUM / ÇEKİM VE DÜŞÜŞ AYARLARI ---")]
    [SerializeField] private float collectDistance = 1.05f;
    [Tooltip("Vakum bırakıldığında veya itildiğinde uygulanan yerçekimi")]
    [SerializeField] private float gravity = 25f;
    [Tooltip("Yere çarptığında sekme katsayısı")]
    [SerializeField] private float bounceFactor = 0.2f;

    [Header("--- BATMIŞLIK / GÖMÜLME HİSSİYATI ---")]
    [Tooltip("Zemine gömülme derinliği aralığı")]
    [SerializeField] private float minBurialOffset = -0.06f;
    [SerializeField] private float maxBurialOffset = 0.05f;

    private Vector3 initialScale;
    private bool isBeingPulled;
    private Transform pullTarget;
    private float currentPullSpeed;

    // Yere düşüş fiziği ve gömülme seviyesi
    private bool isFalling;
    private float fallVelocityY;
    private float groundSurfaceY = 0f;
    private float burialOffset = 0f;

    private GroundTile parentTile;
    private System.Action onCollectedCallback;

    public bool IsBeingPulled => isBeingPulled;

    private void Awake()
    {
        SetupTriggerColliderImmediate();
    }

    private void Start()
    {
        initialScale = transform.localScale;

        // Her atık için zemine hafifçe batmışlık derinliği belirle
        burialOffset = Random.Range(minBurialOffset, maxBurialOffset);

        SetupTriggerColliderImmediate();

        // Başlangıçta zeminin yüksekliğini tespit et
        DetectGroundLevel();

        // Eğer havada doğduysa zemine doğru düşüşü başlat
        if (transform.position.y > groundSurfaceY + 0.08f)
        {
            isFalling = true;
            fallVelocityY = 0f;
        }
        else
        {
            // Doğrudan zemine yerleş
            Vector3 pos = transform.position;
            pos.y = groundSurfaceY;
            transform.position = pos;
        }
    }

    private void SetupTriggerColliderImmediate()
    {
        // Destroy() kare sonunda çalıştığı için MeshCollider'ı ANINDA devre dışı bırak (enabled = false) ve sil.
        // Aksi halde hem karakter doğan çöpe fiziksel olarak takılır hem de GetComponent<Collider>() hala
        // silinmeyi bekleyen içbükey MeshCollider'ı döndürüp "Triggers on concave MeshColliders" hatası fırlatır!
        foreach (var mc in GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc != null)
            {
                mc.enabled = false;
                Destroy(mc);
            }
        }

        SphereCollider sc = GetComponent<SphereCollider>();
        BoxCollider bc = GetComponent<BoxCollider>();

        if (sc != null)
        {
            sc.isTrigger = true;
        }
        else if (bc != null)
        {
            bc.isTrigger = true;
        }
        else
        {
            sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.6f;
            sc.isTrigger = true;
        }
    }

    public void Initialize(ItemType type, int itemAmount, GroundTile tile = null, System.Action onCollected = null)
    {
        itemType = type;
        amount = itemAmount;
        parentTile = tile;
        onCollectedCallback = onCollected;

        if (parentTile != null)
        {
            parentTile.isOccupied = true;
        }
    }

    private void Update()
    {
        if (isBeingPulled && pullTarget != null)
        {
            UpdateVacuumPull();
        }
        else if (isFalling)
        {
            UpdateFalling();
        }
    }

    private void UpdateVacuumPull()
    {
        Vector3 targetPos = pullTarget.position + Vector3.up * 0.6f;
        currentPullSpeed += 32f * Time.deltaTime;

        transform.position = Vector3.MoveTowards(transform.position, targetPos, currentPullSpeed * Time.deltaTime);
        transform.Rotate(new Vector3(180f, 480f, 120f) * Time.deltaTime, Space.Self);

        float dist = Vector3.Distance(transform.position, targetPos);

        // Vakumun içine girerken elastik sündürme ve küçülme efekti (Oomph)
        if (dist < 2.4f)
        {
            float t = Mathf.Clamp01(dist / 2.4f);
            float scaleXZ = Mathf.Lerp(0.22f, 1.15f, t);
            float scaleY = Mathf.Lerp(0.35f, 0.85f, t);
            transform.localScale = new Vector3(initialScale.x * scaleXZ, initialScale.y * scaleY, initialScale.z * scaleXZ);
        }

        if (dist <= collectDistance)
        {
            TryCollect();
        }
    }

    private void UpdateFalling()
    {
        fallVelocityY -= gravity * Time.deltaTime;
        Vector3 newPos = transform.position + new Vector3(0, fallVelocityY * Time.deltaTime, 0);

        // Zemine ulaştı mı?
        if (newPos.y <= groundSurfaceY)
        {
            newPos.y = groundSurfaceY;

            // Yeterli hız varsa hafif bir sekme yap, yoksa tamamen sabitle
            if (Mathf.Abs(fallVelocityY) > 3.0f)
            {
                fallVelocityY = -fallVelocityY * bounceFactor;
            }
            else
            {
                fallVelocityY = 0f;
                isFalling = false;
            }
        }

        transform.position = newPos;
    }

    /// <summary>
    /// Aşağı doğru raycast atarak zemini tespit eder.
    /// OYUNCUYU ve DİĞER TOPLANABİLİRLERİ KESİNLİKLE FİLTRELER (havada asılı kalmayı önler).
    /// </summary>
    private void DetectGroundLevel()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 4f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 15f, ~0, QueryTriggerInteraction.Ignore);
        float bestGroundY = -999f;

        foreach (var hit in hits)
        {
            // Oyuncu veya Karakter Kontrolcüsünü atla
            if (hit.collider.CompareTag("Player") ||
                hit.collider.GetComponentInParent<CharacterController25D>() != null)
                continue;

            // Kendisini veya başka bir toplanabilir hurdayı atla
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<CollectibleItem>() != null)
                continue;

            if (hit.point.y > bestGroundY)
            {
                bestGroundY = hit.point.y;
            }
        }

        if (bestGroundY > -900f)
        {
            groundSurfaceY = bestGroundY + burialOffset;
        }
        else
        {
            groundSurfaceY = (transform.position.y > 0.4f ? 0.05f : transform.position.y) + burialOffset;
        }
    }

    public void PullTowards(Transform target, float basePullSpeed)
    {
        if (!isBeingPulled)
        {
            isBeingPulled = true;
            isFalling = false;
            pullTarget = target;
            currentPullSpeed = basePullSpeed;
        }
    }

    /// <summary>
    /// Vakum bırakıldığında çağrılır.
    /// </summary>
    public void StopPull()
    {
        if (isBeingPulled)
        {
            isBeingPulled = false;
            pullTarget = null;
            transform.localScale = initialScale;

            DetectGroundLevel();
            isFalling = true;
            fallVelocityY = -2f;
        }
    }

    /// <summary>
    /// Çanta doluyken veya oyuncunun dibinde sıkışmaması için nesneyi oyuncudan dışarı iter ve zemine düşürür.
    /// </summary>
    public void StopPullAndPushAway(Vector3 playerPos)
    {
        isBeingPulled = false;
        pullTarget = null;
        transform.localScale = initialScale;

        // Oyuncudan dışarı doğru yatay yön hesapla
        Vector3 pushDir = transform.position - playerPos;
        pushDir.y = 0f;

        if (pushDir.sqrMagnitude < 0.04f)
        {
            pushDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
        }
        else
        {
            pushDir.Normalize();
        }

        // Oyuncunun gövdesinden en az 1.1 birim dışarı al
        transform.position += pushDir * 0.9f;

        DetectGroundLevel();
        isFalling = true;
        fallVelocityY = -2.5f;
    }

    private void TryCollect()
    {
        if (PlayerInventory.Instance != null)
        {
            if (PlayerInventory.Instance.AddItem(itemType, amount))
            {
                if (EcoSystemManager.Instance != null)
                {
                    EcoSystemManager.Instance.RegisterWasteCollected(itemType);
                }

                if (GameAudioAndJuiceManager.Instance != null)
                {
                    GameAudioAndJuiceManager.Instance.TriggerItemCollectedOomph(transform.position, itemType, amount, pullTarget);
                }

                if (parentTile != null)
                {
                    parentTile.isOccupied = false;
                }
                onCollectedCallback?.Invoke();
                Destroy(gameObject);
            }
            else
            {
                // Çanta dolu olduğu için toplayamadı!
                if (GameAudioAndJuiceManager.Instance != null)
                {
                    GameAudioAndJuiceManager.Instance.PlayErrorSound(transform.position, "ÇANTA DOLU!");
                }
                Vector3 playerPos = pullTarget != null ? pullTarget.position : transform.position;
                StopPullAndPushAway(playerPos);
            }
        }
        else
        {
            StopPull();
        }
    }

    public string GetInteractionPrompt()
    {
        return $"[Sol Tık / E] {ItemDatabase.GetDisplayName(itemType)} Topla";
    }

    public void Interact(GameObject interactor)
    {
        pullTarget = interactor != null ? interactor.transform : transform;
        TryCollect();
    }

    public Vector3 GetPosition() => transform.position;
}
