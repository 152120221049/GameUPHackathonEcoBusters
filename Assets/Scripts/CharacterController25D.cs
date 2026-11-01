using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 2.5D İzometrik Karakter Kontrolcüsü.
/// Karakter X ve Z düzleminde (Sağ, Sol, İleri, Geri) hareket eder.
/// Kamera açısına göre (Isometric) hareket desteği ve pürüzsüz model dönüşü içerir.
/// Zıplama içermez; rampa ve zemin etkileşimi için yerçekimi korunur.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharacterController25D : MonoBehaviour
{
    [Header("--- HAREKET AYARLARI ---")]
    [Tooltip("Normal yürüme hızı")]
    [SerializeField] private float moveSpeed = 7f;
    [Tooltip("Koşma hızı çarpanı")]
    [SerializeField] private float sprintMultiplier = 1.5f;
    [Tooltip("Hızlanma ve yavaşlama yumuşatma faktörü")]
    [SerializeField] private float acceleration = 20f;

    [Header("--- İZOMETRİK VE KAMERA AÇISI ---")]
    [Tooltip("Aktifse hareket yönü izometrik kameranın baktığı açıya göre hesaplanır (W = ekranın yukarısı).")]
    [SerializeField] private bool moveRelativeToCamera = true;
    [Tooltip("Referans kamera (Boş bırakılırsa Main Camera otomatik kullanılır)")]
    [SerializeField] private Camera referenceCamera;

    [Header("--- DÖNÜŞ (ROTASYON) ---")]
    [Tooltip("Karakterin hareket ettiği yöne dönme hızı")]
    [SerializeField] private float rotationSpeed = 900f;

    [Header("--- ANİMASYON (OPSİYONEL) ---")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isMovingParam = "IsMoving";

    [Header("--- BASAMAK VE KOT FARKI (STEP OFFSET) ---")]
    [Tooltip("Karakterin takılmadan otomatik adım atıp tırmanabileceği maksimum basamak yüksekliği")]
    [SerializeField] private float stepHeight = 0.55f;
    [Tooltip("Basamağa tırmanma yumuşatma hızı")]
    [SerializeField] private float stepSmooth = 8f;
    [Tooltip("Basamak algılama ileri mesafesi")]
    [SerializeField] private float stepCheckDistance = 0.45f;

    // Dahili bileşenler ve durumlar
    private Rigidbody rb;
    private Vector2 inputVector;
    private Vector3 moveDirection;
    private bool isSprinting;
    private float progressionSpeedMultiplier = 1f;

    public bool IsMoving => inputVector.sqrMagnitude > 0.01f;
    public float CurrentSpeed => new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
    public Vector3 MoveDirection => moveDirection;
    public Rigidbody RigidbodyComponent => rb;

    public void SetSpeedMultiplier(float mult)
    {
        progressionSpeedMultiplier = Mathf.Max(0.5f, mult);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Karakterin fiziksel olarak devrilmesini ve dururken kendi etrafında dönmesini kalıcı olarak engelle.
        // Rotasyon sadece hareket yönüne göre kod ile kontrol edilir.
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Karakterin zemin kenarlarına veya köşelere takılmasını önleyen sürtünmesiz fizik materyali uygula
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            if (capsule.sharedMaterial == null)
            {
                var frictionless = new PhysicsMaterial("CharacterFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounciness = 0f,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                capsule.sharedMaterial = frictionless;
            }
        }

        if (referenceCamera == null)
        {
            referenceCamera = Camera.main;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Envanter, İlerleme ve Etkileşim/Vakum sistemlerini otomatik bağla
        if (GetComponent<PlayerInventory>() == null && PlayerInventory.Instance == null)
            gameObject.AddComponent<PlayerInventory>();

        if (GetComponent<PlayerInteractionController>() == null)
            gameObject.AddComponent<PlayerInteractionController>();

        if (GetComponent<PlayerProgression>() == null && PlayerProgression.Instance == null)
            gameObject.AddComponent<PlayerProgression>();

        if (GetComponent<BuildingPlacementSystem>() == null && BuildingPlacementSystem.Instance == null)
            gameObject.AddComponent<BuildingPlacementSystem>();

        if (GetComponent<VoxelCharacterAnimator>() == null)
            gameObject.AddComponent<VoxelCharacterAnimator>();
    }

    private void Update()
    {
        ProcessInput();
        CalculateMoveDirection();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        RotateTowardsMovement();
        HandleStepOffset();

        // Fizik motorunun çarpmalardan dönme torku üretmesini engelle (yerinde dururken dönme sorununu çözer)
        rb.angularVelocity = Vector3.zero;
    }

    public bool IsGrounded()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        float halfHeight = capsule != null ? capsule.height * 0.5f : 1.0f;
        float radius = capsule != null ? capsule.radius * 0.85f : 0.4f;
        Vector3 origin = transform.position - new Vector3(0f, halfHeight - radius, 0f);
        return Physics.CheckSphere(origin, radius + 0.12f, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Karakterin küçük yükseklik farklarını (0.5m basamaklar, rampa eşikleri) takılmadan
    /// pürüzsüzce tırmanmasını sağlayan Step-Offset sistemi.
    /// Düz zemin karoları arasındaki dikişlerde (seam) gereksiz zıplama/correction YAPMAZ.
    /// </summary>
    private void HandleStepOffset()
    {
        if (moveDirection.sqrMagnitude < 0.01f) return;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        float halfHeight = capsule != null ? capsule.height * 0.5f : 1.0f;
        float radius = capsule != null ? capsule.radius : 0.5f;

        // Ayak tabanı yüksekliği (Dünya koordinatında alt temas noktası)
        float currentFootY = transform.position.y - halfHeight;
        Vector3 footCenter = new Vector3(transform.position.x, currentFootY + 0.06f, transform.position.z);

        // Kapsülün dış yüzeyinden ileri doğru ray atarak kendi collider'ımıza takılmayı önle
        Vector3 rayOrigin = footCenter + moveDirection.normalized * (radius + 0.04f);
        Vector3 rayStepUpOrigin = rayOrigin + Vector3.up * stepHeight;

        // 1. Ayak hizasında önümüzde basamak engeli var mı?
        if (Physics.Raycast(rayOrigin, moveDirection, out RaycastHit lowerHit, stepCheckDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (lowerHit.collider.transform.root == transform.root ||
                lowerHit.collider.isTrigger ||
                lowerHit.collider.GetComponentInParent<CollectibleItem>() != null)
                return;

            // 2. Basamak yüksekliğinde önümüz açık mı?
            if (!Physics.Raycast(rayStepUpOrigin, moveDirection, stepCheckDistance + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Basamağın üst iniş yüzeyini tespit etmek için yukarıdan aşağı raycast
                Vector3 upperCheck = lowerHit.point + moveDirection.normalized * 0.15f + Vector3.up * (stepHeight + 0.1f);
                if (Physics.Raycast(upperCheck, Vector3.down, out RaycastHit surfaceHit, stepHeight + 0.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float targetFootY = surfaceHit.point.y;
                    float stepElevation = targetFootY - currentFootY;

                    // Düz zemin karoları (seam) arasındaki farklarda zıplamayı ve rb.position ışınlanma takılmasını önle!
                    // Yalnızca gerçek bir basamak (>= 0.08m) varsa pürüzsüz dikey hız desteği uygula (interpolation bozulmaz).
                    if (stepElevation >= 0.08f && stepElevation <= stepHeight)
                    {
                        Vector3 vel = rb.linearVelocity;
                        vel.y = Mathf.Max(vel.y, Mathf.Clamp(stepElevation * 9.5f, 1.5f, 4.5f));
                        rb.linearVelocity = vel;
                    }
                }
            }
        }
    }

    private void ProcessInput()
    {
        float x = 0f;
        float y = 0f;
        isSprinting = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;

            if (keyboard.leftShiftKey.isPressed) isSprinting = true;
        }

        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.magnitude > 0.19f)
            {
                x = stick.x;
                y = stick.y;
            }

            if (gamepad.dpad.up.isPressed) y = 1f;
            if (gamepad.dpad.down.isPressed) y = -1f;
            if (gamepad.dpad.right.isPressed) x = 1f;
            if (gamepad.dpad.left.isPressed) x = -1f;

            if (gamepad.rightTrigger.isPressed || gamepad.buttonEast.isPressed) isSprinting = true;
        }
#else
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        isSprinting = Input.GetKey(KeyCode.LeftShift);
#endif

        inputVector = new Vector2(x, y);
        if (inputVector.sqrMagnitude > 1f)
        {
            inputVector.Normalize();
        }
    }

    private void CalculateMoveDirection()
    {
        if (inputVector.sqrMagnitude < 0.001f)
        {
            moveDirection = Vector3.zero;
            return;
        }

        if (moveRelativeToCamera && referenceCamera != null)
        {
            // Kameranın Y rotasyonunu baz alarak girdiyi izometrik ekrana uyarla
            Vector3 camForward = referenceCamera.transform.forward;
            Vector3 camRight = referenceCamera.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // W: Ekranın yukarısına/kameranın baktığı yöne, D: Ekranın sağına
            moveDirection = (camForward * inputVector.y + camRight * inputVector.x).normalized;
        }
        else
        {
            // Dünya eksenine göre (W = +Z, D = +X)
            moveDirection = new Vector3(inputVector.x, 0f, inputVector.y).normalized;
        }
    }

    private void HandleMovement()
    {
        float currentTargetSpeed = moveSpeed * progressionSpeedMultiplier * (isSprinting ? sprintMultiplier : 1f);
        Vector3 targetVelocity = moveDirection * currentTargetSpeed;

        // Mevcut yatay hız (Y eksenini yerçekimi/rampa için koruyoruz)
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 velocityDiff = targetVelocity - currentHorizontalVel;

        // Yumuşak hızlanma ve yavaşlama kuvveti
        Vector3 force = velocityDiff * acceleration;
        rb.AddForce(force, ForceMode.Force);
    }

    private void RotateTowardsMovement()
    {
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            Quaternion newRot = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            transform.rotation = newRot;
            if (rb != null)
            {
                rb.MoveRotation(newRot);
            }
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float currentSpeedNorm = inputVector.magnitude * (isSprinting ? sprintMultiplier : 1f);

        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, currentSpeedNorm);

        if (!string.IsNullOrEmpty(isMovingParam))
            animator.SetBool(isMovingParam, IsMoving);
    }

    public void SetReferenceCamera(Camera cam)
    {
        referenceCamera = cam;
    }
}
