using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum BuildableStructureType
{
    Recycler2x2,
    ShippingBin1x1,
    TreeSapling1x1,
    ConveyorBelt1x1,
    AutoVacuumTurret1x1,
    SolarPanel1x1,
    WindTurbine1x1
}

[Serializable]
public struct BuildingRecipe
{
    public string displayName;
    public BuildableStructureType type;
    public Vector2Int footprint; // (2,2) veya (1,1)
    public int metalCost;
    public int plasticCost;
    public int organicCost;
    public int coinCost;
    public GameObject prefab;
}

/// <summary>
/// Eko-Mucit Yapı ve Otomasyon İnşa Sistemi (Stardew Valley + Factorio).
/// - [B] tuşuna basıldığında İnşa Modu açılır/kapanır.
/// - Mouse imleci hizasında ızgaraya oturan yeşil (uygun) / kırmızı (engelli) önizleme kutusu çıkar.
/// - [1..7] tuşlarıyla inşa edilecek yapı seçilir (1: Pres, 2: Sandık, 3: Konveyör, 4: Fidan, 5: Vakum Kulesi, 6: Güneş Paneli, 7: Rüzgar Türbini).
/// - [R] tuşuna basıldığında yapı 90 derece döner.
/// - Sol tıkla inşa edilir, malzemeler harcanır, karo işgal edilir (isOccupied = true).
/// </summary>
public class BuildingPlacementSystem : MonoBehaviour
{
    public static BuildingPlacementSystem Instance { get; private set; }

    [Header("--- İNŞA EDİLEBİLİR YAPILAR ---")]
    [SerializeField] private List<BuildingRecipe> recipes = new List<BuildingRecipe>();

    [Header("--- ÖNİZLEME (GHOST PREVIEW) ---")]
    [SerializeField] private Material validGhostMaterial;
    [SerializeField] private Material invalidGhostMaterial;

    private bool isBuildModeActive;
    private int selectedRecipeIndex = 0;
    private int currentRotationIndex = 0; // 0: 0°, 1: 90°, 2: 180°, 3: 270°
    private GameObject ghostPreviewObj;
    private Renderer ghostRenderer;
    private Camera mainCamera;
    private Vector2Int currentGridCoord;
    private bool isCurrentPositionValid;

    public bool IsBuildModeActive => isBuildModeActive;
    public int SelectedRecipeIndex => selectedRecipeIndex;
    public BuildingRecipe CurrentRecipe => (selectedRecipeIndex >= 0 && selectedRecipeIndex < recipes.Count) ? recipes[selectedRecipeIndex] : default;

    public event Action<bool> OnBuildModeToggled;
    public event Action<int> OnRecipeSelectionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        mainCamera = Camera.main;
        InitializeDefaultRecipesIfEmpty();
        CreateGhostPreviewObject();
    }

    private void InitializeDefaultRecipesIfEmpty()
    {
        recipes = new List<BuildingRecipe>
        {
            new BuildingRecipe
            {
                displayName = "Geri Dönüşüm Presi (2x2)",
                type = BuildableStructureType.Recycler2x2,
                footprint = new Vector2Int(2, 2),
                metalCost = 15,
                plasticCost = 9,
                organicCost = 0,
                coinCost = 20
            },
            new BuildingRecipe
            {
                displayName = "Besleme/Satış Sandığı (1x1)",
                type = BuildableStructureType.ShippingBin1x1,
                footprint = new Vector2Int(1, 1),
                metalCost = 12,
                plasticCost = 6,
                organicCost = 0,
                coinCost = 0
            },
            new BuildingRecipe
            {
                displayName = "Konveyör Bant (1x1)",
                type = BuildableStructureType.ConveyorBelt1x1,
                footprint = new Vector2Int(1, 1),
                metalCost = 3,
                plasticCost = 1,
                organicCost = 0,
                coinCost = 0
            },
            new BuildingRecipe
            {
                displayName = "Eko-Fidan / Ağaç (1x1)",
                type = BuildableStructureType.TreeSapling1x1,
                footprint = new Vector2Int(1, 1),
                metalCost = 0,
                plasticCost = 0,
                organicCost = 6,
                coinCost = 0
            },
            new BuildingRecipe
            {
                displayName = "Otomatik Vakum Kulesi (1x1)",
                type = BuildableStructureType.AutoVacuumTurret1x1,
                footprint = new Vector2Int(1, 1),
                metalCost = 12,
                plasticCost = 9,
                organicCost = 0,
                coinCost = 45
            },
            new BuildingRecipe
            {
                displayName = "Güneş Enerji Paneli (1x1)",
                type = BuildableStructureType.SolarPanel1x1,
                footprint = new Vector2Int(1, 1),
                metalCost = 12,
                plasticCost = 6,
                organicCost = 0,
                coinCost = 60
            },
            new BuildingRecipe
            {
                displayName = "Rüzgar Türbini (1x1)",
                type = BuildableStructureType.WindTurbine1x1,
                footprint = new Vector2Int(1, 1),
                metalCost = 18,
                plasticCost = 6,
                organicCost = 0,
                coinCost = 90
            }
        };
    }

    private void CreateGhostPreviewObject()
    {
        if (ghostPreviewObj != null) return;

        ghostPreviewObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghostPreviewObj.name = "GhostPreview";
        Destroy(ghostPreviewObj.GetComponent<Collider>());

        ghostRenderer = ghostPreviewObj.GetComponent<Renderer>();

        if (validGhostMaterial == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            validGhostMaterial = new Material(sh);
            validGhostMaterial.color = new Color(0.2f, 0.9f, 0.3f, 0.45f);
        }

        if (invalidGhostMaterial == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            invalidGhostMaterial = new Material(sh);
            invalidGhostMaterial.color = new Color(0.95f, 0.2f, 0.2f, 0.5f);
        }

        ghostPreviewObj.SetActive(false);
    }

    private void Update()
    {
        HandleInput();

        if (isBuildModeActive)
        {
            UpdateGhostPreviewPosition();
            HandlePlacementClick();
        }
    }

    private void HandleInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.bKey.wasPressedThisFrame)
        {
            ToggleBuildMode();
        }

        if (!isBuildModeActive) return;

        if (kb.digit1Key.wasPressedThisFrame) SelectRecipe(0);
        else if (kb.digit2Key.wasPressedThisFrame) SelectRecipe(1);
        else if (kb.digit3Key.wasPressedThisFrame) SelectRecipe(2);
        else if (kb.digit4Key.wasPressedThisFrame) SelectRecipe(3);
        else if (kb.digit5Key.wasPressedThisFrame) SelectRecipe(4);
        else if (kb.digit6Key.wasPressedThisFrame) SelectRecipe(5);
        else if (kb.digit7Key.wasPressedThisFrame) SelectRecipe(6);

        if (kb.rKey.wasPressedThisFrame)
        {
            RotateBuilding();
        }

        if (kb.escapeKey.wasPressedThisFrame)
        {
            SetBuildMode(false);
        }
#else
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBuildMode();
        }

        if (!isBuildModeActive) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRecipe(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectRecipe(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectRecipe(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectRecipe(3);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) SelectRecipe(4);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) SelectRecipe(5);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) SelectRecipe(6);

        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateBuilding();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetBuildMode(false);
        }
#endif
    }

    public void ToggleBuildMode()
    {
        SetBuildMode(!isBuildModeActive);
    }

    public void SetBuildMode(bool active)
    {
        isBuildModeActive = active;
        if (ghostPreviewObj != null)
        {
            ghostPreviewObj.SetActive(isBuildModeActive);
        }

        OnBuildModeToggled?.Invoke(isBuildModeActive);
        CameraFollow25D.Instance?.SetBuildModeZoom(isBuildModeActive);

        if (PlayerInventory.Instance != null)
        {
            string msg = isBuildModeActive
                ? $"İnşa Modu Açıldı: {CurrentRecipe.displayName} ([1-7] Seç, [R] Döndür)"
                : "İnşa Modu Kapatıldı.";
            PlayerInventory.Instance.Notify(msg);
        }
    }

    public void RotateBuilding()
    {
        currentRotationIndex = (currentRotationIndex + 1) % 4;
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Notify($"Yön: {currentRotationIndex * 90}°");
        }
    }

    public void SelectRecipe(int index)
    {
        if (index >= 0 && index < recipes.Count)
        {
            selectedRecipeIndex = index;
            OnRecipeSelectionChanged?.Invoke(selectedRecipeIndex);
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.Notify($"Seçilen Yapı: {CurrentRecipe.displayName}");
            }
        }
    }

    private void UpdateGhostPreviewPosition()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2 mousePos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) mousePos = Mouse.current.position.ReadValue();
#else
        mousePos = Input.mousePosition;
#endif

        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            BuildingRecipe recipe = CurrentRecipe;

            var terrain = ProceduralTerrainGenerator.Instance;
            float tSize = terrain != null ? terrain.TileSize : 2.0f;

            int snapX = Mathf.RoundToInt(hitPoint.x / tSize);
            int snapZ = Mathf.RoundToInt(hitPoint.z / tSize);
            currentGridCoord = new Vector2Int(snapX, snapZ);

            Vector3 previewCenter = new Vector3(
                (snapX + (recipe.footprint.x > 1 ? 0.5f : 0f)) * tSize,
                0.5f,
                (snapZ + (recipe.footprint.y > 1 ? 0.5f : 0f)) * tSize
            );

            Quaternion previewRot = Quaternion.Euler(0f, currentRotationIndex * 90f, 0f);

            ghostPreviewObj.transform.position = previewCenter;
            ghostPreviewObj.transform.rotation = previewRot;
            ghostPreviewObj.transform.localScale = new Vector3(recipe.footprint.x * tSize * 0.95f, 0.9f, recipe.footprint.y * tSize * 0.95f);

            isCurrentPositionValid = CanBuildAt(currentGridCoord, recipe);
            ghostRenderer.material = isCurrentPositionValid ? validGhostMaterial : invalidGhostMaterial;
        }
    }

    private bool CanBuildAt(Vector2Int baseCoord, BuildingRecipe recipe)
    {
        var terrain = ProceduralTerrainGenerator.Instance;
        if (terrain == null) return true;

        for (int x = 0; x < recipe.footprint.x; x++)
        {
            for (int y = 0; y < recipe.footprint.y; y++)
            {
                Vector2Int coord = new Vector2Int(baseCoord.x + x, baseCoord.y + y);
                GroundTile tile = terrain.GetTileAt(coord);

                if (tile == null || !tile.isBuildable || tile.isOccupied)
                {
                    return false;
                }
            }
        }

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv != null)
        {
            if (recipe.metalCost > 0 && !inv.HasItem(ItemType.ScrapMetal, recipe.metalCost)) return false;
            if (recipe.plasticCost > 0 && !inv.HasItem(ItemType.PlasticWaste, recipe.plasticCost)) return false;
            if (recipe.organicCost > 0 && !inv.HasItem(ItemType.OrganicWaste, recipe.organicCost)) return false;
            if (recipe.coinCost > 0 && inv.Coins < recipe.coinCost) return false;
        }

        return true;
    }

    private void HandlePlacementClick()
    {
        bool mouseClicked = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!overUI) mouseClicked = true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!overUI) mouseClicked = true;
        }
#endif

        if (mouseClicked)
        {
            if (isCurrentPositionValid)
            {
                BuildStructureAt(currentGridCoord, CurrentRecipe);
            }
            else if (GameAudioAndJuiceManager.Instance != null)
            {
                GameAudioAndJuiceManager.Instance.PlayErrorSound(ghostPreviewObj != null ? ghostPreviewObj.transform.position : (Vector3?)null, "BURAYA KURULAMAZ!");
            }
        }
    }

    private void BuildStructureAt(Vector2Int baseCoord, BuildingRecipe recipe)
    {
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv != null)
        {
            if (recipe.metalCost > 0) inv.TrySpendItem(ItemType.ScrapMetal, recipe.metalCost);
            if (recipe.plasticCost > 0) inv.TrySpendItem(ItemType.PlasticWaste, recipe.plasticCost);
            if (recipe.organicCost > 0) inv.TrySpendItem(ItemType.OrganicWaste, recipe.organicCost);
            if (recipe.coinCost > 0) inv.TrySpendCoins(recipe.coinCost);
        }

        var terrain = ProceduralTerrainGenerator.Instance;
        if (terrain != null)
        {
            for (int x = 0; x < recipe.footprint.x; x++)
            {
                for (int y = 0; y < recipe.footprint.y; y++)
                {
                    Vector2Int coord = new Vector2Int(baseCoord.x + x, baseCoord.y + y);
                    GroundTile tile = terrain.GetTileAt(coord);
                    if (tile != null)
                    {
                        tile.isOccupied = true;
                    }
                }
            }
        }

        float tSize = terrain != null ? terrain.TileSize : 2.0f;
        Vector3 spawnCenter = new Vector3(
            (baseCoord.x + (recipe.footprint.x > 1 ? 0.5f : 0f)) * tSize,
            0.05f,
            (baseCoord.y + (recipe.footprint.y > 1 ? 0.5f : 0f)) * tSize
        );
        Quaternion spawnRot = Quaternion.Euler(0f, currentRotationIndex * 90f, 0f);

        GameObject placedObj;
        if (recipe.prefab != null)
        {
            placedObj = Instantiate(recipe.prefab, spawnCenter, spawnRot);
            placedObj.transform.localScale = Vector3.one * (tSize / 1.0f);
        }
        else if (recipe.type == BuildableStructureType.AutoVacuumTurret1x1)
        {
            placedObj = new GameObject("Built_AutoVacuumTurret");
            placedObj.transform.position = spawnCenter;
            placedObj.transform.rotation = spawnRot;
            placedObj.transform.localScale = Vector3.one * (tSize / 1.0f);
            placedObj.AddComponent<AutoVacuumTurret>();
        }
        else if (recipe.type == BuildableStructureType.SolarPanel1x1 || recipe.type == BuildableStructureType.WindTurbine1x1)
        {
            placedObj = new GameObject($"Built_{recipe.displayName}");
            placedObj.transform.position = spawnCenter;
            placedObj.transform.rotation = spawnRot;
            placedObj.transform.localScale = Vector3.one * (tSize / 1.0f);
            var gen = placedObj.AddComponent<CleanEnergyStation>();
            gen.ConfigureStation(recipe.type == BuildableStructureType.SolarPanel1x1 ? CleanEnergyStation.EnergyType.Solar : CleanEnergyStation.EnergyType.Wind);
        }
        else
        {
            placedObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placedObj.transform.position = spawnCenter + Vector3.up * 0.45f;
            placedObj.transform.rotation = spawnRot;
            placedObj.transform.localScale = new Vector3(recipe.footprint.x * tSize * 0.95f, 0.9f, recipe.footprint.y * tSize * 0.95f);
            placedObj.name = $"Built_{recipe.displayName}";

            if (recipe.type == BuildableStructureType.Recycler2x2) placedObj.AddComponent<RecyclerMachine>();
            else if (recipe.type == BuildableStructureType.ShippingBin1x1) placedObj.AddComponent<ShippingBinMachine>();
            else if (recipe.type == BuildableStructureType.TreeSapling1x1) placedObj.AddComponent<TreePlantation>();
            else if (recipe.type == BuildableStructureType.ConveyorBelt1x1) placedObj.AddComponent<ConveyorBelt>();
        }

        if (inv != null)
        {
            inv.Notify($"İnşa Edildi: {recipe.displayName}!");
        }

        if (GameAudioAndJuiceManager.Instance != null && placedObj != null)
        {
            Color burstCol = recipe.type switch
            {
                BuildableStructureType.TreeSapling1x1 => new Color(0.3f, 0.95f, 0.45f),
                BuildableStructureType.SolarPanel1x1 => new Color(0.2f, 0.85f, 1f),
                BuildableStructureType.WindTurbine1x1 => new Color(0.85f, 0.95f, 1f),
                _ => new Color(0.4f, 0.88f, 1.0f)
            };
            GameAudioAndJuiceManager.Instance.TriggerBuildOrPlantOomph(spawnCenter, $"KURULDU: {recipe.displayName}!", burstCol, placedObj.transform);
        }
    }

    public void SetRecipePrefab(int index, GameObject prefab)
    {
        if (index >= 0 && index < recipes.Count)
        {
            BuildingRecipe r = recipes[index];
            r.prefab = prefab;
            recipes[index] = r;
        }
    }
}
