using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Eko-Mucit Karakter Etkileşim ve Vakum/Mıknatıs Sistemi.
/// - Sol Tık veya [Space] basılı tutulduğunda çevredeki atıkları/hurdaları vakumla kendine çeker.
/// - Çanta doluysa fazla atıkları çekmez; havadakileri dışarı itip yere tok bir şekilde düşürür.
/// - Yakındaki etkileşimli nesneleri (IInteractable: Atölye, Tarla, Fabrika vb.) algılar ve [E] tuşuyla etkileşime girer.
/// </summary>
public class PlayerInteractionController : MonoBehaviour
{
    [Header("--- VAKUM / MIKNATIS SİSTEMİ ---")]
    [Tooltip("Vakumun çevredeki hurdaları çekme yarıçapı")]
    [SerializeField] private float vacuumRadius = 5.0f;
    [Tooltip("Vakum başlangıç çekim hızı")]
    [SerializeField] private float vacuumPullSpeed = 6.5f;
    [Tooltip("Vakuma basmadan da tam üstünden geçince otomatik toplama mesafesi")]
    [SerializeField] private float autoPickupRadius = 1.1f;

    [Header("--- NESNE ETKİLEŞİM (INTERACT) SİSTEMİ ---")]
    [Tooltip("Atölye, makine veya toprakla etkileşime geçme mesafesi")]
    [SerializeField] private float interactionRadius = 3.8f;

    private bool isVacuumActive;
    private IInteractable currentInteractable;
    private readonly HashSet<CollectibleItem> currentlyPulledItems = new HashSet<CollectibleItem>();
    private readonly HashSet<CollectibleItem> foundThisFrame = new HashSet<CollectibleItem>();
    private readonly Collider[] overlapBuffer = new Collider[64];
    private float lastBagFullNotifyTime;
    private float interactDetectTimer;
    private ParticleSystem vacuumSuctionVFX;

    public bool IsVacuumActive => isVacuumActive;
    public IInteractable CurrentInteractable => currentInteractable;
    public float VacuumRadius => vacuumRadius;
    public float VacuumPullSpeed => vacuumPullSpeed;

    private void Awake()
    {
        // Oyuncuda PlayerInventory yoksa otomatik ekle
        if (GetComponent<PlayerInventory>() == null && PlayerInventory.Instance == null)
        {
            gameObject.AddComponent<PlayerInventory>();
        }

        // Oyuncuda PlayerProgression yoksa otomatik ekle
        if (GetComponent<PlayerProgression>() == null && PlayerProgression.Instance == null)
        {
            gameObject.AddComponent<PlayerProgression>();
        }
    }

    private void Start()
    {
        EnsureVacuumSuctionVFX();
    }

    private void Update()
    {
        HandleInputs();
        UpdateVacuum();
        UpdateVacuumSuctionVFX();

        interactDetectTimer -= Time.deltaTime;
        if (interactDetectTimer <= 0f)
        {
            interactDetectTimer = 0.08f;
            DetectClosestInteractable();
        }
    }

    public void SetVacuumStats(float newRadius, float newPullSpeed)
    {
        vacuumRadius = Mathf.Max(3f, newRadius);
        vacuumPullSpeed = Mathf.Max(4f, newPullSpeed);
    }

    private void HandleInputs()
    {
        bool interactPressed = false;
        bool leftClickAction = false;
        isVacuumActive = false;

        bool hasVacuum = PlayerProgression.Instance != null && PlayerProgression.Instance.HasVacuumUnlocked;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        var gamepad = Gamepad.current;

        if (keyboard != null)
        {
            // Vakum: Space veya V tuşu basılı tutulduğunda (vakum açıldıysa)
            if (hasVacuum && (keyboard.spaceKey.isPressed || keyboard.vKey.isPressed))
                isVacuumActive = true;

            // Etkileşim: E veya F tuşuna basıldığında
            if (keyboard.eKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
                interactPressed = true;
        }

        if (mouse != null)
        {
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

            if (!overUI)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    leftClickAction = true;
                }

                // Sağ Tık veya vakum tuşu basılı tutulduğunda alan vakumu çekimi (vakum açıldıysa)
                if (hasVacuum && mouse.rightButton.isPressed)
                {
                    isVacuumActive = true;
                }
            }
        }

        if (gamepad != null)
        {
            if (hasVacuum && (gamepad.buttonWest.isPressed || gamepad.leftTrigger.isPressed))
                isVacuumActive = true;

            if (gamepad.buttonSouth.wasPressedThisFrame)
                interactPressed = true;
        }
#else
        bool hasVacuum = PlayerProgression.Instance != null && PlayerProgression.Instance.HasVacuumUnlocked;
        isVacuumActive = hasVacuum && (Input.GetMouseButton(1) || Input.GetKey(KeyCode.Space));
        leftClickAction = Input.GetMouseButtonDown(0);
        interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
#endif

        // Çöp Çubuğu ile veya Elle Tek Tek Seçerek Toplama ([Sol Tık] veya [E])
        if ((interactPressed || leftClickAction) && currentInteractable != null)
        {
            var anim = GetComponent<VoxelCharacterAnimator>();
            if (anim != null) anim.PlayStickSwingAnimation();

            currentInteractable.Interact(gameObject);
        }
    }

    private void UpdateVacuum()
    {
        float activeRadius = isVacuumActive ? vacuumRadius : autoPickupRadius;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, activeRadius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);

        foundThisFrame.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            CollectibleItem collectible = overlapBuffer[i].GetComponent<CollectibleItem>();
            if (collectible == null)
                collectible = overlapBuffer[i].GetComponentInParent<CollectibleItem>();

            if (collectible != null)
            {
                // Çantada bu eşya için yer yoksa ASLA ÇEKME!
                if (PlayerInventory.Instance != null && !PlayerInventory.Instance.CanAddItem(collectible.itemType, 1))
                {
                    if (currentlyPulledItems.Contains(collectible))
                    {
                        collectible.StopPullAndPushAway(transform.position);
                        currentlyPulledItems.Remove(collectible);
                    }

                    if (isVacuumActive && Time.time - lastBagFullNotifyTime > 2.5f)
                    {
                        lastBagFullNotifyTime = Time.time;
                        PlayerInventory.Instance.Notify($"Çanta Dolu! ({PlayerInventory.Instance.TotalWasteCount}/{PlayerInventory.Instance.BackpackCapacity}) [Atölyede İcat Üret veya Yükselt]");
                    }
                    continue;
                }

                foundThisFrame.Add(collectible);
                currentlyPulledItems.Add(collectible);
                collectible.PullTowards(transform, vacuumPullSpeed);
            }
        }

        // Menzil dışına çıkan, vakum bırakıldığında veya çanta dolduğunda serbest kalan nesneleri durdur
        if (currentlyPulledItems.Count > 0)
        {
            List<CollectibleItem> toRelease = null;
            foreach (var item in currentlyPulledItems)
            {
                if (item == null)
                {
                    if (toRelease == null) toRelease = new List<CollectibleItem>();
                    toRelease.Add(item);
                    continue;
                }

                bool inventoryFull = PlayerInventory.Instance != null && !PlayerInventory.Instance.CanAddItem(item.itemType, 1);
                if (inventoryFull || (!isVacuumActive && !foundThisFrame.Contains(item)))
                {
                    if (toRelease == null) toRelease = new List<CollectibleItem>();
                    toRelease.Add(item);
                }
            }

            if (toRelease != null)
            {
                for (int i = 0; i < toRelease.Count; i++)
                {
                    if (toRelease[i] != null)
                    {
                        toRelease[i].StopPullAndPushAway(transform.position);
                    }
                    currentlyPulledItems.Remove(toRelease[i]);
                }
            }
        }

        if (GameAudioAndJuiceManager.Instance != null)
        {
            GameAudioAndJuiceManager.Instance.SetVacuumSoundActive(isVacuumActive, currentlyPulledItems.Count);
        }
    }

    private void DetectClosestInteractable()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, interactionRadius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);

        IInteractable closest = null;
        float minDistanceSq = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable == null)
                interactable = col.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                float distSq = (interactable.GetPosition() - transform.position).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closest = interactable;
                }
            }
        }

        currentInteractable = closest;
    }

    private void EnsureVacuumSuctionVFX()
    {
        if (vacuumSuctionVFX != null) return;

        Transform existing = transform.Find("VacuumSuctionVFX");
        if (existing != null)
        {
            vacuumSuctionVFX = existing.GetComponent<ParticleSystem>();
            if (vacuumSuctionVFX != null) return;
        }

        GameObject vfxObj = new GameObject("VacuumSuctionVFX");
        vfxObj.transform.SetParent(transform, false);
        // Vakum namlusunun biraz ilerisinde doğup geriye (oyuncuya doğru) çekilen hava akımı
        vfxObj.transform.localPosition = new Vector3(0.25f, 0.1f, 2.2f);
        vfxObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        vacuumSuctionVFX = vfxObj.AddComponent<ParticleSystem>();
        var main = vacuumSuctionVFX.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5.5f, 9.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.95f, 1.0f, 0.65f),
            new Color(0.4f, 1.0f, 0.65f, 0.45f)
        );
        main.maxParticles = 45;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = vacuumSuctionVFX.emission;
        emission.rateOverTime = 36f;

        var shape = vacuumSuctionVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.85f;

        var sizeOverLifetime = vacuumSuctionVFX.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1.0f);
        curve.AddKey(1f, 0.15f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.4f;
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (particleShader != null)
            {
                Material mat = new Material(particleShader);
                mat.color = new Color(0.55f, 0.95f, 1f, 0.55f);
                renderer.sharedMaterial = mat;
            }
        }

        vacuumSuctionVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateVacuumSuctionVFX()
    {
        if (vacuumSuctionVFX == null) EnsureVacuumSuctionVFX();
        if (vacuumSuctionVFX == null) return;

        bool canSuck = isVacuumActive && (PlayerInventory.Instance == null || !PlayerInventory.Instance.IsBackpackFull);
        if (canSuck)
        {
            if (!vacuumSuctionVFX.isPlaying)
                vacuumSuctionVFX.Play();
        }
        else
        {
            if (vacuumSuctionVFX.isPlaying)
                vacuumSuctionVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vakum menzili (Mavi)
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, vacuumRadius);

        // Etkileşim menzili (Sarı)
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
