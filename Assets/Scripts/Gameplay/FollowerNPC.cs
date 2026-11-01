using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Takipçi ve Gönüllü Çevreci NPC (Follower & Assistant AI).
/// - Şehirde davamıza inanan ve arkamızda toplanan destekçilerden biridir.
/// - Oyuncuyu takip eder (Stardew/Factorio bot mantığı).
/// - 350x350'lik dev haritada seyahat yorgunluğunu çözmek için:
///    * Yerdeki atıkları (metal, plastik, biyo) tespit eder ve toplar.
///    * Kendi mini çantasında taşır (kapasite: 5-10 atık).
///    * Oyuncu üsse döndüğünde veya yakındaki bir Dönüştürücüye/Sandığa vardığında yükünü otomatik boşaltır!
/// - [E] tuşu ile modu değiştirilebilir: "Beni Takip Et" <-> "Burada Kal ve Otomasyona Yardım Et".
/// - Prosedürel voxel adım yaylanması (squash & stretch) ve sevimli baş üstü durum simgeleri içerir.
/// </summary>
public class FollowerNPC : MonoBehaviour, IInteractable
{
    public enum FollowerState
    {
        FollowingPlayer,
        MovingToScrap,
        DepositingToBase,
        StationaryHelper
    }

    [Header("--- TAKİPÇİ KİMLİĞİ VE DURUMU ---")]
    [SerializeField] private string followerName = "Eko-Gönüllü";
    [SerializeField] private FollowerState currentState = FollowerState.FollowingPlayer;
    [SerializeField] private int carryCapacity = 6;
    [SerializeField] private int currentCarriedCount = 0;

    [Header("--- HAREKET VE TAKİP AYARLARI ---")]
    [SerializeField] private float moveSpeed = 6.2f;
    [SerializeField] private float followDistance = 2.4f;
    [SerializeField] private float scrapDetectRadius = 7.5f;

    [Header("--- GÖRSEL VE YAYLANMA (SQUASH & STRETCH) ---")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject carriedItemVisual;
    [SerializeField] private float hopHeight = 0.08f;
    [SerializeField] private float hopSpeed = 12f;

    private Transform playerTransform;
    private CollectibleItem targetScrap;
    private RecyclerMachine nearbyRecycler;
    private ShippingBinMachine nearbyShippingBin;
    private Vector3 initialVisualScale;
    private Vector3 initialVisualPos;
    private float walkTimer;
    private float scanTimer;
    private bool isWalking;
    private Vector3 lastMoveDir = Vector3.forward;
    private bool isChildNPC = false;
    private float childHopTimer = 0f;

    private readonly List<ItemType> carriedItems = new List<ItemType>();

    public string FollowerName => followerName;
    public int CarriedCount => currentCarriedCount;
    public FollowerState State => currentState;
    public bool IsWalking => isWalking;
    public float MoveSpeed => moveSpeed;
    public Vector3 MoveDirection => lastMoveDir;

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (visualRoot == null)
        {
            var child = transform.Find("VisualRoot");
            visualRoot = child != null ? child : transform;
        }

        initialVisualScale = visualRoot.localScale;
        initialVisualPos = visualRoot.localPosition;
    }

    private void Start()
    {
        Vector3 p = transform.position;
        p.y = 0f;
        transform.position = p;

        isChildNPC = gameObject.name.ToLower().Contains("child");

        if (followerName == "Eko-Gönüllü" || string.IsNullOrEmpty(followerName))
        {
            bool isFemale = gameObject.name.ToLower().Contains("fem");
            followerName = isFemale ? $"Gönüllü {TurkishNameGenerator.GetRandomFemaleName()}" : $"Gönüllü {TurkishNameGenerator.GetRandomMaleName()}";
        }

        FindPlayer();
        UpdateCarriedVisual();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        switch (currentState)
        {
            case FollowerState.FollowingPlayer:
                HandleFollowingPlayer();
                break;

            case FollowerState.MovingToScrap:
                HandleMovingToScrap();
                break;

            case FollowerState.DepositingToBase:
                HandleDepositingToBase();
                break;

            case FollowerState.StationaryHelper:
                HandleStationary();
                break;
        }

        ApplyProceduralHop();
    }

    private void HandleFollowingPlayer()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.4f;

            // Yakında boşta atık varsa ve çantada yer varsa toplamaya git
            if (currentCarriedCount < carryCapacity)
            {
                CollectibleItem nearby = FindNearbyScrap();
                if (nearby != null)
                {
                    targetScrap = nearby;
                    currentState = FollowerState.MovingToScrap;
                    return;
                }
            }

            // Çanta doluysa ve yakında bir Dönüştürücü / Sandık varsa boşaltmaya git
            if (currentCarriedCount > 0)
            {
                if (CheckNearbyDepositStation())
                {
                    currentState = FollowerState.DepositingToBase;
                    return;
                }
            }
        }

        // Oyuncuyu takip et
        if (distToPlayer > followDistance)
        {
            Vector3 target = playerTransform.position - (playerTransform.forward * (followDistance * 0.7f));
            MoveTowardsTarget(target);
            isWalking = true;
        }
        else
        {
            isWalking = false;
        }
    }

    private void HandleMovingToScrap()
    {
        if (targetScrap == null || !targetScrap.gameObject.activeInHierarchy || targetScrap.IsBeingPulled)
        {
            targetScrap = null;
            currentState = FollowerState.FollowingPlayer;
            return;
        }

        float dist = Vector3.Distance(transform.position, targetScrap.transform.position);
        if (dist > 0.8f)
        {
            MoveTowardsTarget(targetScrap.transform.position);
            isWalking = true;
        }
        else
        {
            // Hurdayı topla
            ItemType itemType = targetScrap.ItemType;
            Destroy(targetScrap.gameObject);
            targetScrap = null;

            carriedItems.Add(itemType);
            currentCarriedCount = carriedItems.Count;
            UpdateCarriedVisual();

            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.Notify($"{followerName} bir atık topladı! ({currentCarriedCount}/{carryCapacity})");
            }

            if (EcoSystemManager.Instance != null)
            {
                EcoSystemManager.Instance.RegisterWasteCollected(itemType);
            }

            if (currentCarriedCount >= carryCapacity)
            {
                currentState = FollowerState.FollowingPlayer;
            }
            else
            {
                currentState = FollowerState.FollowingPlayer;
            }
        }
    }

    private void HandleDepositingToBase()
    {
        Vector3 targetPos = nearbyRecycler != null ? nearbyRecycler.transform.position :
                            nearbyShippingBin != null ? nearbyShippingBin.transform.position : transform.position;

        float dist = Vector3.Distance(transform.position, targetPos);
        if (dist > 1.2f)
        {
            MoveTowardsTarget(targetPos);
            isWalking = true;
        }
        else
        {
            // Yükü boşalt
            isWalking = false;
            foreach (var item in carriedItems)
            {
                if (nearbyRecycler != null)
                {
                    nearbyRecycler.ReceiveItemFromConveyor(item);
                }
                else if (nearbyShippingBin != null)
                {
                    nearbyShippingBin.ReceiveItemFromConveyor(item);
                }
                else if (PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.AddItem(item, 1);
                }
            }

            carriedItems.Clear();
            currentCarriedCount = 0;
            nearbyRecycler = null;
            nearbyShippingBin = null;
            UpdateCarriedVisual();

            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.Notify($"{followerName} taşınan atıkları üs tesisine boşalttı!");
            }

            currentState = FollowerState.FollowingPlayer;
        }
    }

    private void HandleStationary()
    {
        isWalking = false;

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.5f;
            // Sabit dururken çok yakınındaki (4m) atıkları temizleyip hemen yerindeki sandığa/makineye atar
            if (currentCarriedCount < carryCapacity)
            {
                CollectibleItem nearby = FindNearbyScrap(4.5f);
                if (nearby != null)
                {
                    targetScrap = nearby;
                    currentState = FollowerState.MovingToScrap;
                }
            }
        }
    }

    private void MoveTowardsTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0f; // Yatay düzlemde kal

        if (dir.sqrMagnitude > 0.001f)
        {
            lastMoveDir = dir.normalized;
            Vector3 step = dir.normalized * (moveSpeed * Time.deltaTime);
            Vector3 nextPos = transform.position + step;

            float yHop = 0f;
            if (isChildNPC)
            {
                float hopSine = Mathf.Sin(Time.time * 11f);
                yHop = Mathf.Abs(hopSine) * 0.26f;
                childHopTimer -= Time.deltaTime;
                if (hopSine > 0.88f && childHopTimer <= 0f)
                {
                    childHopTimer = 0.28f;
                    GameAudioAndJuiceManager.Instance?.PlayJumpSound(transform.position, volumeMultiplier: 0.35f, customPitch: 1.35f);
                }
            }
            nextPos.y = yHop;
            transform.position = nextPos;

            Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 14f * Time.deltaTime);
        }
    }

    private CollectibleItem FindNearbyScrap(float radius = -1f)
    {
        float r = radius > 0f ? radius : scrapDetectRadius;
        Collider[] hits = Physics.OverlapSphere(transform.position, r);
        CollectibleItem closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var scrap = hit.GetComponent<CollectibleItem>();
            if (scrap != null && !scrap.IsBeingPulled && scrap.gameObject.activeInHierarchy)
            {
                float d = Vector3.Distance(transform.position, scrap.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = scrap;
                }
            }
        }
        return closest;
    }

    private bool CheckNearbyDepositStation()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 6.0f);
        foreach (var hit in hits)
        {
            var recycler = hit.GetComponentInParent<RecyclerMachine>();
            if (recycler != null)
            {
                nearbyRecycler = recycler;
                return true;
            }

            var bin = hit.GetComponentInParent<ShippingBinMachine>();
            if (bin != null)
            {
                nearbyShippingBin = bin;
                return true;
            }
        }
        return false;
    }

    private void ApplyProceduralHop()
    {
        // Eğer gelişmiş 3 karelik VoxelCharacterAnimator bileşeni varsa prosedürel animasyonları o yönetsin
        if (GetComponent<VoxelCharacterAnimator>() != null) return;
        if (visualRoot == null) return;

        if (isWalking)
        {
            walkTimer += Time.deltaTime * hopSpeed;
            float hop = Mathf.Abs(Mathf.Sin(walkTimer)) * hopHeight;
            visualRoot.localPosition = initialVisualPos + new Vector3(0f, hop, 0f);

            // Tatlı yaylanma
            float squash = Mathf.Sin(walkTimer) * 0.06f;
            visualRoot.localScale = initialVisualScale + new Vector3(squash, -squash, squash);
        }
        else
        {
            walkTimer = 0f;
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, initialVisualPos, 15f * Time.deltaTime);
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, initialVisualScale, 15f * Time.deltaTime);
        }
    }

    private void UpdateCarriedVisual()
    {
        if (carriedItemVisual != null)
        {
            carriedItemVisual.SetActive(currentCarriedCount > 0);
        }
    }

    private void FindPlayer()
    {
        var cc = FindFirstObjectByType<CharacterController25D>();
        if (cc != null) playerTransform = cc.transform;
    }

    public string GetInteractionPrompt()
    {
        string modeStr = currentState == FollowerState.StationaryHelper ? "Beni Takip Et" : "Burada Kal ve Otomasyona Yardım Et";
        return $"[E] {followerName}: {modeStr} (Taşıdığı: {currentCarriedCount}/{carryCapacity})";
    }

    public void Interact(GameObject interactor)
    {
        if (currentState == FollowerState.StationaryHelper)
        {
            currentState = FollowerState.FollowingPlayer;
            PlayerInventory.Instance?.Notify($"{followerName} artık seni takip ediyor ve atık taşıyor!");
        }
        else
        {
            currentState = FollowerState.StationaryHelper;
            PlayerInventory.Instance?.Notify($"{followerName} bu noktada kaldı ve çevredeki otomasyona yardım ediyor!");
        }
    }

    public Vector3 GetPosition() => transform.position;
}

