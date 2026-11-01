using UnityEngine;

/// <summary>
/// Şehirde gezinen, yaşayan ve oyuncunun Eko-İtibarı (Community Reputation) arttıkça
/// [E] tuşuyla takıma katılmaya ikna edilebilen Şehir Vatandaşı / Çocuk NPC.
/// - Takıma katılana kadar şehir sokaklarında sakince dolaşır (Wander).
/// - Takıma katıldığında FollowerNPC'ye dönüşerek oyuncuyu takip eder ve etraftaki atıkları toplar!
/// </summary>
public class CityCitizenNPC : MonoBehaviour, IInteractable
{
    [Header("--- VATANDAŞ KİMLİĞİ ---")]
    [SerializeField] private string citizenName = "Şehir Sakini";
    [SerializeField] private bool isChild = false;
    [Tooltip("Bu vatandaşı takıma katmak için gereken minimum Halk İtibarı")]
    [SerializeField] private float requiredReputationToRecruit = 10f;

    [Header("--- ŞEHİRDE GEZİNME (WANDER) AYARLARI ---")]
    [SerializeField] private float wanderSpeed = 2.6f;
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private Vector2 idleWaitTimeRange = new Vector2(2.0f, 5.0f);

    private Vector3 homePosition;
    private Vector3 currentWanderTarget;
    private float waitTimer;
    private bool isWaiting;
    private bool isRecruited = false;
    private Vector3 lastMoveDir = Vector3.forward;
    private float hopTimer = 0f;

    private FollowerNPC followerComp;
    private VoxelCharacterAnimator voxelAnimator;

    public bool IsWalking => !isRecruited && !isWaiting;
    public float WanderSpeed => wanderSpeed;
    public Vector3 MoveDirection => lastMoveDir;

    private void Awake()
    {
        followerComp = GetComponent<FollowerNPC>();
        voxelAnimator = GetComponent<VoxelCharacterAnimator>();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Başlangıçta henüz takıma katılmadığı için FollowerNPC'yi devre dışı tut
        if (followerComp != null)
        {
            followerComp.enabled = false;
        }
    }

    private void Start()
    {
        Vector3 p = transform.position;
        p.y = 0f;
        transform.position = p;
        homePosition = p;

        if (gameObject.name.ToLower().Contains("child"))
        {
            isChild = true;
        }

        if (citizenName == "Şehir Sakini" || string.IsNullOrEmpty(citizenName))
        {
            if (isChild)
            {
                citizenName = TurkishNameGenerator.GetRandomChildName();
            }
            else
            {
                // Model isminden veya rastgele kadın/erkek seç
                bool isFemale = gameObject.name.ToLower().Contains("fem");
                citizenName = isFemale ? TurkishNameGenerator.GetRandomFemaleName() : TurkishNameGenerator.GetRandomMaleName();
            }
        }

        PickNewWanderTarget();
    }

    public void InitializeCitizen(string cName, bool child, float reqRep)
    {
        citizenName = cName;
        isChild = child;
        requiredReputationToRecruit = reqRep;
        homePosition = transform.position;
    }

    private void Update()
    {
        if (isRecruited) return;

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                PickNewWanderTarget();
            }
            return;
        }

        // Hedefe doğru yürü
        Vector3 dir = currentWanderTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.25f)
        {
            isWaiting = true;
            waitTimer = Random.Range(idleWaitTimeRange.x, idleWaitTimeRange.y);
            Vector3 groundPos = transform.position;
            groundPos.y = 0f;
            transform.position = groundPos;
            return;
        }

        Vector3 moveDir = dir.normalized;
        lastMoveDir = moveDir;
        // Önünde bina/engel varsa yeni yön seç
        if (Physics.Raycast(transform.position + Vector3.up * 0.65f, moveDir, 0.9f, ~0, QueryTriggerInteraction.Ignore))
        {
            isWaiting = true;
            waitTimer = Random.Range(1.0f, 2.5f);
            Vector3 groundPos = transform.position;
            groundPos.y = 0f;
            transform.position = groundPos;
            return;
        }

        // Çocuk NPC'ler için sevimli zıplama/hop adımları ve Jump sesi efekti
        float yHop = 0f;
        if (isChild)
        {
            float hopSine = Mathf.Sin(Time.time * 11f);
            yHop = Mathf.Abs(hopSine) * 0.26f;
            hopTimer -= Time.deltaTime;
            if (hopSine > 0.88f && hopTimer <= 0f)
            {
                hopTimer = 0.28f;
                GameAudioAndJuiceManager.Instance?.PlayJumpSound(transform.position, volumeMultiplier: 0.35f, customPitch: 1.35f);
            }
        }

        Vector3 nextPos = transform.position + moveDir * (wanderSpeed * Time.deltaTime);
        nextPos.y = yHop;
        transform.position = nextPos;
        Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
    }

    private void PickNewWanderTarget()
    {
        Vector2 randCircle = Random.insideUnitCircle * wanderRadius;
        currentWanderTarget = homePosition + new Vector3(randCircle.x, 0f, randCircle.y);
    }

    public string GetInteractionPrompt()
    {
        if (isRecruited && followerComp != null)
        {
            return followerComp.GetInteractionPrompt();
        }

        float currentRep = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CommunityReputation : 0f;
        if (currentRep >= requiredReputationToRecruit)
        {
            string roleDesc = isChild ? "Genç Eko-İzci" : "Gönüllü Vatandaş";
            return $"[E] {citizenName} ({roleDesc}): Bize Katılmaya İkna Et!";
        }
        else
        {
            return $"{citizenName}: \"Adayı temizlediğini görmek umut verici...\" (Gereken İtibar: {Mathf.CeilToInt(requiredReputationToRecruit)})";
        }
    }

    public void Interact(GameObject interactor)
    {
        if (isRecruited)
        {
            if (followerComp != null) followerComp.Interact(interactor);
            return;
        }

        float currentRep = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CommunityReputation : 0f;
        if (currentRep >= requiredReputationToRecruit)
        {
            RecruitToTeam();
        }
        else
        {
            PlayerInventory.Instance?.Notify($"{citizenName} henüz ikna olmadı. Biraz daha atık temizleyip Halk İtibarını artır! ({Mathf.FloorToInt(currentRep)}/{Mathf.CeilToInt(requiredReputationToRecruit)})");
        }
    }

    private void RecruitToTeam()
    {
        isRecruited = true;

        if (followerComp == null)
        {
            followerComp = gameObject.AddComponent<FollowerNPC>();
        }

        followerComp.enabled = true;
        EcoSystemManager.Instance?.RegisterFollowerRecruited();
        PlayerInventory.Instance?.Notify($"{citizenName} Eko-Mucit takımına katıldı! Artık çevredeki atıkları topluyor.");

        // Bu bileşeni devre dışı bırak (bundan sonra FollowerNPC yönetecek)
        this.enabled = false;
    }

    public Vector3 GetPosition() => transform.position;
}
