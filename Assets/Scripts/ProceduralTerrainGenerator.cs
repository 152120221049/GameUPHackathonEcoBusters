using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2.5D İzometrik Prosedürel Arazi Üreteci.
/// - 350x350+ geniş organik ada düzeni (Fabrika, Şehir, Tarım ve Ara Bağlantı Bölgeleri).
/// - Ara bölgeden geçen kıvrımlı, -1 kotunda nehir yatağı ve ana hat kesişimlerinde köprüler.
/// - Chunk Streaming ve Nesne Havuzlama (GameObject Pooling - SetActive) ile sıfır titreme/GC.
/// </summary>
public class ProceduralTerrainGenerator : MonoBehaviour
{
    public static ProceduralTerrainGenerator Instance { get; private set; }

    [Header("--- HARİTA BOYUTU VE ÖLÇEK ---")]
    [SerializeField] private bool useRandomMapSize = false;
    [SerializeField] private Vector2Int fixedSize = new Vector2Int(350, 350);

    [Header("--- SEED (RASTGELELİK TOHUMU) ---")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int customSeed = 1337;

    [Header("--- GRID VE TILE AYARLARI ---")]
    [SerializeField] private float tileSize = 2.0f;
    [SerializeField] private float coastlineNoiseScale = 0.018f;
    [SerializeField] private float coastlineNoiseStrength = 24f;
    [SerializeField] private bool useOrganicIslandShape = true;

    public float TileSize => tileSize;

    [Header("--- CHUNK STREAMING (PERFORMANS SİSTEMİ) ---")]
    [Tooltip("350x350 gibi devasa haritalarda FPS düşüşünü ve çökmeyi önleyen dinamik chunk sistemi")]
    [SerializeField] private bool enableChunkStreaming = true;
    [SerializeField] private int chunkSize = 20;
    [Tooltip("Oyuncu etrafında kaç chunk mesafenin aktif kalacağı (4 chunk = ~80x80 karo görüş açısı)")]
    [SerializeField] private int viewDistanceInChunks = 4;

    [Header("--- OYUNCU ÜSSÜ (ATÖLYE ALANI) ---")]
    [SerializeField] private int baseRadius = 5;
    [SerializeField] private bool autoSpawnPlayerAtBase = true;
    [SerializeField] private Transform playerTransform;

    [Header("--- TILE PREFAB SLOTLARI ---")]
    [SerializeField] private GameObject cityTilePrefab;
    [Tooltip("Sürülmüş tarla toprağı karosu (FarmTileTilledSOIL)")]
    [SerializeField] private GameObject farmTilePrefab;
    [Tooltip("Samanlı çiftlik zemini karosu (Hay Ground tILE)")]
    [SerializeField] private GameObject hayTilePrefab;
    [SerializeField] private GameObject factoryTilePrefab;
    [SerializeField] private GameObject transitionTilePrefab;
    [SerializeField] private GameObject playerBaseTilePrefab;
    [Tooltip("Harita dışı/sınır için hazırlanacak asset (Opsiyonel)")]
    [SerializeField] private GameObject boundaryTilePrefab;
    [Tooltip("Nehir su karosu prefabı (Opsiyonel)")]
    [SerializeField] private GameObject riverTilePrefab;
    [Tooltip("Nehir üstü ahşap köprü prefabı (Opsiyonel)")]
    [SerializeField] private GameObject bridgePrefab;
    [Tooltip("Nehir kıyısı köprü rampası (Bridge ramp.vox)")]
    [SerializeField] private GameObject bridgeRampPrefab;
    [Tooltip("Nehir üstü yükseltilmiş köprü tabliyesi (BridgeTop.vox)")]
    [SerializeField] private GameObject bridgeTopPrefab;

    [Header("--- YOL VE RAY PREFABLARI ---")]
    [SerializeField] private GameObject roadMiddlePrefab;
    [SerializeField] private GameObject roadSidesPrefab;
    [SerializeField] private GameObject roadBasePrefab;
    [SerializeField] private GameObject roadRailsPrefab;
    [SerializeField] private GameObject gravelRoadPrefab;

    [Header("--- BİNA VE ÇEVRE DEKORASYON PREFABLARI ---")]
    [Tooltip("Sadece Şehir Bölgesine yerleştirilecek evler/binalar")]
    [SerializeField] private List<GameObject> cityBuildingPrefabs = new List<GameObject>();
    [Tooltip("Sadece Fabrika Bölgesine yerleştirilecek sanayi binaları/depolar")]
    [SerializeField] private List<GameObject> factoryBuildingPrefabs = new List<GameObject>();
    [Tooltip("Sadece Tarım/Taşra Bölgesine yerleştirilecek voxel çiftlik yapıları (XeehSets)")]
    [SerializeField] private List<GameObject> farmStructurePrefabs = new List<GameObject>();
    [Tooltip("Çiftlik ahır çatısı (farm-20-barn_roof)")]
    [SerializeField] private GameObject barnRoofPrefab;
    [Tooltip("Çiftlik yel değirmeni çatısı (farm-0-windmill_roof)")]
    [SerializeField] private GameObject windmillRoofPrefab;
    [Tooltip("Çiftlik yel değirmeni pervanesi (farm-1-windmill_blade)")]
    [SerializeField] private GameObject windmillBladePrefab;
    [Tooltip("Saman zeminli çiftlik avlusunda doğacak hayvanlar ve avlu eşyaları")]
    [SerializeField] private List<GameObject> farmAnimalPrefabs = new List<GameObject>();
    [Tooltip("Sürülmüş tarla zemininde (FarmTileTilledSOIL) doğacak ekin/mahsul dekorasyonları")]
    [SerializeField] private List<GameObject> farmCropPrefabs = new List<GameObject>();
    [Tooltip("Çiftlik binaları ve yapıları için boyut çarpanı")]
    [SerializeField] private float farmStructureScaleMultiplier = 2.4f;
    [Tooltip("Çiftlik ekinleri ve hayvanlar için boyut çarpanı")]
    [SerializeField] private float farmCropScaleMultiplier = 1.6f;
    [SerializeField] private GameObject largeBuilding1Prefab;
    [SerializeField] private GameObject largeBuilding2Prefab;
    [Tooltip("Şehir çöp konteyneri (Voxel Trash Can T2)")]
    [SerializeField] private GameObject trashCanPrefab;
    [Tooltip("Şehir vatandaş NPC prefabları (Kadın, Erkek, Çocuk)")]
    [SerializeField] private List<GameObject> cityCitizenNPCPrefabs = new List<GameObject>();
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private float buildingScaleMultiplier = 4.5f;

    [Header("--- NEHİR VE KOT FARKI AYARLARI ---")]
    [Tooltip("Aktifse ara bölgelerden kıvrımlı, -1 kotunda nehir yatağı geçer")]
    [SerializeField] private bool generateSunkenRiver = true;
    [Tooltip("Nehrin derinliği (Unity birimi)")]
    [SerializeField] private float riverDepth = 1.0f;

    [Header("--- ATIK / TOPLANABİLİR & ATÖLYE PREFABLARI ---")]
    [Tooltip("Hücre başına toplanabilir atık doğma ihtimali")]
    [Range(0f, 0.2f)]
    [SerializeField] private float resourceSpawnChance = 0.045f;
    [SerializeField] private List<GameObject> scrapMetalPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> plasticWastePrefabs = new List<GameObject>();
    [SerializeField] private GameObject scrapMetalPrefab;
    [SerializeField] private GameObject plasticWastePrefab;
    [SerializeField] private GameObject organicWastePrefab;
    [Tooltip("Lastik Atık 3B Modeli (Tire.vox)")]
    [SerializeField] private GameObject tireWastePrefab;
    [Tooltip("Elektronik Atık 3B Modeli (Electronicwaste.vox)")]
    [SerializeField] private GameObject electronicWastePrefab;
    [Tooltip("Güneş Paneli 3B Modeli (SolarPanel.vox)")]
    [SerializeField] private GameObject solarPanelPrefab;
    [Tooltip("Rüzgar Türbini 3B Modeli (WindTurbine.vox)")]
    [SerializeField] private GameObject windTurbinePrefab;
    [SerializeField] private GameObject workbenchPrefab;

    [Header("--- BÖLGESEL EKO-İSTASYON VE YENİ MODEL PREFABLARI ---")]
    [SerializeField] private GameObject chimneyPrefab;
    [SerializeField] private GameObject compostStationPrefab;
    [SerializeField] private GameObject riverBuoyPrefab;
    [SerializeField] private GameObject cityHubPrefab;
    [Tooltip("Baca Filtresi 3B Modeli (AirFilter.vox)")]
    [SerializeField] private GameObject airFilterVoxPrefab;
    [Tooltip("Nehir Temizleme Dubası 3B Modeli (Buoy.vox)")]
    [SerializeField] private GameObject buoyVoxPrefab;
    [Tooltip("Fabrika Bacası 3B Modeli (FactoryChimney.vox)")]
    [SerializeField] private GameObject factoryChimneyVoxPrefab;
    [Tooltip("Fabrika Ana Geri Dönüşüm Tesisi (RecyclingBuilding.vox)")]
    [SerializeField] private GameObject recyclingBuildingVoxPrefab;
    [Tooltip("Şehir Belediye Binası (TownHall.vox)")]
    [SerializeField] private GameObject townHallVoxPrefab;
    [Tooltip("Çiftlik Arızalı Traktör Modelleri (farm-60-tractor, farm-59-tractor2)")]
    [SerializeField] private List<GameObject> farmTractorPrefabs = new List<GameObject>();
    [Tooltip("4 Büyüme Aşamalı Mısır (growth1..4)")]
    [SerializeField] private GameObject[] cornStagePrefabs = new GameObject[4];
    [Tooltip("4 Büyüme Aşamalı Domates (growth1..4)")]
    [SerializeField] private GameObject[] tomatoStagePrefabs = new GameObject[4];
    [Tooltip("4 Büyüme Aşamalı Havuç (growth1..4)")]
    [SerializeField] private GameObject[] carrotStagePrefabs = new GameObject[4];
    [Tooltip("4 Büyüme Aşamalı Pancar (growth1..4)")]
    [SerializeField] private GameObject[] beetStagePrefabs = new GameObject[4];

    [Header("--- TEST FALLBACK AYARI ---")]
    [SerializeField] private bool useFallbackPrimitivesIfEmpty = true;

    // Pusula (Compass) için Bölge Merkezlerinin Dünya Koordinatları
    public Vector3 BaseWorldPosition { get; private set; }
    public Vector3 CityCenterWorldPosition { get; private set; }
    public Vector3 FactoryHubWorldPosition { get; private set; }
    public Vector3 FarmCenterWorldPosition { get; private set; }
    public Vector3 RiverCenterWorldPosition { get; private set; }
    public int MapWidth => currentWidth > 0 ? currentWidth : fixedSize.x;
    public int MapHeight => currentHeight > 0 ? currentHeight : fixedSize.y;

    private readonly List<Vector3> riverCenterWorldPoints = new List<Vector3>();

    // Dahili durumlar
    private int currentWidth;
    private int currentHeight;
    private int currentSeed;
    private Transform mapParent;
    private GroundType[,] gridData;
    private GroundType[,] baseDistrictData;
    private Vector2Int playerBaseCenter;
    private readonly HashSet<Vector2Int> collectedResourceCoords = new HashSet<Vector2Int>();

    private struct DistrictNode
    {
        public Vector2 position;
        public GroundType type;
    }
    private readonly List<DistrictNode> districtNodes = new List<DistrictNode>();

    private class MapChunk
    {
        public Vector2Int chunkCoord;
        public GameObject chunkObject;
        public bool isLoaded;
        public readonly Dictionary<Vector2Int, GroundTile> tiles = new Dictionary<Vector2Int, GroundTile>();
    }

    private readonly Dictionary<Vector2Int, MapChunk> chunks = new Dictionary<Vector2Int, MapChunk>();
    private Vector2Int lastPlayerChunkCoord = new Vector2Int(int.MinValue, int.MinValue);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(GenerateMapCoroutine());
    }

    private void Update()
    {
        if (enableChunkStreaming && playerTransform != null)
        {
            UpdateChunkStreaming();
        }
    }

    [ContextMenu("Haritayı Yeniden Üret (Generate)")]
    public void GenerateMap()
    {
        if (Application.isPlaying)
        {
            StopAllCoroutines();
            StartCoroutine(GenerateMapCoroutine());
        }
        else
        {
            GenerateMapDirect();
        }
    }

    private IEnumerator GenerateMapCoroutine()
    {
        var loading = SceneLoadingScreen.ShowScreen();
        if (loading != null) loading.SetProgress(0.08f, "Ekosistem Hazırlanıyor: Vadi ve Ada Şekillendiriliyor...");
        yield return null;

        ClearMap();
        EnsureEditorAssetsLoaded();

        if (useRandomSeed)
        {
            currentSeed = unchecked((int)DateTime.UtcNow.Ticks) ^ Guid.NewGuid().GetHashCode();
        }
        else
        {
            currentSeed = customSeed;
        }

        if (fixedSize.x <= 0 || fixedSize.y <= 0)
        {
            fixedSize = new Vector2Int(350, 350);
        }

        currentWidth = fixedSize.x;
        currentHeight = fixedSize.y;

        if (currentWidth >= 120 && !enableChunkStreaming)
        {
            Debug.LogWarning("[Eko-Mucit] 350x350 harita için Chunk Streaming otomatik olarak aktif edildi.");
            enableChunkStreaming = true;
        }

        gridData = new GroundType[currentWidth, currentHeight];
        baseDistrictData = new GroundType[currentWidth, currentHeight];

        // 1. Ana 3 Bölgeyi Merkezlendir
        SetupConnectedDistricts();

        // 2. Oyuncu Üssünü Belirle
        DeterminePlayerBasePosition();

        // 3. Grid Hücrelerini Hesapla (Organik Ada Formu + Bütünleşik District'ler)
        float noiseOffsetX = UnityEngine.Random.Range(-10000f, 10000f);
        float noiseOffsetY = UnityEngine.Random.Range(-10000f, 10000f);

        Vector2 mapCenter = new Vector2(currentWidth * 0.5f, currentHeight * 0.5f);
        float baseIslandRadius = Mathf.Min(currentWidth, currentHeight) * 0.44f;

        for (int x = 0; x < currentWidth; x++)
        {
            for (int y = 0; y < currentHeight; y++)
            {
                if (useOrganicIslandShape)
                {
                    float distToCenter = Vector2.Distance(new Vector2(x, y), mapCenter);
                    float coastNoise = (Mathf.PerlinNoise((x + noiseOffsetX) * coastlineNoiseScale, (y + noiseOffsetY) * coastlineNoiseScale) - 0.5f) * coastlineNoiseStrength;
                    
                    if (distToCenter + coastNoise > baseIslandRadius)
                    {
                        gridData[x, y] = GroundType.Empty;
                        baseDistrictData[x, y] = GroundType.Empty;
                        continue;
                    }
                }

                GroundType evaluated = EvaluateDistrictCell(x, y, noiseOffsetX, noiseOffsetY);
                gridData[x, y] = evaluated;
                baseDistrictData[x, y] = evaluated;
            }
        }

        // 4. Oyuncu Üssünü Güvenli Şekilde Oy
        CarvePlayerBase();

        if (loading != null) loading.SetProgress(0.35f, "Nehir Yatağı Oyuluyor ve Köprüler Kuruluyor...");
        yield return null;

        // 5. Ara Bölgelerden -1 Kotunda Nehir Yatağı Oy ve Kesişimlere Köprü Yerleştir
        CarveSunkenRiverAndBridges(noiseOffsetX, noiseOffsetY);

        // 6. Bölgeler Arasında Yol Ağını Oluştur (Road Network)
        GenerateRoadNetwork();
        BuildBridgesAtRoadCrossings();

        if (loading != null) loading.SetProgress(0.60f, "Bölgeler ve Altyapı Hazırlanıyor...");
        yield return null;

        // 7. Chunk'ları Kur
        SetupChunkContainers();

        // 8. Oyuncuyu Üsse Konumlandır
        if (autoSpawnPlayerAtBase)
        {
            PositionPlayerAtBase();
        }

        Vector3 mapOriginOffset = new Vector3(-currentWidth * 0.5f * tileSize, 0, -currentHeight * 0.5f * tileSize);

        if (loading != null) loading.SetProgress(0.78f, "Binalar, Çiftlik Avlusu ve Ağaçlar Yerleştiriliyor...");
        yield return null;

        // 9. Şehir ve Fabrika Bölgelerine Büyük Binaları ve Ağaçları Yerleştir
        SpawnDistrictDecorations(mapOriginOffset);

        // 10. Nehir Saydam Su Yüzeyi Akıntı Efektlerini ve Dünya Sınırı Sis Kuşağını Kur
        var waterAndFog = GetComponent<RiverWaterAndFogSystem>();
        if (waterAndFog == null) waterAndFog = gameObject.AddComponent<RiverWaterAndFogSystem>();
        waterAndFog.InitializeRiverAndBorderFog(mapParent, baseIslandRadius * tileSize, riverCenterWorldPoints);

        if (loading != null) loading.SetProgress(0.92f, "Dünya Çevreleri ve Atık Kümeleri Yükleniyor...");
        yield return null;

        // 11. Görünür Chunk'ları Yükle
        if (enableChunkStreaming)
        {
            Vector2Int startChunk = WorldToChunkCoord(playerTransform != null ? playerTransform.position : Vector3.zero);
            lastPlayerChunkCoord = startChunk;
            LoadVisibleChunks(startChunk);
        }
        else
        {
            foreach (var chunk in chunks.Values)
            {
                LoadChunk(chunk);
            }
        }

        if (loading != null)
        {
            loading.SetProgress(1.0f, "Ekosistem Hazır! Giriş Yapılıyor...");
            yield return new WaitForSeconds(0.2f);
            loading.DismissScreen(0.75f);
        }
    }

    private void GenerateMapDirect()
    {
        ClearMap();
        EnsureEditorAssetsLoaded();

        if (useRandomSeed)
        {
            currentSeed = unchecked((int)DateTime.UtcNow.Ticks) ^ Guid.NewGuid().GetHashCode();
        }
        else
        {
            currentSeed = customSeed;
        }

        if (fixedSize.x <= 0 || fixedSize.y <= 0)
        {
            fixedSize = new Vector2Int(350, 350);
        }

        currentWidth = fixedSize.x;
        currentHeight = fixedSize.y;

        if (currentWidth >= 120 && !enableChunkStreaming)
        {
            enableChunkStreaming = true;
        }

        gridData = new GroundType[currentWidth, currentHeight];
        baseDistrictData = new GroundType[currentWidth, currentHeight];

        SetupConnectedDistricts();
        DeterminePlayerBasePosition();

        float noiseOffsetX = UnityEngine.Random.Range(-10000f, 10000f);
        float noiseOffsetY = UnityEngine.Random.Range(-10000f, 10000f);

        Vector2 mapCenter = new Vector2(currentWidth * 0.5f, currentHeight * 0.5f);
        float baseIslandRadius = Mathf.Min(currentWidth, currentHeight) * 0.44f;

        for (int x = 0; x < currentWidth; x++)
        {
            for (int y = 0; y < currentHeight; y++)
            {
                if (useOrganicIslandShape)
                {
                    float distToCenter = Vector2.Distance(new Vector2(x, y), mapCenter);
                    float coastNoise = (Mathf.PerlinNoise((x + noiseOffsetX) * coastlineNoiseScale, (y + noiseOffsetY) * coastlineNoiseScale) - 0.5f) * coastlineNoiseStrength;
                    if (distToCenter + coastNoise > baseIslandRadius)
                    {
                        gridData[x, y] = GroundType.Empty;
                        baseDistrictData[x, y] = GroundType.Empty;
                        continue;
                    }
                }

                GroundType evaluated = EvaluateDistrictCell(x, y, noiseOffsetX, noiseOffsetY);
                gridData[x, y] = evaluated;
                baseDistrictData[x, y] = evaluated;
            }
        }

        CarvePlayerBase();
        CarveSunkenRiverAndBridges(noiseOffsetX, noiseOffsetY);
        GenerateRoadNetwork();
        BuildBridgesAtRoadCrossings();
        SetupChunkContainers();

        if (autoSpawnPlayerAtBase)
        {
            PositionPlayerAtBase();
        }

        Vector3 mapOriginOffset = new Vector3(-currentWidth * 0.5f * tileSize, 0, -currentHeight * 0.5f * tileSize);
        SpawnDistrictDecorations(mapOriginOffset);

        var waterAndFog = GetComponent<RiverWaterAndFogSystem>();
        if (waterAndFog == null) waterAndFog = gameObject.AddComponent<RiverWaterAndFogSystem>();
        waterAndFog.InitializeRiverAndBorderFog(mapParent, baseIslandRadius * tileSize, riverCenterWorldPoints);

        if (enableChunkStreaming)
        {
            Vector2Int startChunk = WorldToChunkCoord(playerTransform != null ? playerTransform.position : Vector3.zero);
            lastPlayerChunkCoord = startChunk;
            LoadVisibleChunks(startChunk);
        }
        else
        {
            foreach (var chunk in chunks.Values)
            {
                LoadChunk(chunk);
            }
        }
    }

    private void EnsureEditorAssetsLoaded()
    {
#if UNITY_EDITOR
        GameObject LoadVox(string exactName)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{exactName} t:GameObject");
            foreach (var g in guids)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".vox", System.StringComparison.OrdinalIgnoreCase) &&
                    System.IO.Path.GetFileNameWithoutExtension(p).Equals(exactName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
                }
            }
            return null;
        }

        if (airFilterVoxPrefab == null) airFilterVoxPrefab = LoadVox("AirFilter");
        if (buoyVoxPrefab == null) buoyVoxPrefab = LoadVox("Buoy");
        if (factoryChimneyVoxPrefab == null) factoryChimneyVoxPrefab = LoadVox("FactoryChimney");
        if (recyclingBuildingVoxPrefab == null) recyclingBuildingVoxPrefab = LoadVox("RecyclingBuilding");
        if (townHallVoxPrefab == null) townHallVoxPrefab = LoadVox("TownHall");
        if (bridgeRampPrefab == null) bridgeRampPrefab = LoadVox("Bridge ramp");
        if (bridgeTopPrefab == null) bridgeTopPrefab = LoadVox("BridgeTop");
        if (tireWastePrefab == null) tireWastePrefab = LoadVox("Tire");
        if (electronicWastePrefab == null) electronicWastePrefab = LoadVox("Electronicwaste");
        if (solarPanelPrefab == null) solarPanelPrefab = LoadVox("SolarPanel");
        if (windTurbinePrefab == null) windTurbinePrefab = LoadVox("WindTurbine");

        if (farmStructurePrefabs == null || farmStructurePrefabs.Count == 0)
        {
            farmStructurePrefabs = new List<GameObject>();
            string[] sNames = { "farm-21-barn", "farm-2-windmill", "farm-49-coop", "farm-60-tractor", "farm-59-tractor2" };
            foreach (var n in sNames)
            {
                var go = LoadVox(n);
                if (go != null) farmStructurePrefabs.Add(go);
            }
        }

        if (farmTractorPrefabs == null || farmTractorPrefabs.Count == 0)
        {
            farmTractorPrefabs = new List<GameObject>();
            var t1 = LoadVox("farm-60-tractor");
            var t2 = LoadVox("farm-59-tractor2");
            if (t1 != null) farmTractorPrefabs.Add(t1);
            if (t2 != null) farmTractorPrefabs.Add(t2);
        }

        cornStagePrefabs = new GameObject[] {
            LoadVox("farm-51-corn_growth1"),
            LoadVox("farm-52-corn_growth2"),
            LoadVox("farm-30-corn_growth3"),
            LoadVox("farm-32-corn_growth4")
        };
        tomatoStagePrefabs = new GameObject[] {
            LoadVox("farm-58-tomato_growth1"),
            LoadVox("farm-57-tomato_growth2"),
            LoadVox("farm-56-tomato_growth3"),
            LoadVox("farm-55-tomato_growth4")
        };
        carrotStagePrefabs = new GameObject[] {
            LoadVox("farm-37-carrot_growth1"),
            LoadVox("farm-38-carrot_growth2"),
            LoadVox("farm-39-carrot_growth3"),
            LoadVox("farm-40-carrot_growth4")
        };
        beetStagePrefabs = new GameObject[] {
            LoadVox("farm-11-beet_growth1"),
            LoadVox("farm-12-beet_growth2"),
            LoadVox("farm-13-beet_growth3"),
            LoadVox("farm-14-beet_growth4")
        };

        if (cityBuildingPrefabs == null || cityBuildingPrefabs.Count < 4)
        {
            cityBuildingPrefabs = new List<GameObject>();
            string[] names = { "House1", "House2", "SmallHouse", "SmallHouse2", "SmallHouse3", "TownHall", "LargeBuilding", "LargeBuilding2" };
            foreach (var n in names)
            {
                var go = LoadVox(n);
                if (go != null) cityBuildingPrefabs.Add(go);
            }
        }

        if (factoryBuildingPrefabs == null || factoryBuildingPrefabs.Count < 2)
        {
            factoryBuildingPrefabs = new List<GameObject>();
            string[] names = { "FactoryBaseBuilding", "RecyclingBuilding", "LargeBuilding2" };
            foreach (var n in names)
            {
                var go = LoadVox(n);
                if (go != null) factoryBuildingPrefabs.Add(go);
            }
        }

        if (trashCanPrefab == null)
        {
            trashCanPrefab = LoadVox("Voxel Trash Can T2");
        }

        if (barnRoofPrefab == null) barnRoofPrefab = LoadVox("farm-20-barn_roof");
        if (windmillRoofPrefab == null) windmillRoofPrefab = LoadVox("farm-0-windmill_roof");
        if (windmillBladePrefab == null) windmillBladePrefab = LoadVox("farm-1-windmill_blade");

        if (farmAnimalPrefabs == null || farmAnimalPrefabs.Count == 0)
        {
            farmAnimalPrefabs = new List<GameObject>();
            string[] animalNames = { "farm-19-cow_br", "farm-23-cow_bw", "farm-25-sheep_white", "farm-24-sheep_black", "farm-45-chicken_yellow", "farm-46-chicken_white", "farm-16-feeder", "farm-47-watertrough_S", "farm-43-fence_mid" };
            foreach (var n in animalNames)
            {
                var go = LoadVox(n);
                if (go != null) farmAnimalPrefabs.Add(go);
            }
        }

        if (farmCropPrefabs == null || farmCropPrefabs.Count == 0)
        {
            farmCropPrefabs = new List<GameObject>();
            string[] cropNames = { "farm-32-corn_growth4", "farm-40-carrot_growth4", "farm-55-tomato_growth4", "farm-14-beet_growth4", "farm-33-cauliflower_growth4", "farm-42-turnip_growth4" };
            foreach (var n in cropNames)
            {
                var go = LoadVox(n);
                if (go != null) farmCropPrefabs.Add(go);
            }
        }

        if (cityCitizenNPCPrefabs == null || cityCitizenNPCPrefabs.Count == 0)
        {
            cityCitizenNPCPrefabs = new List<GameObject>();
            string[] npcPaths = {
                "Assets/Prefabs/NPC_Female_Follower.prefab",
                "Assets/Prefabs/NPC_Male_Follower.prefab",
                "Assets/Prefabs/NPC_Child_Citizen.prefab"
            };
            foreach (var np in npcPaths)
            {
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(np);
                if (go != null) cityCitizenNPCPrefabs.Add(go);
            }
            if (cityCitizenNPCPrefabs.Count < 3)
            {
                var childVox = LoadVox("Child");
                if (childVox != null && !cityCitizenNPCPrefabs.Contains(childVox))
                    cityCitizenNPCPrefabs.Add(childVox);
            }
        }
#endif
    }

    [ContextMenu("Haritayı Temizle (Clear)")]
    public void ClearMap()
    {
        chunks.Clear();
        districtNodes.Clear();
        collectedResourceCoords.Clear();

        if (mapParent != null)
        {
            if (Application.isPlaying)
                Destroy(mapParent.gameObject);
            else
                DestroyImmediate(mapParent.gameObject);
        }
    }

    private void SetupConnectedDistricts()
    {
        districtNodes.Clear();
        Vector2 center = new Vector2(currentWidth * 0.5f, currentHeight * 0.5f);
        float radius = Mathf.Min(currentWidth, currentHeight) * 0.28f;

        float angleOffset = (currentSeed % 360) * Mathf.Deg2Rad;

        districtNodes.Add(new DistrictNode
        {
            position = center + new Vector2(Mathf.Cos(angleOffset), Mathf.Sin(angleOffset)) * radius,
            type = GroundType.City
        });

        float factoryAngle = angleOffset + (2f * Mathf.PI / 3f);
        districtNodes.Add(new DistrictNode
        {
            position = center + new Vector2(Mathf.Cos(factoryAngle), Mathf.Sin(factoryAngle)) * radius,
            type = GroundType.Factory
        });

        float farmAngle = angleOffset + (4f * Mathf.PI / 3f);
        districtNodes.Add(new DistrictNode
        {
            position = center + new Vector2(Mathf.Cos(farmAngle), Mathf.Sin(farmAngle)) * radius,
            type = GroundType.Farmland
        });
    }

    private void DeterminePlayerBasePosition()
    {
        // Üs konumunu; Şehir bölgesinin en dış kenarında, hiçbir binanın oluşmadığı açık bir alanda
        // ve diğer bölgelere (Fabrika / Çiftlik sınırına) olabildiğince yakın bir noktada oluştur!
        Vector2 cityPos = districtNodes[0].position;
        Vector2 mapCenter = new Vector2(currentWidth * 0.5f, currentHeight * 0.5f);

        // Şehir merkezinden %75 oranında diğer bölgelerin kesişim sınırına doğru ilerle
        Vector2 basePos = Vector2.Lerp(cityPos, mapCenter, 0.75f);
        playerBaseCenter = new Vector2Int(Mathf.RoundToInt(basePos.x), Mathf.RoundToInt(basePos.y));
    }

    private GroundType EvaluateDistrictCell(int x, int y, float noiseX, float noiseY)
    {
        Vector2 cellPos = new Vector2(x, y);

        float d0 = Vector2.Distance(cellPos, districtNodes[0].position);
        float d1 = Vector2.Distance(cellPos, districtNodes[1].position);
        float d2 = Vector2.Distance(cellPos, districtNodes[2].position);

        float n = (Mathf.PerlinNoise((x + noiseX) * 0.035f, (y + noiseY) * 0.035f) - 0.5f) * 14f;
        d0 += n;
        d1 += n;
        d2 += n;

        float min1 = Mathf.Min(d0, Mathf.Min(d1, d2));
        float min2;
        GroundType closestType;

        if (min1 == d0)
        {
            closestType = districtNodes[0].type;
            min2 = Mathf.Min(d1, d2);
        }
        else if (min1 == d1)
        {
            closestType = districtNodes[1].type;
            min2 = Mathf.Min(d0, d2);
        }
        else
        {
            closestType = districtNodes[2].type;
            min2 = Mathf.Min(d0, d1);
        }

        float diff = min2 - min1;
        float transitionBand = 6.5f;

        if (diff < transitionBand)
        {
            return GroundType.Transition;
        }

        return closestType;
    }

    private void CarvePlayerBase()
    {
        for (int x = playerBaseCenter.x - baseRadius; x <= playerBaseCenter.x + baseRadius; x++)
        {
            for (int y = playerBaseCenter.y - baseRadius; y <= playerBaseCenter.y + baseRadius; y++)
            {
                if (x >= 0 && x < currentWidth && y >= 0 && y < currentHeight)
                {
                    if (Vector2Int.Distance(new Vector2Int(x, y), playerBaseCenter) <= baseRadius)
                    {
                        gridData[x, y] = GroundType.PlayerBase;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adanın ortasındaki vadi boyunca kıvrılarak akan kesintisiz, etkileyici bir Nehir Yatağı (-1 kotunda) oyar,
    /// nehir merkez noktalarını su yüzeyi/akıntı efektleri için kaydeder ve yaya/araç köprülerini yerleştirir.
    /// </summary>
    private void CarveSunkenRiverAndBridges(float noiseOffsetX, float noiseOffsetY)
    {
        riverCenterWorldPoints.Clear();
        if (!generateSunkenRiver) return;

        Vector3 originOffset = new Vector3(-currentWidth * 0.5f * tileSize, 0, -currentHeight * 0.5f * tileSize);
        List<Vector2Int> centerlineCells = new List<Vector2Int>();

        // Adanın batısından doğusuna doğru kıvrımlı (Sinüs + Perlin) kesintisiz ana nehir kanalı oluştur
        int riverHalfWidth = 2; // 4-5 karo (8-10 metre) genişliğinde ferah nehir yatağı
        float baseCenterY = currentHeight * 0.5f;

        for (int x = 14; x < currentWidth - 14; x++)
        {
            float normX = (float)x / currentWidth;
            float wave1 = Mathf.Sin(normX * Mathf.PI * 2.5f + (currentSeed % 17)) * (currentHeight * 0.14f);
            float wave2 = (Mathf.PerlinNoise((x + noiseOffsetX) * 0.025f, noiseOffsetY * 0.025f) - 0.5f) * (currentHeight * 0.12f);
            int centerY = Mathf.RoundToInt(baseCenterY + wave1 + wave2);

            // Oyuncu üssünün çok yakınından geçiyorsa üssün etrafından yumuşakça kavis al
            float distToBase = Vector2.Distance(new Vector2(x, centerY), playerBaseCenter);
            float minSafeDist = baseRadius + 10f;
            if (distToBase < minSafeDist)
            {
                float pushDir = (centerY >= playerBaseCenter.y) ? 1f : -1f;
                centerY = playerBaseCenter.y + Mathf.RoundToInt(pushDir * minSafeDist);
            }

            centerY = Mathf.Clamp(centerY, 16, currentHeight - 16);
            if (gridData[x, centerY] == GroundType.Empty) continue;

            centerlineCells.Add(new Vector2Int(x, centerY));
            riverCenterWorldPoints.Add(GridToWorldPosition(x, centerY, originOffset));

            for (int wy = -riverHalfWidth; wy <= riverHalfWidth; wy++)
            {
                int ry = centerY + wy;
                if (ry <= 4 || ry >= currentHeight - 4) continue;
                if (gridData[x, ry] == GroundType.Empty || gridData[x, ry] == GroundType.PlayerBase) continue;
                if (Vector2Int.Distance(new Vector2Int(x, ry), playerBaseCenter) <= baseRadius + 6f) continue;

                gridData[x, ry] = GroundType.River;
            }
        }

        if (riverCenterWorldPoints.Count > 0)
        {
            RiverCenterWorldPosition = riverCenterWorldPoints[riverCenterWorldPoints.Count / 2];
        }
    }

    private void SetBridgeTileAt(int x, int y, GroundType type)
    {
        if (x >= 0 && x < currentWidth && y >= 0 && y < currentHeight)
        {
            if (gridData[x, y] != GroundType.Empty && gridData[x, y] != GroundType.PlayerBase)
            {
                gridData[x, y] = type;
            }
        }
    }

    /// <summary>
    /// Bölgeler (Şehir, Fabrika, Tarım) ve Oyuncu Üssü arasında organik ve düzenli ana yol arterleri açar.
    /// Yol genişliği 3 hücredir: Sol Kenar (RoadSides), Orta (RoadMiddle), Sağ Kenar (RoadSides).
    /// </summary>
    private void GenerateRoadNetwork()
    {
        if (districtNodes.Count < 2) return;

        DistrictNode cityNode = districtNodes.Find(n => n.type == GroundType.City);
        DistrictNode factoryNode = districtNodes.Find(n => n.type == GroundType.Factory);
        DistrictNode farmNode = districtNodes.Find(n => n.type == GroundType.Farmland);

        // 1. Ana Arterler: Üs <-> Şehir, Şehir <-> Fabrika, Fabrika <-> Tarla, Tarla <-> Üs (Halka Arter)
        if (cityNode.type == GroundType.City)
        {
            CarveRoadBetween(playerBaseCenter, Vector2Int.RoundToInt(cityNode.position));
        }

        if (cityNode.type == GroundType.City && factoryNode.type == GroundType.Factory)
        {
            CarveRoadBetween(Vector2Int.RoundToInt(cityNode.position), Vector2Int.RoundToInt(factoryNode.position));
        }

        if (factoryNode.type == GroundType.Factory && farmNode.type == GroundType.Farmland)
        {
            CarveRoadBetween(Vector2Int.RoundToInt(factoryNode.position), Vector2Int.RoundToInt(farmNode.position));
        }

        if (farmNode.type == GroundType.Farmland)
        {
            CarveRoadBetween(Vector2Int.RoundToInt(farmNode.position), playerBaseCenter);
        }

        // 2. Tüm Şehir Bölgesine Yayılan Sokak Grid Ağı (Grid-Iron Blocks)
        // Sadece dar bir merkezde değil, adadaki TÜM GroundType.City karolarına düzenli cadde ve sokak ağı yayar.
        // Oyuncu üssünün etrafındaki açık alanı (baseRadius + 7) korur.
        if (cityNode.type == GroundType.City)
        {
            Vector2Int cCenter = Vector2Int.RoundToInt(cityNode.position);
            int streetInterval = 18;
            int offsetX = ((cCenter.x % streetInterval) + streetInterval) % streetInterval;
            int offsetY = ((cCenter.y % streetInterval) + streetInterval) % streetInterval;

            // Doğu-Batı sokakları (Tüm harita boyunca GroundType.City olan her yerde)
            for (int y = 4; y < currentHeight - 4; y++)
            {
                if ((y % streetInterval) != offsetY) continue;

                for (int x = 4; x < currentWidth - 4; x++)
                {
                    if (baseDistrictData[x, y] == GroundType.City &&
                        Vector2Int.Distance(new Vector2Int(x, y), playerBaseCenter) > baseRadius + 7f)
                    {
                        CarveRoadStamp(x, y);
                    }
                }
            }

            // Kuzey-Güney caddeleri (Tüm harita boyunca GroundType.City olan her yerde)
            for (int x = 4; x < currentWidth - 4; x++)
            {
                if ((x % streetInterval) != offsetX) continue;

                for (int y = 4; y < currentHeight - 4; y++)
                {
                    if (baseDistrictData[x, y] == GroundType.City &&
                        Vector2Int.Distance(new Vector2Int(x, y), playerBaseCenter) > baseRadius + 7f)
                    {
                        CarveRoadStamp(x, y);
                    }
                }
            }
        }

        // 3. Fabrika Bölgesi İçi Sanayi Yolu Kolları (Fabrika binalarının da tüm bölgeye yayılması için)
        if (factoryNode.type == GroundType.Factory)
        {
            Vector2Int fCenter = Vector2Int.RoundToInt(factoryNode.position);
            int fInterval = 18;
            int fOffX = ((fCenter.x % fInterval) + fInterval) % fInterval;
            int fOffY = ((fCenter.y % fInterval) + fInterval) % fInterval;

            for (int y = 4; y < currentHeight - 4; y++)
            {
                if ((y % fInterval) != fOffY) continue;
                for (int x = 4; x < currentWidth - 4; x++)
                {
                    if (baseDistrictData[x, y] == GroundType.Factory &&
                        Vector2Int.Distance(new Vector2Int(x, y), playerBaseCenter) > baseRadius + 7f)
                    {
                        CarveRoadStamp(x, y);
                    }
                }
            }

            for (int x = 4; x < currentWidth - 4; x++)
            {
                if ((x % fInterval) != fOffX) continue;
                for (int y = 4; y < currentHeight - 4; y++)
                {
                    if (baseDistrictData[x, y] == GroundType.Factory &&
                        Vector2Int.Distance(new Vector2Int(x, y), playerBaseCenter) > baseRadius + 7f)
                    {
                        CarveRoadStamp(x, y);
                    }
                }
            }
        }
    }

    private void CarveRoadBetween(Vector2Int start, Vector2Int end)
    {
        // Önce X ekseninde dümdüz git, sonra Y ekseninde dümdüz git (Tam L-şeklinde, asla çapraz adım atmaz)
        int curX = start.x;
        int curY = start.y;

        int stepX = (int)Mathf.Sign(end.x - curX);
        if (stepX != 0)
        {
            while (curX != end.x)
            {
                CarveRoadStamp(curX, curY);
                curX += stepX;
            }
        }

        int stepY = (int)Mathf.Sign(end.y - curY);
        if (stepY != 0)
        {
            while (curY != end.y)
            {
                CarveRoadStamp(curX, curY);
                curY += stepY;
            }
        }

        CarveRoadStamp(end.x, end.y);
    }

    private void CarveRoadStamp(int cx, int cy)
    {
        // 5 karo (10 metre) genişliğinde ferah cadde damarı aç (cx, cy merkez: -2 .. +2)
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oy = -2; oy <= 2; oy++)
            {
                int rx = cx + ox;
                int ry = cy + oy;

                if (rx >= 0 && rx < currentWidth && ry >= 0 && ry < currentHeight)
                {
                    // Nehir yatağını asla yol veya rastgele köprü ile ezme; köprüler sadece BuildBridgesAtRoadCrossings ile temiz kurulur
                    if (gridData[rx, ry] == GroundType.River)
                    {
                        continue;
                    }

                    // Boşluk, üs ve köprüyü bozma; diğer karaları yola dönüştür
                    if (gridData[rx, ry] != GroundType.Empty &&
                        gridData[rx, ry] != GroundType.Bridge &&
                        gridData[rx, ry] != GroundType.BridgeRamp &&
                        gridData[rx, ry] != GroundType.PlayerBase)
                    {
                        gridData[rx, ry] = GroundType.Road;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Yol ağının nehir ile kesiştiği gerçek noktalarda nehri dikine kesen düzgün, temiz köprüler ve rampalar inşa eder.
    /// Nehir boyunca rastgele veya yamuk köprü parçası oluşmasını engeller.
    /// </summary>
    private void BuildBridgesAtRoadCrossings()
    {
        // Yol karosu olup nehre komşu olan ve karşı kıyısında da yol olan koridorları bul
        for (int x = 2; x < currentWidth - 2; x++)
        {
            for (int y = 2; y < currentHeight - 2; y++)
            {
                // Düşey geçiş (Kuzey - Güney yönünde nehir geçişi):
                // Bu hücre Yol ve bir üstü Nehir ise, nehrin karşı kıyısına kadar tara
                if (gridData[x, y] == GroundType.Road && gridData[x, y + 1] == GroundType.River)
                {
                    int span = 0;
                    while (y + 1 + span < currentHeight - 2 && gridData[x, y + 1 + span] == GroundType.River)
                    {
                        span++;
                    }

                    int northLandY = y + 1 + span;
                    // Karşı kıyıda da kara/yol varsa burası gerçek bir köprü geçiş noktasıdır
                    if (span >= 1 && span <= 8 && northLandY < currentHeight - 2 && gridData[x, northLandY] != GroundType.Empty)
                    {
                        // Kıyı karoları normal Yol olarak kalır (rampa olmadan düz geçiş)
                        // Nehir üstü boyunca düz köprü tabliyesi yerleştirilir
                        for (int k = 1; k <= span; k++)
                        {
                            gridData[x, y + k] = GroundType.Bridge;
                        }
                    }
                }
            }
        }
    }

    private void SetupChunkContainers()
    {
        GameObject rootObj = new GameObject("MapRoot");
        rootObj.transform.SetParent(transform);
        mapParent = rootObj.transform;

        int numChunksX = Mathf.CeilToInt((float)currentWidth / chunkSize);
        int numChunksY = Mathf.CeilToInt((float)currentHeight / chunkSize);

        for (int cx = 0; cx < numChunksX; cx++)
        {
            for (int cy = 0; cy < numChunksY; cy++)
            {
                Vector2Int coord = new Vector2Int(cx, cy);
                chunks[coord] = new MapChunk
                {
                    chunkCoord = coord,
                    chunkObject = null,
                    isLoaded = false
                };
            }
        }
    }

    private void UpdateChunkStreaming()
    {
        Vector2Int currentChunkCoord = WorldToChunkCoord(playerTransform.position);
        if (currentChunkCoord != lastPlayerChunkCoord)
        {
            lastPlayerChunkCoord = currentChunkCoord;
            LoadVisibleChunks(currentChunkCoord);
        }
    }

    private void LoadVisibleChunks(Vector2Int centerChunk)
    {
        HashSet<Vector2Int> visibleCoords = new HashSet<Vector2Int>();

        for (int x = -viewDistanceInChunks; x <= viewDistanceInChunks; x++)
        {
            for (int y = -viewDistanceInChunks; y <= viewDistanceInChunks; y++)
            {
                Vector2Int coord = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                if (chunks.ContainsKey(coord))
                {
                    visibleCoords.Add(coord);
                    LoadChunk(chunks[coord]);
                }
            }
        }

        foreach (var pair in chunks)
        {
            if (pair.Value.isLoaded && !visibleCoords.Contains(pair.Key))
            {
                UnloadChunk(pair.Value);
            }
        }
    }

    private void LoadChunk(MapChunk chunk)
    {
        if (chunk.isLoaded) return;

        // Daha önce oluşturulmuşsa sadece aktif et (Silip yeniden yükleme yapılmaz, parlamayı önler!)
        if (chunk.chunkObject != null)
        {
            chunk.chunkObject.SetActive(true);
            chunk.isLoaded = true;
            return;
        }

        GameObject chunkObj = new GameObject($"Chunk_{chunk.chunkCoord.x}_{chunk.chunkCoord.y}");
        chunkObj.transform.SetParent(mapParent);
        chunk.chunkObject = chunkObj;

        Vector3 originOffset = new Vector3(-currentWidth * 0.5f * tileSize, 0, -currentHeight * 0.5f * tileSize);

        int startX = chunk.chunkCoord.x * chunkSize;
        int startY = chunk.chunkCoord.y * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, currentWidth);
        int endY = Mathf.Min(startY + chunkSize, currentHeight);

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                GroundType type = gridData[x, y];
                if (type == GroundType.Empty) continue;

                Vector3 worldPos = GridToWorldPosition(x, y, originOffset);

                // Nehir -1 kotunda, Köprü ve Kara 0 kotunda yer alır
                float elevation = 0f;
                if (type == GroundType.River)
                {
                    elevation = -riverDepth;
                    worldPos.y = -riverDepth;
                }

                Vector2Int cellCoord = new Vector2Int(x, y);

                GameObject tileObj = InstantiateTile(type, cellCoord, worldPos);
                if (tileObj == null) continue;

                tileObj.transform.SetParent(chunkObj.transform);

                GroundTile tileComp = tileObj.GetComponent<GroundTile>();
                if (tileComp == null) tileComp = tileObj.AddComponent<GroundTile>();

                bool isBuildable = (type == GroundType.PlayerBase || type == GroundType.Farmland || type == GroundType.Transition);
                tileComp.Initialize(type, cellCoord, isBuildable, elevation);

                chunk.tiles[cellCoord] = tileComp;

                // Toplanabilir atık (Sadece Fabrika, Şehir ve Bozkırda; Tarlada, Nehirde ve Köprüde çöp doğmaz!)
                TrySpawnCollectibleOnTile(type, cellCoord, worldPos, tileComp, chunkObj.transform);
            }
        }

        // Karolar arası iç kenar (internal edge) takılmalarını %100 yok etmek için birleştirilmiş pürüzsüz zemin collider'ı ve nehir su yüzeyi oluştur
        BuildSeamlessChunkFloorAndWater(chunkObj, startX, endX, startY, endY, originOffset);

        chunk.isLoaded = true;
    }

    private void BuildSeamlessChunkFloorAndWater(GameObject chunkObj, int startX, int endX, int startY, int endY, Vector3 originOffset)
    {
        bool hasRiverOrVoid = false;
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                GroundType gt = gridData[x, y];
                if (gt == GroundType.River || gt == GroundType.Empty)
                {
                    hasRiverOrVoid = true;
                    break;
                }
            }
            if (hasRiverOrVoid) break;
        }

        float thickness = 0.4f;

        // Eğer chunk içinde nehir veya boşluk yoksa, tüm 20x20 chunk için TEK BİR pürüzsüz BoxCollider kullan! (Sıfır dikiş, sıfır takılma!)
        if (!hasRiverOrVoid)
        {
            int countX = endX - startX;
            int countY = endY - startY;
            Vector3 minPos = GridToWorldPosition(startX, startY, originOffset);
            Vector3 maxPos = GridToWorldPosition(endX - 1, endY - 1, originOffset);
            Vector3 center = (minPos + maxPos) * 0.5f;

            BoxCollider chunkBox = chunkObj.AddComponent<BoxCollider>();
            chunkBox.center = new Vector3(center.x, -thickness * 0.5f, center.z);
            chunkBox.size = new Vector3(countX * tileSize, thickness, countY * tileSize);
            return;
        }

        // Nehir veya kıyı içeren chunk'larda her Y satırı boyunca bitişik kara ve nehir şeritlerini birleştir
        Material waterMat = RiverWaterAndFogSystem.Instance != null ? RiverWaterAndFogSystem.Instance.SharedWaterMaterial : null;

        for (int y = startY; y < endY; y++)
        {
            int x = startX;
            while (x < endX)
            {
                GroundType gt = gridData[x, y];
                if (gt == GroundType.Empty)
                {
                    x++;
                    continue;
                }

                bool isRiverRun = (gt == GroundType.River);
                int runStart = x;
                while (x < endX && gridData[x, y] != GroundType.Empty && ((gridData[x, y] == GroundType.River) == isRiverRun))
                {
                    x++;
                }
                int runEnd = x - 1;
                int runLen = runEnd - runStart + 1;

                Vector3 pStart = GridToWorldPosition(runStart, y, originOffset);
                Vector3 pEnd = GridToWorldPosition(runEnd, y, originOffset);
                Vector3 stripCenter = (pStart + pEnd) * 0.5f;
                float topY = isRiverRun ? -riverDepth : 0f;

                BoxCollider stripCol = chunkObj.AddComponent<BoxCollider>();
                stripCol.center = new Vector3(stripCenter.x, topY - thickness * 0.5f, stripCenter.z);
                stripCol.size = new Vector3(runLen * tileSize, thickness, tileSize);

                // Nehir şeridiyse üzerine yarı-saydam (transparent) su yüzeyi plakası yerleştir
                if (isRiverRun)
                {
                    GameObject waterSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    waterSlab.name = $"RiverWaterSurface_{runStart}_{y}";
                    waterSlab.transform.SetParent(chunkObj.transform, false);
                    waterSlab.transform.position = new Vector3(stripCenter.x, -0.22f, stripCenter.z);
                    waterSlab.transform.localScale = new Vector3(runLen * tileSize, 0.08f, tileSize);

                    Collider wCol = waterSlab.GetComponent<Collider>();
                    if (wCol != null)
                    {
                        wCol.enabled = false;
                        Destroy(wCol);
                    }

                    Renderer wRend = waterSlab.GetComponent<Renderer>();
                    if (wRend != null)
                    {
                        if (waterMat != null)
                        {
                            wRend.sharedMaterial = waterMat;
                        }
                        else
                        {
                            SetRendererColorSafe(wRend, new Color(0.12f, 0.58f, 0.88f, 0.82f));
                        }
                    }
                }
            }
        }
    }

    private void TrySpawnCollectibleOnTile(GroundType groundType, Vector2Int coord, Vector3 worldPos, GroundTile tileComp, Transform chunkParent)
    {
        if (groundType == GroundType.Empty || groundType == GroundType.Farmland || groundType == GroundType.River || groundType == GroundType.Bridge || groundType == GroundType.BridgeRamp) return;
        if (collectedResourceCoords.Contains(coord)) return;

        // Oyuncunun tam doğduğu merkez ve atölye üstüne çöp koyma
        float distToBase = Vector2Int.Distance(coord, playerBaseCenter);
        if (distToBase < 3.5f) return;

        int hash = coord.x * 73856093 ^ coord.y * 19349663 ^ currentSeed;
        float rand01 = Mathf.Abs(hash % 10000) / 10000f;

        // Organik çöp öbekleri (Clustering): Perlin noise ile belirgin büyük çöp yığınları oluştur
        // Rastgele minik serpiştirmeler yerine temiz sokaklar ve belirgin doyurucu yığınlar (8 ila 14 parça)
        float clusterNoise = Mathf.PerlinNoise((coord.x + (currentSeed % 100) * 0.3f) * 0.12f, (coord.y + (currentSeed % 100) * 0.3f) * 0.12f);
        bool isClusterCenter = (clusterNoise > 0.65f) && ((coord.x % 4 == 0) && (coord.y % 4 == 0));

        if (!isClusterCenter) return;

        // Doyurucu büyük çöp yığını: 8 ila 14 adet parçalı organik yığın
        int spawnCount = 8 + Mathf.Abs(hash % 7);

        for (int i = 0; i < spawnCount; i++)
        {
            int subHash = hash ^ (i * 99991);
            int typeHash = Mathf.Abs((subHash / 97) % 100);

            // 5 Atık Türü: Metal Hurda, Plastik, Lastik (Tire), Elektronik Atık (Electronicwaste), Biyo-Atık
            ItemType itemToSpawn;
            if (groundType == GroundType.Factory)
            {
                if (typeHash < 35) itemToSpawn = ItemType.ScrapMetal;
                else if (typeHash < 65) itemToSpawn = ItemType.ElectronicWaste;
                else if (typeHash < 85) itemToSpawn = ItemType.RubberTire;
                else itemToSpawn = ItemType.PlasticWaste;
            }
            else if (groundType == GroundType.City)
            {
                if (typeHash < 40) itemToSpawn = ItemType.PlasticWaste;
                else if (typeHash < 65) itemToSpawn = ItemType.RubberTire;
                else if (typeHash < 85) itemToSpawn = ItemType.ElectronicWaste;
                else itemToSpawn = ItemType.ScrapMetal;
            }
            else // Transition / Bozkır
            {
                if (typeHash < 35) itemToSpawn = ItemType.PlasticWaste;
                else if (typeHash < 60) itemToSpawn = ItemType.RubberTire;
                else if (typeHash < 80) itemToSpawn = ItemType.OrganicWaste;
                else itemToSpawn = ItemType.ScrapMetal;
            }

            GameObject prefab = itemToSpawn switch
            {
                ItemType.ScrapMetal => (scrapMetalPrefabs != null && scrapMetalPrefabs.Count > 0) ? scrapMetalPrefabs[Mathf.Abs(subHash % scrapMetalPrefabs.Count)] : scrapMetalPrefab,
                ItemType.PlasticWaste => (plasticWastePrefabs != null && plasticWastePrefabs.Count > 0) ? plasticWastePrefabs[Mathf.Abs(subHash % plasticWastePrefabs.Count)] : plasticWastePrefab,
                ItemType.RubberTire => tireWastePrefab ?? scrapMetalPrefab,
                ItemType.ElectronicWaste => electronicWastePrefab ?? scrapMetalPrefab,
                ItemType.OrganicWaste => organicWastePrefab ?? plasticWastePrefab,
                _ => scrapMetalPrefab
            };

            // Öbek içindeki doğal radyal yayılma (merkezde sıkışık, kenarlarda yayvan)
            float angleDeg = (i * (360f / Mathf.Max(1, spawnCount))) + ((subHash % 50) - 25f);
            float distOffset = 0.20f + (Mathf.Abs(subHash % 85) / 100f) * 1.15f;
            Vector3 radialOffset = new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad) * distOffset, 0f, Mathf.Sin(angleDeg * Mathf.Deg2Rad) * distOffset);

            float tiltX = ((subHash / 7) % 19) - 9f;
            float tiltZ = ((subHash / 17) % 19) - 9f;
            Quaternion randomRot = Quaternion.Euler(tiltX, (subHash % 360), tiltZ);

            float spawnYOffset = 0.18f + (Mathf.Abs((subHash / 11) % 20) / 100f);
            Vector3 spawnPos = worldPos + radialOffset + new Vector3(0f, spawnYOffset, 0f);
            GameObject itemObj;

            float scaleVariation = 0.88f + (Mathf.Abs((subHash / 13) % 35) / 100f);
            int itemAmount = (scaleVariation > 1.15f) ? 2 : 1;

            if (prefab != null)
            {
                itemObj = Instantiate(prefab, spawnPos, randomRot);
                itemObj.transform.localScale = itemObj.transform.localScale * scaleVariation;
            }
            else if (useFallbackPrimitivesIfEmpty)
            {
                itemObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                itemObj.transform.position = spawnPos;
                itemObj.transform.rotation = randomRot;
                itemObj.transform.localScale = new Vector3(tileSize * 0.35f * scaleVariation, tileSize * 0.25f * scaleVariation, tileSize * 0.35f * scaleVariation);
                itemObj.name = $"Collectible_{itemToSpawn}";

                var rend = itemObj.GetComponent<Renderer>();
                SetRendererColorSafe(rend, ItemDatabase.GetFallbackColor(itemToSpawn));
            }
            else
            {
                continue;
            }

            itemObj.transform.SetParent(chunkParent);

            foreach (var mc in itemObj.GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc != null)
                {
                    mc.enabled = false;
                    Destroy(mc);
                }
            }

            CollectibleItem collectible = itemObj.GetComponent<CollectibleItem>();
            if (collectible == null) collectible = itemObj.AddComponent<CollectibleItem>();

            collectible.Initialize(itemToSpawn, itemAmount, tileComp, () =>
            {
                collectedResourceCoords.Add(coord);
            });
        }
    }

    private void UnloadChunk(MapChunk chunk)
    {
        if (!chunk.isLoaded) return;

        // Sadece nesneyi gizle (Caching/Pooling)
        if (chunk.chunkObject != null)
        {
            chunk.chunkObject.SetActive(false);
        }
        chunk.isLoaded = false;
    }

    private GameObject InstantiateTile(GroundType type, Vector2Int coord, Vector3 worldPos)
    {
        Quaternion rotation = Quaternion.identity;
        GameObject prefab = null;

        if (type == GroundType.Road)
        {
            // Yol için komşu analizi yaparak yönü ve doğru prefabı belirle
            prefab = DetermineRoadTilePrefabAndRotation(coord, out rotation);
            if (prefab == null) prefab = roadBasePrefab ?? gravelRoadPrefab;
        }
        else if (type == GroundType.Bridge)
        {
            prefab = bridgeTopPrefab ?? bridgePrefab;
            rotation = DetermineBridgeTopRotation(coord);
            worldPos.y = 0f; // Rampa kaldırıldığı için köprü yolu kara/yol seviyesinde (0 kotu) pürüzsüz bağlanır
        }
        else if (type == GroundType.BridgeRamp)
        {
            prefab = bridgeTopPrefab ?? bridgePrefab;
            rotation = DetermineBridgeTopRotation(coord);
            worldPos.y = 0f;
        }
        else if (type == GroundType.Farmland)
        {
            int rotHash = Mathf.Abs((coord.x * 73856093 ^ coord.y * 19349663 ^ currentSeed) % 4);

            if (IsFarmCourtyardZone(coord.x, coord.y))
            {
                // Çiftlik yapı avlusu: Saman karosu (Hay Ground tILE)
                prefab = hayTilePrefab ?? transitionTilePrefab;
                rotation = Quaternion.Euler(0f, rotHash * 90f, 0f);
            }
            else if (IsFarmTilledPlotZone(coord.x, coord.y))
            {
                // Sürülmüş tarla toprağı: FarmTileTilledSOIL. KESİNLİKLE DÖNDÜRÜLMEZ (Rotation = 0)!
                prefab = farmTilePrefab ?? transitionTilePrefab;
                rotation = Quaternion.identity;
            }
            else
            {
                // Çiftliğin geri kalanı açık yeşil çim (GrassBaseTile) ile kaplanır
                prefab = transitionTilePrefab;
                rotation = Quaternion.Euler(0f, rotHash * 90f, 0f);
            }
        }
        else
        {
            prefab = GetPrefabForType(type);

            // Çim, Şehir ve Fabrika zemin karolarına 0, 90, 180, 270 derece deterministik rastgele dönüş ver
            if (type == GroundType.City || type == GroundType.Factory || type == GroundType.Transition)
            {
                int rotHash = Mathf.Abs((coord.x * 73856093 ^ coord.y * 19349663 ^ currentSeed) % 4);
                rotation = Quaternion.Euler(0f, rotHash * 90f, 0f);
            }
        }

        if (prefab != null)
        {
            GameObject tile = Instantiate(prefab, worldPos, rotation);
            tile.transform.localScale = Vector3.one * (tileSize / 1.0f);

            // Zemin karolarındaki iç içe geçen / pürüzlü MeshCollider'ları anında temizle
            foreach (var col in tile.GetComponentsInChildren<Collider>(true))
            {
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }
            }

            // Tile mesh'inin LOKAL uzaydaki en üst yüzeyini (localTopY) hesapla
            float localTopY = 0f;
            MeshFilter mf = tile.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                float childLocalY = (mf.transform != tile.transform) ? mf.transform.localPosition.y : 0f;
                localTopY = childLocalY + mf.sharedMesh.bounds.max.y;
            }

            float worldTopShift = localTopY * tile.transform.localScale.y;
            if (type != GroundType.Bridge && type != GroundType.BridgeRamp)
            {
                tile.transform.position -= new Vector3(0f, worldTopShift, 0f);
            }
            else if (type == GroundType.Bridge || type == GroundType.BridgeRamp)
            {
                tile.transform.position = new Vector3(worldPos.x, 0f, worldPos.z);

                // Köprü tabliyesi için pürüzsüz düz yürüyüş collider'ı (kara seviyesinde)
                BoxCollider deckCol = tile.AddComponent<BoxCollider>();
                deckCol.center = new Vector3(0f, 0.05f, 0f);
                deckCol.size = new Vector3(tileSize * 0.99f, 0.2f, tileSize * 0.99f);

                // Nehir boşluğuna bakan dış kenarlara koruyucu korkuluk BoxCollider ekle
                if (IsRiverOrEmpty(coord.x - 1, coord.y))
                {
                    BoxCollider railL = tile.AddComponent<BoxCollider>();
                    railL.center = new Vector3(-tileSize * 0.45f, 0.45f, 0f);
                    railL.size = new Vector3(0.2f, 0.8f, tileSize);
                }
                if (IsRiverOrEmpty(coord.x + 1, coord.y))
                {
                    BoxCollider railR = tile.AddComponent<BoxCollider>();
                    railR.center = new Vector3(tileSize * 0.45f, 0.45f, 0f);
                    railR.size = new Vector3(0.2f, 0.8f, tileSize);
                }
            }

            return tile;
        }

        if (useFallbackPrimitivesIfEmpty)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = worldPos;
            cube.transform.rotation = rotation;
            cube.transform.localScale = new Vector3(tileSize * 0.98f, 0.2f, tileSize * 0.98f);
            cube.name = $"{type}_Tile";

            Collider cCol = cube.GetComponent<Collider>();
            if (cCol != null)
            {
                cCol.enabled = false;
                Destroy(cCol);
            }

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = type switch
                {
                    GroundType.Road => new Color(0.25f, 0.25f, 0.28f),
                    GroundType.City => new Color(0.42f, 0.44f, 0.48f),
                    GroundType.Factory => new Color(0.32f, 0.22f, 0.18f),
                    GroundType.Farmland => new Color(0.38f, 0.62f, 0.28f),
                    GroundType.Transition => new Color(0.68f, 0.62f, 0.42f),
                    GroundType.PlayerBase => new Color(0.2f, 0.78f, 0.75f),
                    GroundType.River => new Color(0.18f, 0.55f, 0.88f, 0.8f),
                    GroundType.Bridge => new Color(0.48f, 0.32f, 0.18f),
                    _ => Color.white
                };
                SetRendererColorSafe(renderer, color);
            }
            return cube;
        }

        GameObject emptyObj = new GameObject($"{type}_Tile");
        emptyObj.transform.position = worldPos;
        return emptyObj;
    }

    private bool IsRiverOrEmpty(int x, int y)
    {
        if (x < 0 || x >= currentWidth || y < 0 || y >= currentHeight) return true;
        return gridData[x, y] == GroundType.River || gridData[x, y] == GroundType.Empty;
    }

    private bool IsBridgeAt(int x, int y)
    {
        if (x < 0 || x >= currentWidth || y < 0 || y >= currentHeight) return false;
        return gridData[x, y] == GroundType.Bridge;
    }

    private Quaternion DetermineBridgeTopRotation(Vector2Int coord)
    {
        // Nehir esasen X ekseni boyunca akıp Y ekseninde (Kuzey-Güney) genişler.
        // Köprüler nehri dik keser (Kuzey-Güney doğrultusunda uzanır).
        // Modelin tahta/korkuluk doğrultusunu yolun yönüne göre hizala:
        bool riverLeft = IsRiverOrEmpty(coord.x - 1, coord.y);
        bool riverRight = IsRiverOrEmpty(coord.x + 1, coord.y);

        // Yol Kuzey-Güney uzanıyorsa 90 derece döndürerek köprü tabliyesini yola bağla
        if (riverLeft || riverRight)
        {
            return Quaternion.Euler(0f, 90f, 0f);
        }

        bool riverUp = IsRiverOrEmpty(coord.x, coord.y + 1);
        bool riverDown = IsRiverOrEmpty(coord.x, coord.y - 1);
        if (riverUp || riverDown)
        {
            return Quaternion.identity;
        }

        return Quaternion.Euler(0f, 90f, 0f);
    }

    private Quaternion DetermineBridgeRampRotation(Vector2Int coord)
    {
        // Bridge ramp kıyıdan köprüye (0 kotundan +1 kotuna) tırmanır.
        // Eğer rampanın yönü ters bağlanmışsa 180 derece tersine çevrilerek köprüye tam oturması sağlanır.
        if (IsBridgeAt(coord.x, coord.y + 1)) return Quaternion.Euler(0f, 180f, 0f); // Köprü Kuzeyde (+Z), rampa köprüye doğru yükselir
        if (IsBridgeAt(coord.x, coord.y - 1)) return Quaternion.identity;             // Köprü Güneyde (-Z), rampa köprüye doğru yükselir
        if (IsBridgeAt(coord.x + 1, coord.y)) return Quaternion.Euler(0f, 270f, 0f); // Köprü Doğuda (+X), rampa köprüye doğru yükselir
        if (IsBridgeAt(coord.x - 1, coord.y)) return Quaternion.Euler(0f, 90f, 0f);  // Köprü Batıda (-X), rampa köprüye doğru yükselir

        return Quaternion.Euler(0f, 90f, 0f);
    }

    private void AddSmoothRampColliders(GameObject tile, Vector2Int coord)
    {
        // 4 basamaklı mikro-adım BoxCollider dizisi (0'dan 1.0f kotuna takılmasız yürüyüş)
        for (int s = 0; s < 4; s++)
        {
            BoxCollider step = tile.AddComponent<BoxCollider>();
            float stepY = (s + 0.5f) * 0.25f;
            float stepFrac = ((s + 0.5f) / 4.0f) - 0.5f;

            if (IsBridgeAt(coord.x, coord.y + 1))
            {
                step.center = new Vector3(0f, stepY, stepFrac * tileSize);
                step.size = new Vector3(tileSize * 0.98f, 0.26f, tileSize * 0.28f);
            }
            else if (IsBridgeAt(coord.x, coord.y - 1))
            {
                step.center = new Vector3(0f, stepY, -stepFrac * tileSize);
                step.size = new Vector3(tileSize * 0.98f, 0.26f, tileSize * 0.28f);
            }
            else if (IsBridgeAt(coord.x + 1, coord.y))
            {
                step.center = new Vector3(stepFrac * tileSize, stepY, 0f);
                step.size = new Vector3(tileSize * 0.28f, 0.26f, tileSize * 0.98f);
            }
            else
            {
                step.center = new Vector3(-stepFrac * tileSize, stepY, 0f);
                step.size = new Vector3(tileSize * 0.28f, 0.26f, tileSize * 0.98f);
            }
        }
    }

    public bool IsFarmCourtyardZone(int x, int y)
    {
        if (districtNodes == null || districtNodes.Count == 0) return false;
        DistrictNode farmNode = districtNodes.Find(n => n.type == GroundType.Farmland);
        if (farmNode.type != GroundType.Farmland) return false;

        // Binaların, ahırın, değirmenin ve arızalı traktörlerin yer aldığı kompakt saman avlu
        float dist = Vector2.Distance(new Vector2(x, y), farmNode.position);
        return dist <= 7.5f;
    }

    public bool IsFarmTilledPlotZone(int x, int y)
    {
        if (districtNodes == null || districtNodes.Count == 0) return false;
        DistrictNode farmNode = districtNodes.Find(n => n.type == GroundType.Farmland);
        if (farmNode.type != GroundType.Farmland) return false;

        int dx = x - Mathf.RoundToInt(farmNode.position.x);
        int dy = y - Mathf.RoundToInt(farmNode.position.y);

        // Avludan uzakta, düzenli sıralı 2 adet dikdörtgen sürülmüş tarla parseli (Doğu ve Kuzey parselleri)
        bool inEastPlot = (dx >= 9 && dx <= 21 && dy >= -6 && dy <= 6);
        bool inNorthPlot = (dy >= 9 && dy <= 21 && dx >= -6 && dx <= 6);
        return inEastPlot || inNorthPlot;
    }

    private bool IsFarmStructureZone(int x, int y)
    {
        return IsFarmCourtyardZone(x, y);
    }

    /// <summary>
    /// Yol hücresinin komşularını ve bulunduğu bölgeyi analiz eder:
    /// - Çiftlik (Farmland) bölgesindeki tüm yollar GravelRoad (toprak/çakıl yol) ile döşenir.
    /// - Şehir/Fabrika bölgesindeki 5 karoluk geniş caddelerde en dış şeritler RoadSides,
    ///   tam orta şerit RoadMiddle, ara şeritler ve kavşaklar RoadBase ile döşenir.
    /// </summary>
    private GameObject DetermineRoadTilePrefabAndRotation(Vector2Int coord, out Quaternion rot)
    {
        rot = Quaternion.identity;

        // Çiftlik bölgesindeki yollar tamamen GravelRoad asseti ile yapılır!
        if (baseDistrictData != null &&
            coord.x >= 0 && coord.x < currentWidth &&
            coord.y >= 0 && coord.y < currentHeight &&
            baseDistrictData[coord.x, coord.y] == GroundType.Farmland &&
            gravelRoadPrefab != null)
        {
            int gRot = Mathf.Abs((coord.x * 73856093 ^ coord.y * 19349663 ^ currentSeed) % 4);
            rot = Quaternion.Euler(0f, gRot * 90f, 0f);
            return gravelRoadPrefab;
        }

        bool northRoad = IsRoadAt(coord.x, coord.y + 1);
        bool southRoad = IsRoadAt(coord.x, coord.y - 1);
        bool eastRoad = IsRoadAt(coord.x + 1, coord.y);
        bool westRoad = IsRoadAt(coord.x - 1, coord.y);

        // 1. DÜŞEY YOL (Kuzey - Güney yönünde uzanan 5 karoluk geniş yol)
        // Düşey yolun Batı (Sol) dış kenarı:
        if (northRoad && southRoad && eastRoad && !westRoad)
        {
            rot = Quaternion.Euler(0f, 90f, 0f);
            return roadSidesPrefab ?? roadBasePrefab;
        }
        // Düşey yolun Doğu (Sağ) dış kenarı:
        if (northRoad && southRoad && westRoad && !eastRoad)
        {
            rot = Quaternion.Euler(0f, 270f, 0f);
            return roadSidesPrefab ?? roadBasePrefab;
        }

        // 2. YATAY YOL (Doğu - Batı yönünde uzanan 5 karoluk geniş yol)
        // Yatay yolun Kuzey (Üst) dış kenarı:
        if (eastRoad && westRoad && southRoad && !northRoad)
        {
            rot = Quaternion.identity;
            return roadSidesPrefab ?? roadBasePrefab;
        }
        // Yatay yolun Güney (Alt) dış kenarı:
        if (eastRoad && westRoad && northRoad && !southRoad)
        {
            rot = Quaternion.Euler(0f, 180f, 0f);
            return roadSidesPrefab ?? roadBasePrefab;
        }

        // 3. 5 KAROLUK GENİŞ YOLUN TAM ORTA ŞERİDİ (RoadMiddle)
        bool isVerticalSpan = !IsRoadAt(coord.x - 3, coord.y) && !IsRoadAt(coord.x + 3, coord.y);
        bool isHorizontalSpan = !IsRoadAt(coord.x, coord.y - 3) && !IsRoadAt(coord.x, coord.y + 3);

        // Düşey caddenin tam merkezi (solunda 2 yol karosu, sağında 2 yol karosu var, 3. karolar yol değil)
        if (northRoad && southRoad && isVerticalSpan && !IsRoadAt(coord.x - 3, coord.y) && !IsRoadAt(coord.x + 3, coord.y) &&
            IsRoadAt(coord.x - 2, coord.y) && IsRoadAt(coord.x + 2, coord.y))
        {
            rot = Quaternion.Euler(0f, 90f, 0f);
            return roadMiddlePrefab ?? roadBasePrefab;
        }

        // Yatay caddenin tam merkezi (altında 2 yol karosu, üstünde 2 yol karosu var, 3. karolar yol değil)
        if (eastRoad && westRoad && isHorizontalSpan && !IsRoadAt(coord.x, coord.y - 3) && !IsRoadAt(coord.x, coord.y + 3) &&
            IsRoadAt(coord.x, coord.y - 2) && IsRoadAt(coord.x, coord.y + 2))
        {
            rot = Quaternion.identity;
            return roadMiddlePrefab ?? roadBasePrefab;
        }

        // 4. Ara şeritler ve kavşaklar (RoadBase - düz asfalt)
        return roadBasePrefab ?? gravelRoadPrefab;
    }

    private bool IsRoadAt(int x, int y)
    {
        if (x < 0 || x >= currentWidth || y < 0 || y >= currentHeight) return false;
        return gridData[x, y] == GroundType.Road || gridData[x, y] == GroundType.Bridge || gridData[x, y] == GroundType.BridgeRamp;
    }

    /// <summary>
    /// Yolun en dış sınırına tek taraflı rayları yerleştirir.
    /// Yoldan çıkış/giriş yapılabilmesi için her 5 hücrede bir boşluk bırakır.
    /// Yalnızca tek yönde dış kenara bakan hücrelere doğru rotasyonla tek bir ray koyar.
    /// </summary>
    private void TrySpawnBridgeRailing(Vector2Int coord, Vector3 worldPos, Transform parent)
    {
        if (roadRailsPrefab == null) return;

        bool northRiver = IsRiverAt(coord.x, coord.y + 1);
        bool southRiver = IsRiverAt(coord.x, coord.y - 1);
        bool eastRiver = IsRiverAt(coord.x + 1, coord.y);
        bool westRiver = IsRiverAt(coord.x - 1, coord.y);

        Quaternion railRot;
        if (northRiver) railRot = Quaternion.Euler(0f, 0f, 0f);
        else if (southRiver) railRot = Quaternion.Euler(0f, 180f, 0f);
        else if (westRiver) railRot = Quaternion.Euler(0f, 90f, 0f);
        else if (eastRiver) railRot = Quaternion.Euler(0f, 270f, 0f);
        else return;

        GameObject rail = Instantiate(roadRailsPrefab, worldPos + Vector3.up * 0.05f, railRot);
        rail.transform.localScale = Vector3.one * (tileSize / 1.0f);
        NormalizePlacedObjectPivot(rail);
        rail.transform.SetParent(parent);
    }

    private bool IsRiverAt(int x, int y)
    {
        if (x < 0 || x >= currentWidth || y < 0 || y >= currentHeight) return false;
        return gridData[x, y] == GroundType.River;
    }

    private void SetRendererColorSafe(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        Material mat = renderer.sharedMaterial;
        
        // Eğer materyal yoksa veya URP'de varsayılan Built-in Default-Material (ve pembeleşmeye müsait) ise URP uyumlu yeni materyal ata
        if (mat == null || mat.shader == null || mat.shader.name == "Default-Material" || mat.shader.name == "Standard")
        {
            Material urpBase = RiverWaterAndFogSystem.Instance != null ? RiverWaterAndFogSystem.Instance.SharedWaterMaterial : null;
            if (urpBase != null && urpBase.shader != null)
            {
                mat = new Material(urpBase.shader);
            }
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
                mat = new Material(s);
            }
            renderer.material = mat;
        }

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private GameObject GetPrefabForType(GroundType type)
    {
        return type switch
        {
            GroundType.City => cityTilePrefab,
            GroundType.Farmland => farmTilePrefab,
            GroundType.Factory => factoryTilePrefab,
            GroundType.Transition => transitionTilePrefab,
            GroundType.PlayerBase => transitionTilePrefab, // Üs zemini doğal çim karo ile kusursuz bütünleşir
            GroundType.River => riverTilePrefab != null ? riverTilePrefab : (gravelRoadPrefab != null ? gravelRoadPrefab : transitionTilePrefab),
            GroundType.Bridge => bridgeTopPrefab != null ? bridgeTopPrefab : (bridgePrefab != null ? bridgePrefab : roadBasePrefab),
            GroundType.BridgeRamp => bridgeRampPrefab != null ? bridgeRampPrefab : (bridgePrefab != null ? bridgePrefab : roadBasePrefab),
            _ => null
        };
    }

    private void PositionPlayerAtBase()
    {
        var pc = FindFirstObjectByType<CharacterController25D>();
        if (pc != null)
        {
            playerTransform = pc.transform;
        }
        else if (playerTransform != null && !playerTransform.gameObject.scene.IsValid())
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) playerTransform = tagged.transform;
        }

        Vector3 originOffset = new Vector3(-currentWidth * 0.5f * tileSize, 0, -currentHeight * 0.5f * tileSize);
        Vector3 basePos = GridToWorldPosition(playerBaseCenter.x, playerBaseCenter.y, originOffset);
        BaseWorldPosition = basePos;

        foreach (var dn in districtNodes)
        {
            Vector3 wp = GridToWorldPosition(Mathf.RoundToInt(dn.position.x), Mathf.RoundToInt(dn.position.y), originOffset);
            if (dn.type == GroundType.City) CityCenterWorldPosition = wp;
            else if (dn.type == GroundType.Factory) FactoryHubWorldPosition = wp;
            else if (dn.type == GroundType.Farmland) FarmCenterWorldPosition = wp;
        }

        if (playerTransform != null)
        {
            var rb = playerTransform.GetComponent<Rigidbody>();
            Vector3 spawnPoint = basePos + Vector3.up * 1.05f;
            playerTransform.position = spawnPoint;
            if (rb != null)
            {
                rb.position = spawnPoint;
                rb.linearVelocity = Vector3.zero;
            }
        }

        // Atölye masasını ve oyuncu evini üsse yerleştir
        SpawnPlayerBaseStructures(basePos);

        // Bölgesel Eko-İstasyonları (Belediye Binası, Fabrika Merkez Tesisi + Bacalar + AirFilter, Çiftlik Traktörleri + Çiftçiler, Nehir Dubaları) yerleştir
        SpawnDistrictLandmarks(originOffset);
    }

    private void SpawnDistrictLandmarks(Vector3 originOffset)
    {
        bool spawnedCityHub = false;
        bool spawnedFactoryHub = false;
        int spawnedChimneys = 0;
        bool spawnedFarmCompost = false;
        int spawnedBrokenTractors = 0;

        int step = 5;

        for (int x = 10; x < currentWidth - 10; x += step)
        {
            for (int y = 10; y < currentHeight - 10; y += step)
            {
                GroundType type = gridData[x, y];
                Vector2Int coord = new Vector2Int(x, y);
                float distToBase = Vector2Int.Distance(coord, playerBaseCenter);

                if (distToBase < baseRadius + 9f) continue;
                if (!IsValidLandTile(x, y) || !IsClearOfRoads(x, y, 2)) continue;

                Vector3 worldPos = GridToWorldPosition(x, y, originOffset);

                // 1. ŞEHİR MERKEZİ: TownHall (Belediye Binası & Gönüllü Merkezi)
                if (!spawnedCityHub && type == GroundType.City)
                {
                    GameObject hubAsset = townHallVoxPrefab != null ? townHallVoxPrefab : cityHubPrefab;
                    GameObject hubObj = SpawnStationObject(hubAsset, worldPos, "TownHall_CityHub", typeof(CityRecruitmentHub));
                    if (townHallVoxPrefab != null)
                    {
                        ApplySmartBuildingScalePivotAndCollider(hubObj);
                    }
                    CityCenterWorldPosition = worldPos;
                    spawnedCityHub = true;
                }
                // 2. FABRİKA BÖLGESİ: Ana RecyclingBuilding Tesisi + Filtrelenebilir Bacalar (FactoryChimney + AirFilter)
                else if (type == GroundType.Factory)
                {
                    if (!spawnedFactoryHub && recyclingBuildingVoxPrefab != null)
                    {
                        GameObject facHub = Instantiate(recyclingBuildingVoxPrefab, worldPos, Quaternion.identity);
                        facHub.name = "MainFactory_RecyclingBuildingHub";
                        facHub.transform.SetParent(mapParent);
                        ApplySmartBuildingScalePivotAndCollider(facHub);
                        if (facHub.GetComponent<FactoryRecyclingHub>() == null)
                            facHub.AddComponent<FactoryRecyclingHub>();

                        FactoryHubWorldPosition = worldPos;
                        spawnedFactoryHub = true;
                    }
                    else if (spawnedChimneys < 3)
                    {
                        GameObject chimAsset = factoryChimneyVoxPrefab != null ? factoryChimneyVoxPrefab : chimneyPrefab;
                        GameObject chimObj = SpawnStationObject(chimAsset, worldPos, $"FactoryChimney_{spawnedChimneys + 1}", typeof(FactoryFilterChimney));
                        if (factoryChimneyVoxPrefab != null)
                        {
                            chimObj.transform.localScale = Vector3.one * 2.2f;
                            NormalizePlacedObjectPivot(chimObj);
                            EnsureSolidCollider(chimObj);
                        }

                        var smokeEmit = chimObj.GetComponent<FactorySmokeEmitter>() ?? chimObj.AddComponent<FactorySmokeEmitter>();
                        smokeEmit.EnsureSmokeEmission();

                        // Yeni eklenen AirFilter.vox modelini bacaya bağla (filtre takılınca görünür olur)
                        if (airFilterVoxPrefab != null)
                        {
                            GameObject filterInst = Instantiate(airFilterVoxPrefab, chimObj.transform);
                            filterInst.name = "InstalledAirFilter";
                            filterInst.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                            filterInst.transform.localScale = Vector3.one * 1.15f;
                            NormalizePlacedObjectPivot(filterInst);
                            filterInst.SetActive(false);

                            var chimneyComp = chimObj.GetComponent<FactoryFilterChimney>();
                            if (chimneyComp != null)
                            {
                                chimneyComp.SetFilterVisualObject(filterInst);
                            }
                        }

                        spawnedChimneys++;
                    }
                }
                // 3. ÇİFTLİK BÖLGESİ: Arızalı Traktörler (farm-60-tractor, farm-59-tractor2), Yanlarında Çiftçi NPC'ler ve Kompost İstasyonu
                else if (type == GroundType.Farmland && IsFarmStructureZone(x, y))
                {
                    if (!spawnedFarmCompost)
                    {
                        SpawnStationObject(compostStationPrefab, worldPos, "FarmlandCompostStation", typeof(FarmlandCompostStation));
                        FarmCenterWorldPosition = worldPos;
                        spawnedFarmCompost = true;
                    }
                    else if (spawnedBrokenTractors < 3 && farmTractorPrefabs != null && farmTractorPrefabs.Count > 0)
                    {
                        GameObject tractorAsset = farmTractorPrefabs[spawnedBrokenTractors % farmTractorPrefabs.Count];
                        if (tractorAsset != null)
                        {
                            GameObject trObj = Instantiate(tractorAsset, worldPos, Quaternion.Euler(0f, spawnedBrokenTractors * 90f, 0f));
                            trObj.name = $"BrokenFarmTractor_{spawnedBrokenTractors + 1}";
                            trObj.transform.localScale = Vector3.one * (farmStructureScaleMultiplier * (tileSize / 1.0f));
                            trObj.transform.SetParent(mapParent);
                            NormalizePlacedObjectPivot(trObj);
                            EnsureSolidCollider(trObj);

                            var brokenComp = trObj.AddComponent<BrokenFarmMachineInteractable>();
                            brokenComp.InitializeMachine($"Çiftlik Traktörü #{spawnedBrokenTractors + 1}");

                            // Traktörün hemen yanına Çiftçi NPC yerleştir (ayakları tam Y=0 zemininde!)
                            SpawnFarmerNPCNear(worldPos + new Vector3(2.2f, 0f, 1.2f), spawnedBrokenTractors + 1);
                            spawnedBrokenTractors++;
                        }
                    }
                }
            }
        }

        // 4. NEHİR KANALI: Buoy.vox kullanarak 4 adet Nehir Temizleme & Balık/Atık Yakalama Dubası yerleştir
        if (riverCenterWorldPoints.Count > 0)
        {
            int buoyCount = 4;
            int stepIdx = Mathf.Max(1, riverCenterWorldPoints.Count / (buoyCount + 1));
            for (int b = 1; b <= buoyCount; b++)
            {
                int idx = Mathf.Clamp(b * stepIdx, 0, riverCenterWorldPoints.Count - 1);
                Vector3 buoyPos = riverCenterWorldPoints[idx];
                buoyPos.y = -0.22f; // Su yüzeyi kotu

                GameObject buoyAsset = buoyVoxPrefab != null ? buoyVoxPrefab : riverBuoyPrefab;
                GameObject bObj = SpawnStationObject(buoyAsset, buoyPos, $"RiverBuoyStation_{b}", typeof(RiverBuoyStation));
                if (buoyVoxPrefab != null)
                {
                    bObj.transform.localScale = Vector3.one * 2.2f;
                    NormalizePlacedObjectPivot(bObj);
                    bObj.transform.position = new Vector3(buoyPos.x, -0.22f, buoyPos.z);
                    EnsureSolidCollider(bObj);
                }
            }
        }
    }

    private void SpawnFarmerNPCNear(Vector3 pos, int farmerIndex)
    {
        if (cityCitizenNPCPrefabs == null || cityCitizenNPCPrefabs.Count == 0) return;

        GameObject prefab = cityCitizenNPCPrefabs[farmerIndex % cityCitizenNPCPrefabs.Count];
        if (prefab == null) return;

        pos.y = 0f;
        GameObject farmerObj = Instantiate(prefab, pos, Quaternion.Euler(0f, 180f, 0f));
        farmerObj.name = $"FarmerNPC_{farmerIndex}";
        farmerObj.transform.SetParent(mapParent);

        EnsureCitizenComponents(farmerObj, prefab);
        var citizen = farmerObj.GetComponent<CityCitizenNPC>();
        if (citizen != null)
        {
            string farmerName = TurkishNameGenerator.GetRandomFarmerName();
            citizen.InitializeCitizen(farmerName, false, 15f);
        }
    }

    /// <summary>
    /// Şehir ve Fabrika bölgelerine tüm yeni ev/bina modellerini (House1, House2, SmallHouse1-3, TownHall, LargeBuilding1-2, RecyclingBuilding),
    /// çöp kutularını, şehirde gezen vatandaş NPC'leri ve Tarım/Bozkır alanlarına çiftlik yapılarını/ağaçları yerleştirir.
    /// </summary>
    private void SpawnDistrictDecorations(Vector3 originOffset)
    {
        int decorStep = 7; // 2 kat büyütülen yeni evlerin iç içe geçmemesi için 7 karoda bir parsel kontrolü
        int spawnedCitizenCount = 0;
        int maxCitizens = 20;

        for (int x = 6; x < currentWidth - 6; x += decorStep)
        {
            for (int y = 6; y < currentHeight - 6; y += decorStep)
            {
                GroundType type = gridData[x, y];
                Vector2Int coord = new Vector2Int(x, y);

                // Oyuncu üssünün etrafında geniş bir açık alan bırak (hiçbir bina/ağaç doğmasın)
                if (Vector2Int.Distance(coord, playerBaseCenter) < baseRadius + 7f) continue;
                if (type == GroundType.Road || type == GroundType.River || type == GroundType.Bridge || type == GroundType.Empty) continue;

                Vector3 worldPos = GridToWorldPosition(x, y, originOffset);

                // 1. ŞEHİR BÖLGESİ -> TÜM ŞEHİR KAROLARINA YAYILAN EVLER, APARTMANLAR, BELEDİYE BİNASI, ÇÖP KUTULARI VE NPC'LER
                if (type == GroundType.City)
                {
                    if (!IsValidLandTile(x, y)) continue;

                    int cityHash = Mathf.Abs((x * 73856093 ^ y * 19349663 ^ currentSeed) % 100);
                    bool clearForBuilding = IsClearOfRoads(x, y, 2);

                    // A) Sokak taşmasını önleyerek parsel içine Bina / Ev yerleştir (%70 ihtimal)
                    if (clearForBuilding && cityHash < 70)
                    {
                        GameObject buildingPrefab = null;
                        if (cityBuildingPrefabs != null && cityBuildingPrefabs.Count > 0)
                        {
                            int idx = Mathf.Abs((x * 19 + y * 37 + currentSeed) % cityBuildingPrefabs.Count);
                            buildingPrefab = cityBuildingPrefabs[idx];
                        }
                        else
                        {
                            buildingPrefab = ((x + y) % 2 == 0) ? largeBuilding1Prefab : largeBuilding2Prefab;
                            if (buildingPrefab == null) buildingPrefab = largeBuilding1Prefab ?? largeBuilding2Prefab;
                        }

                        if (buildingPrefab != null)
                        {
                            Quaternion bRot = Quaternion.Euler(0f, ((x * 3 + y * 7) % 4) * 90f, 0f);
                            GameObject bObj = Instantiate(buildingPrefab, worldPos, bRot);
                            bObj.name = $"CityBuilding_{x}_{y}";
                            bObj.transform.SetParent(mapParent);

                            ApplySmartBuildingScalePivotAndCollider(bObj);
                        }
                    }
                    // B) Şehir İçi Kaldırım / Park Ağacı veya Çöp Kutusu (%18 ihtimal)
                    else if (cityHash < 88)
                    {
                        if (trashCanPrefab != null && (cityHash % 2 == 0))
                        {
                            Quaternion tcRot = Quaternion.Euler(0f, ((x + y) % 4) * 90f, 0f);
                            GameObject tcObj = Instantiate(trashCanPrefab, worldPos, tcRot);
                            tcObj.name = $"CityTrashCan_{x}_{y}";
                            tcObj.transform.localScale = Vector3.one * 0.55f;
                            tcObj.transform.SetParent(mapParent);
                            NormalizePlacedObjectPivot(tcObj);
                            EnsureSolidCollider(tcObj);
                        }
                        else if (treePrefab != null)
                        {
                            Quaternion tRot = Quaternion.Euler(0f, (cityHash * 13) % 360, 0f);
                            GameObject treeObj = Instantiate(treePrefab, worldPos, tRot);
                            treeObj.name = $"CityTree_{x}_{y}";
                            treeObj.transform.localScale = Vector3.one * (2.0f * (tileSize / 1.0f));
                            treeObj.transform.SetParent(mapParent);
                            NormalizePlacedObjectPivot(treeObj);
                            EnsureSolidCollider(treeObj);
                        }
                    }
                    // C) Açık Şehir Kaldırımlarında / Meydanlarında Gezen Vatandaş NPC'ler (Kadın, Erkek, Çocuk)
                    else if (spawnedCitizenCount < maxCitizens && cityCitizenNPCPrefabs != null && cityCitizenNPCPrefabs.Count > 0)
                    {
                        GameObject npcPrefab = cityCitizenNPCPrefabs[spawnedCitizenCount % cityCitizenNPCPrefabs.Count];
                        if (npcPrefab != null)
                        {
                            Quaternion npcRot = Quaternion.Euler(0f, (cityHash * 29) % 360, 0f);
                            Vector3 npcSpawnPos = new Vector3(worldPos.x, 0f, worldPos.z);
                            GameObject npcObj = Instantiate(npcPrefab, npcSpawnPos, npcRot);
                            npcObj.name = $"CityCitizen_{spawnedCitizenCount + 1}_{x}_{y}";
                            npcObj.transform.SetParent(mapParent);

                            EnsureCitizenComponents(npcObj, npcPrefab);

                            spawnedCitizenCount++;
                        }
                    }
                }
                // 2. FABRİKA BÖLGESİ -> SANAYİ BİNALARI / GERİ DÖNÜŞÜM TESİSLERİ (RecyclingBuilding vb.)
                else if (type == GroundType.Factory)
                {
                    if (!IsValidLandTile(x, y)) continue;
                    bool clearForBuilding = IsClearOfRoads(x, y, 2);
                    int facHash = Mathf.Abs((x * 73856093 ^ y * 19349663 ^ currentSeed) % 100);

                    if (clearForBuilding && facHash < 45 && factoryBuildingPrefabs != null && factoryBuildingPrefabs.Count > 0)
                    {
                        GameObject factoryBuilding = factoryBuildingPrefabs[(x + y) % factoryBuildingPrefabs.Count];
                        if (factoryBuilding != null)
                        {
                            Quaternion fRot = Quaternion.Euler(0f, ((x * 3 + y * 7) % 4) * 90f, 0f);
                            GameObject fObj = Instantiate(factoryBuilding, worldPos, fRot);
                            fObj.name = $"FactoryBuilding_{x}_{y}";
                            fObj.transform.SetParent(mapParent);

                            ApplySmartBuildingScalePivotAndCollider(fObj);

                            if (factoryBuilding.name.IndexOf("RecyclingBuilding", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (fObj.GetComponent<FactoryRecyclingHub>() == null)
                                    fObj.AddComponent<FactoryRecyclingHub>();
                            }

                            // Fabrika sanayi binalarına gökyüzüne tüten duman ekle
                            var bSmoke = fObj.GetComponent<FactorySmokeEmitter>() ?? fObj.AddComponent<FactorySmokeEmitter>();
                            bSmoke.EnsureSmokeEmission();
                        }
                    }
                }
                // 3. TARLA VE TAŞRA BÖLGELERİ -> SAMAN BÖLGESİNDE YAPILAR+ÇATILARI, UZAK TOPRAK BÖLGESİNDE 4 AŞAMALI EKİNLER
                else if (type == GroundType.Farmland)
                {
                    if (!IsValidLandTile(x + 1, y) || !IsValidLandTile(x - 1, y) ||
                        !IsValidLandTile(x, y + 1) || !IsValidLandTile(x, y - 1))
                    {
                        continue;
                    }

                    int farmHash = Mathf.Abs((x * 73856093 ^ y * 19349663 ^ currentSeed) % 100);
                    bool inStructureHayZone = IsFarmStructureZone(x, y);

                    // 3A) SAMAN (HAY) ZEMİNLİ ÇİFTLİK AVLUSU -> SADECE ÇİFTLİK YAPILARI (ÇATILARIYLA BİRLİKTE) VE HAYVANLAR
                    if (inStructureHayZone)
                    {
                        if (IsClearOfRoads(x, y, 2) && farmHash < 38 && farmStructurePrefabs != null && farmStructurePrefabs.Count > 0)
                        {
                            GameObject farmStruct = farmStructurePrefabs[(x + y) % farmStructurePrefabs.Count];
                            if (farmStruct != null)
                            {
                                Quaternion sRot = Quaternion.Euler(0f, ((farmHash * 7) % 4) * 90f, 0f);
                                GameObject structObj = Instantiate(farmStruct, worldPos, sRot);
                                structObj.name = $"FarmStructure_{x}_{y}";
                                structObj.transform.localScale = Vector3.one * (farmStructureScaleMultiplier * (tileSize / 1.0f));
                                structObj.transform.SetParent(mapParent);

                                // Pivotu tabana oturt
                                NormalizePlacedObjectPivot(structObj);

                                // Ahır (barn) veya Değirmen (windmill) ise üstüne çatı (ve pervane) nesnesini yerleştir!
                                AttachRoofToFarmStructure(structObj, farmStruct.name);

                                // Katı çarpışma kutusu ekle
                                EnsureSolidCollider(structObj);

                                // Değirmen veya Traktör ise başlangıçta Arızalı (Tamir Kiti ile onarılabilir) olarak işaretle
                                string lowerName = farmStruct.name.ToLower();
                                if (lowerName.Contains("windmill") || lowerName.Contains("tractor"))
                                {
                                    var broken = structObj.AddComponent<BrokenFarmMachineInteractable>();
                                    broken.InitializeMachine(lowerName.Contains("windmill") ? "Çiftlik Yel Değirmeni" : "Çiftlik Traktörü");
                                }
                            }
                        }
                        else if (farmHash < 72 && farmAnimalPrefabs != null && farmAnimalPrefabs.Count > 0)
                        {
                            GameObject animalOrProp = farmAnimalPrefabs[farmHash % farmAnimalPrefabs.Count];
                            if (animalOrProp != null)
                            {
                                Quaternion aRot = Quaternion.Euler(0f, (farmHash * 13) % 360, 0f);
                                GameObject aObj = Instantiate(animalOrProp, worldPos, aRot);
                                aObj.name = $"FarmYardProp_{x}_{y}";
                                aObj.transform.localScale = Vector3.one * (farmCropScaleMultiplier * (tileSize / 1.0f));
                                aObj.transform.SetParent(mapParent);

                                NormalizePlacedObjectPivot(aObj);
                                EnsureSolidCollider(aObj);
                            }
                        }
                    }
                    // 3B) SÜRÜLMÜŞ TARLA PARSELİ (FarmTileTilledSOIL) -> 4 AŞAMALI DÜZ SIRALI EKİNLER
                    else if (IsFarmTilledPlotZone(x, y))
                    {
                        if (farmHash < 85)
                        {
                            SpawnFourStageCropPlot(worldPos, x, y, farmHash);
                        }
                    }
                    // 3C) ÇİFTLİK GENEL ÇİM ZEMİNİ (GrassBaseTile) -> AĞAÇLAR VE ÇAYIR BİTKİLERİ
                    else
                    {
                        if (farmHash < 35 && treePrefab != null)
                        {
                            Quaternion tRot = Quaternion.Euler(0f, (farmHash * 17) % 360, 0f);
                            GameObject treeObj = Instantiate(treePrefab, worldPos, tRot);
                            treeObj.name = $"FarmTree_{x}_{y}";
                            float treeScaleVar = 2.0f + (farmHash % 15) * 0.05f;
                            treeObj.transform.localScale = Vector3.one * (treeScaleVar * (tileSize / 1.0f));
                            treeObj.transform.SetParent(mapParent);

                            NormalizePlacedObjectPivot(treeObj);
                            EnsureSolidCollider(treeObj);
                        }
                    }
                }
                // 4. BOZKIR / ARA GEÇİŞ BÖLGELERİ -> AĞAÇLAR
                else if (type == GroundType.Transition)
                {
                    if (!IsValidLandTile(x + 1, y) || !IsValidLandTile(x - 1, y) ||
                        !IsValidLandTile(x, y + 1) || !IsValidLandTile(x, y - 1))
                    {
                        continue;
                    }

                    int treeHash = Mathf.Abs((x * 73856093 ^ y * 19349663 ^ currentSeed) % 100);
                    if (treeHash < 30 && treePrefab != null)
                    {
                        Quaternion tRot = Quaternion.Euler(0f, (treeHash * 17) % 360, 0f);
                        GameObject treeObj = Instantiate(treePrefab, worldPos, tRot);
                        treeObj.name = $"Tree_{x}_{y}";
                        float treeScaleVar = 2.2f + (treeHash % 15) * 0.05f;
                        treeObj.transform.localScale = Vector3.one * (treeScaleVar * (tileSize / 1.0f));
                        treeObj.transform.SetParent(mapParent);

                        NormalizePlacedObjectPivot(treeObj);
                        EnsureSolidCollider(treeObj);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Çiftlik bölgesindeki Ahır (farm-21-barn) ve Yel Değirmeni (farm-2-windmill) yapılarının
    /// üstüne kendi çatı modellerini (farm-20-barn_roof, farm-0-windmill_roof) ve değirmen pervanesini
    /// (farm-1-windmill_blade) tam tepe yüksekliğine hizalayarak yerleştirir.
    /// </summary>
    private void AttachRoofToFarmStructure(GameObject baseStructObj, string assetName)
    {
        if (baseStructObj == null || string.IsNullOrEmpty(assetName)) return;

        string lower = assetName.ToLower();
        GameObject roofPrefab = null;
        GameObject bladePrefab = null;

        if (lower.Contains("barn") && barnRoofPrefab != null)
        {
            roofPrefab = barnRoofPrefab;
        }
        else if (lower.Contains("windmill") && windmillRoofPrefab != null)
        {
            roofPrefab = windmillRoofPrefab;
            bladePrefab = windmillBladePrefab;
        }

        if (roofPrefab == null) return;

        if (!TryGetCombinedLocalBounds(baseStructObj, out Bounds baseBounds)) return;

        // Alt yapının dünya uzayındaki en üst noktası (NormalizePlacedObjectPivot sonrası taban Y=0'dadır)
        float structScaleY = baseStructObj.transform.localScale.y;
        float topWorldY = baseBounds.size.y * structScaleY;

        GameObject roofObj = Instantiate(roofPrefab, baseStructObj.transform.position, baseStructObj.transform.rotation);
        roofObj.name = $"{roofPrefab.name}_Attached";
        roofObj.transform.localScale = baseStructObj.transform.localScale;
        roofObj.transform.position = new Vector3(baseStructObj.transform.position.x, topWorldY, baseStructObj.transform.position.z);

        // Çatının kendi alt tabanını tam olarak yapının üst yüzeyine (topWorldY) oturt
        NormalizePlacedObjectPivot(roofObj);
        roofObj.transform.SetParent(baseStructObj.transform, true);

        // Değirmen ise pervanesini de çatının ön cephesine tak
        if (bladePrefab != null && TryGetCombinedLocalBounds(roofObj, out Bounds roofBounds))
        {
            float roofHeightWorld = roofBounds.size.y * roofObj.transform.localScale.y;
            float roofDepthHalfWorld = (roofBounds.size.z * 0.5f) * roofObj.transform.localScale.z;

            Vector3 bladePos = new Vector3(baseStructObj.transform.position.x, topWorldY + roofHeightWorld * 0.45f, baseStructObj.transform.position.z)
                               - (baseStructObj.transform.forward * (roofDepthHalfWorld + 0.15f));

            GameObject bladeObj = Instantiate(bladePrefab, bladePos, baseStructObj.transform.rotation);
            bladeObj.name = $"{bladePrefab.name}_Attached";
            bladeObj.transform.localScale = baseStructObj.transform.localScale;
            bladeObj.transform.SetParent(roofObj.transform, true);
        }
    }

    private void EnsureCitizenComponents(GameObject npcObj, GameObject sourceAsset)
    {
        if (npcObj == null) return;

        // Dinamik Rigidbody ile çakışan tüm içbükey (concave) MeshCollider bileşenlerini (kapalı Step1/Step2 kareleri dahil) temizle!
        foreach (var mc in npcObj.GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc != null)
            {
                if (Application.isPlaying) Destroy(mc);
                else DestroyImmediate(mc);
            }
        }

        var cap = npcObj.GetComponent<CapsuleCollider>();
        if (cap == null)
        {
            cap = npcObj.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.9f, 0f);
            cap.radius = 0.4f;
            cap.height = 1.8f;
        }

        var rb = npcObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = npcObj.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Vector3 p = npcObj.transform.position;
        p.y = 0f;
        npcObj.transform.position = p;

        if (npcObj.GetComponent<FollowerNPC>() == null)
        {
            npcObj.AddComponent<FollowerNPC>();
        }

        var anim = npcObj.GetComponent<VoxelCharacterAnimator>();
        if (anim == null && sourceAsset != null)
        {
            // Eğer doğrudan Child.vox gibi ham modelden üretildiyse mevcut mesh'i gizleyip VoxelCharacterAnimator kur
            foreach (var r in npcObj.GetComponentsInChildren<Renderer>())
            {
                r.enabled = false;
            }
            anim = npcObj.AddComponent<VoxelCharacterAnimator>();
            anim.idleModel = sourceAsset;
            anim.walkStep1Model = sourceAsset;
            anim.walkStep2Model = sourceAsset;
            anim.InitializeModelInstances();
        }

        if (npcObj.GetComponent<CityCitizenNPC>() == null)
        {
            npcObj.AddComponent<CityCitizenNPC>();
        }
    }

    private bool IsClearOfRoads(int cx, int cy, int clearanceRadius)
    {
        for (int ox = -clearanceRadius; ox <= clearanceRadius; ox++)
        {
            for (int oy = -clearanceRadius; oy <= clearanceRadius; oy++)
            {
                int rx = cx + ox;
                int ry = cy + oy;
                if (rx >= 0 && rx < currentWidth && ry >= 0 && ry < currentHeight)
                {
                    if (gridData[rx, ry] == GroundType.Road || gridData[rx, ry] == GroundType.Bridge)
                        return false;
                }
            }
        }
        return true;
    }

    private bool TryGetCombinedLocalBounds(GameObject obj, out Bounds combinedBounds)
    {
        combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (obj == null) return false;

        MeshFilter[] mfs = obj.GetComponentsInChildren<MeshFilter>();
        bool hasBounds = false;

        foreach (var mf in mfs)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 childOffset = (mf.transform != obj.transform) ? mf.transform.localPosition : Vector3.zero;
            Bounds shifted = new Bounds(childOffset + mb.center, mb.size);

            if (!hasBounds)
            {
                combinedBounds = shifted;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(shifted);
            }
        }

        return hasBounds;
    }

    /// <summary>
    /// Hem 16x16x32'lik küçük voksel binaları (LargeBuilding) hem de yeni eklenen tam boyutlu
    /// evleri (House1, House2, SmallHouse1-3, TownHall, RecyclingBuilding) kendi taban genişliğine
    /// göre akıllıca ölçekler, alt tabanını Y=0 zeminine oturtur ve tam oturan BoxCollider ekler.
    /// </summary>
    private void ApplySmartBuildingScalePivotAndCollider(GameObject obj)
    {
        if (obj == null) return;

        if (TryGetCombinedLocalBounds(obj, out Bounds localBounds))
        {
            float maxHorizontalSpan = Mathf.Max(localBounds.size.x, localBounds.size.z);
            float scaleFactor;

            if (maxHorizontalSpan <= 1.6f)
            {
                // 16x16x32 voksel (1m x 1m) olan eski LargeBuilding modelleri
                scaleFactor = buildingScaleMultiplier * (tileSize / 1.0f);
            }
            else if (maxHorizontalSpan <= 5.0f)
            {
                // SmallHouse, SmallHouse2, SmallHouse3, House2 (2 kat büyütüldü: 1.6f -> 3.2f)
                scaleFactor = 3.2f;
            }
            else if (maxHorizontalSpan <= 8.5f)
            {
                // House1, TownHall (2 kat büyütüldü: 1.35f -> 2.7f)
                scaleFactor = 2.7f;
            }
            else
            {
                // RecyclingBuilding (2 kat büyütüldü: 1.05f -> 2.1f)
                scaleFactor = 2.1f;
            }

            obj.transform.localScale = Vector3.one * scaleFactor;

            // Alt tabanı tam olarak Y=0 zeminine hizala (ne gömülür ne de havada kalır)
            float bottomY = localBounds.min.y * scaleFactor;
            obj.transform.position -= new Vector3(0f, bottomY, 0f);

            // Alt nesne kaynaklı içbükey MeshCollider'ları anında devre dışı bırak ve ana nesneye tam oturan BoxCollider ekle
            foreach (var mc in obj.GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc != null)
                {
                    mc.enabled = false;
                    Destroy(mc);
                }
            }

            var box = obj.GetComponent<BoxCollider>();
            if (box == null) box = obj.AddComponent<BoxCollider>();
            box.center = localBounds.center;
            box.size = localBounds.size;
            box.isTrigger = false;
        }
        else
        {
            obj.transform.localScale = Vector3.one * (buildingScaleMultiplier * (tileSize / 1.0f));
            EnsureSolidCollider(obj);
        }
    }

    private bool IsNearRoad(int cx, int cy, int searchRadius)
    {
        for (int ox = -searchRadius; ox <= searchRadius; ox++)
        {
            for (int oy = -searchRadius; oy <= searchRadius; oy++)
            {
                int rx = cx + ox;
                int ry = cy + oy;
                if (rx >= 0 && rx < currentWidth && ry >= 0 && ry < currentHeight)
                {
                    if (gridData[rx, ry] == GroundType.Road) return true;
                }
            }
        }
        return false;
    }

    private bool IsValidLandTile(int x, int y)
    {
        // En az 3 karo harita sınırlarından ve boşluklardan içeride olmalı
        if (x < 3 || x >= currentWidth - 3 || y < 3 || y >= currentHeight - 3) return false;
        
        // 3x3 etrafındaki hiçbir hücre Empty (Boşluk) veya River (Nehir) olmamalı!
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oy = -2; oy <= 2; oy++)
            {
                int rx = x + ox;
                int ry = y + oy;
                if (rx < 0 || rx >= currentWidth || ry < 0 || ry >= currentHeight) return false;
                GroundType t = gridData[rx, ry];
                if (t == GroundType.Empty || t == GroundType.River) return false;
            }
        }
        return true;
    }

    private void EnsureSolidCollider(GameObject obj)
    {
        if (obj == null) return;

        foreach (var mc in obj.GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc != null)
            {
                mc.enabled = false;
                Destroy(mc);
            }
        }

        var box = obj.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = obj.AddComponent<BoxCollider>();
        }
        box.isTrigger = false;

        if (TryGetCombinedLocalBounds(obj, out Bounds localBounds))
        {
            box.center = localBounds.center;
            box.size = localBounds.size;
        }
    }

    /// <summary>
    /// Voxel modellerinin pivotları bazen merkezde veya havada olabildiği için
    /// alt tabanını (min Y) sıfırlayarak toprağa gömülmesini veya havada kalmasını engeller.
    /// </summary>
    private void NormalizePlacedObjectPivot(GameObject obj)
    {
        if (obj == null) return;

        foreach (var mc in obj.GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc != null)
            {
                mc.enabled = false;
                Destroy(mc);
            }
        }

        if (TryGetCombinedLocalBounds(obj, out Bounds localBounds))
        {
            float minY = localBounds.min.y;
            if (Mathf.Abs(minY) > 0.005f)
            {
                obj.transform.position -= new Vector3(0f, minY * obj.transform.localScale.y, 0f);
            }
        }
    }

    private void SpawnFourStageCropPlot(Vector3 worldPos, int x, int y, int farmHash)
    {
        GameObject plotRoot = new GameObject($"FarmCropPlot_{x}_{y}");
        plotRoot.transform.position = new Vector3(worldPos.x, 0f, worldPos.z);
        plotRoot.transform.SetParent(mapParent);

        var col = plotRoot.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.45f, 0f);
        col.size = new Vector3(1.3f, 0.9f, 1.3f);
        col.isTrigger = true;

        var cropPlot = plotRoot.AddComponent<FarmCropPlot>();

        // Mısır, Domates, Havuç veya Pancar türü seç
        int varietyIndex = (farmHash / 3) % 4;
        string cropName;
        GameObject[] selectedStages;

        switch (varietyIndex)
        {
            case 0:
                cropName = "Eko-Mısır";
                selectedStages = cornStagePrefabs;
                break;
            case 1:
                cropName = "Organik Domates";
                selectedStages = tomatoStagePrefabs;
                break;
            case 2:
                cropName = "Taze Havuç";
                selectedStages = carrotStagePrefabs;
                break;
            default:
                cropName = "Şeker Pancarı";
                selectedStages = beetStagePrefabs;
                break;
        }

        // Eğer 4 aşamalı prefab dizisi atanmamışsa farmCropPrefabs içinden yedek oluştur
        if (selectedStages == null || selectedStages.Length < 4 || selectedStages[0] == null)
        {
            if (farmCropPrefabs != null && farmCropPrefabs.Count > 0)
            {
                GameObject fallback = farmCropPrefabs[farmHash % farmCropPrefabs.Count];
                selectedStages = new GameObject[] { fallback, fallback, fallback, fallback };
            }
        }

        GameObject[] instStages = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject prefab = (selectedStages != null && i < selectedStages.Length) ? selectedStages[i] : null;
            GameObject stageObj;
            if (prefab != null)
            {
                stageObj = Instantiate(prefab, plotRoot.transform);
                stageObj.transform.localPosition = Vector3.zero;
                // Ekinler sürülmüş tarla çizgilerine tam paralel hizalanır (Rotation = 0)
                stageObj.transform.localRotation = Quaternion.identity;
                float stageScale = farmCropScaleMultiplier * (tileSize / 1.0f) * (0.55f + i * 0.15f);
                stageObj.transform.localScale = Vector3.one * stageScale;
                NormalizePlacedObjectPivot(stageObj);
            }
            else
            {
                stageObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stageObj.transform.SetParent(plotRoot.transform, false);
                float h = 0.2f + i * 0.18f;
                stageObj.transform.localScale = new Vector3(0.35f, h, 0.35f);
                stageObj.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
                Destroy(stageObj.GetComponent<Collider>());
                SetRendererColorSafe(stageObj.GetComponent<Renderer>(), new Color(0.25f, 0.65f + i * 0.08f, 0.25f));
            }
            stageObj.name = $"Stage_{i + 1}";
            instStages[i] = stageObj;
        }

        cropPlot.SetupCropStages(cropName, instStages[0], instStages[1], instStages[2], instStages[3]);
    }

    private GameObject SpawnStationObject(GameObject prefab, Vector3 worldPos, string defaultName, System.Type componentType)
    {
        GameObject obj;
        if (prefab != null)
        {
            obj = Instantiate(prefab, worldPos, Quaternion.identity);
            obj.name = defaultName;
        }
        else
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.position = worldPos + Vector3.up * 0.5f;
            obj.name = defaultName;
        }

        obj.transform.SetParent(mapParent);
        if (componentType != null && obj.GetComponent(componentType) == null)
        {
            obj.AddComponent(componentType);
        }
        return obj;
    }

    private void SpawnPlayerBaseStructures(Vector3 baseWorldPos)
    {
        // 1. Oyuncu Evi (Base Cabin / Home)
        Vector3 housePos = baseWorldPos + new Vector3(-3.8f, 0f, 2.5f);
        GameObject housePrefab = null;
        if (cityBuildingPrefabs != null && cityBuildingPrefabs.Count > 0)
        {
            housePrefab = cityBuildingPrefabs.Find(b => b != null && b.name.ToLower().Contains("smallhouse"))
                          ?? cityBuildingPrefabs.Find(b => b != null && b.name.ToLower().Contains("house"))
                          ?? cityBuildingPrefabs[0];
        }

        if (housePrefab != null)
        {
            GameObject houseObj = Instantiate(housePrefab, housePos, Quaternion.Euler(0f, 90f, 0f));
            houseObj.name = "Player_Base_Home";
            houseObj.transform.SetParent(mapParent);
            ApplySmartBuildingScalePivotAndCollider(houseObj);
        }

        // 2. Hurdalık Atölyesi (Workbench)
        Vector3 tablePos = baseWorldPos + new Vector3(2.0f, 0f, 1.2f);
        GameObject benchObj;

        if (workbenchPrefab != null)
        {
            benchObj = Instantiate(workbenchPrefab, tablePos, Quaternion.identity);
        }
        else
        {
            benchObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            benchObj.transform.position = tablePos + Vector3.up * 0.45f;
            benchObj.transform.localScale = new Vector3(1.6f, 0.9f, 1.0f);
            benchObj.name = "Hurdalık_Atölyesi_Workbench";

            var rend = benchObj.GetComponent<Renderer>();
            SetRendererColorSafe(rend, new Color(0.85f, 0.65f, 0.25f));
        }

        benchObj.transform.SetParent(mapParent);

        if (benchObj.GetComponent<WorkbenchInteractable>() == null)
            benchObj.AddComponent<WorkbenchInteractable>();

        var col = benchObj.GetComponent<BoxCollider>();
        if (col == null) col = benchObj.AddComponent<BoxCollider>();
        col.isTrigger = false;
    }

    public Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        Vector3 originOffset = new Vector3(-currentWidth * 0.5f * tileSize, 0, -currentHeight * 0.5f * tileSize);
        float localX = (worldPos.x - originOffset.x) / tileSize;
        float localZ = (worldPos.z - originOffset.z) / tileSize;

        int gridX = Mathf.FloorToInt(localX);
        int gridZ = Mathf.FloorToInt(localZ);

        return new Vector2Int(gridX / chunkSize, gridZ / chunkSize);
    }

    public Vector3 GridToWorldPosition(int gridX, int gridY, Vector3 originOffset)
    {
        return new Vector3(gridX * tileSize + originOffset.x, 0, gridY * tileSize + originOffset.z);
    }

    public GroundTile GetTileAt(Vector2Int coord)
    {
        Vector2Int chunkCoord = new Vector2Int(coord.x / chunkSize, coord.y / chunkSize);
        if (chunks.TryGetValue(chunkCoord, out var chunk) && chunk.isLoaded)
        {
            chunk.tiles.TryGetValue(coord, out var tile);
            return tile;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (gridData == null) return;

        Vector3 originOffset = new Vector3(-currentWidth * 0.5f * tileSize, 0, -currentHeight * 0.5f * tileSize);
        Vector3 baseWorldPos = GridToWorldPosition(playerBaseCenter.x, playerBaseCenter.y, originOffset);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(baseWorldPos + Vector3.up * 0.5f, baseRadius * tileSize);

        foreach (var node in districtNodes)
        {
            Vector3 nodeWorldPos = GridToWorldPosition(Mathf.RoundToInt(node.position.x), Mathf.RoundToInt(node.position.y), originOffset);
            Gizmos.color = node.type switch
            {
                GroundType.City => Color.gray,
                GroundType.Factory => Color.red,
                GroundType.Farmland => Color.green,
                _ => Color.yellow
            };
            Gizmos.DrawWireSphere(nodeWorldPos + Vector3.up * 1f, 8f * tileSize);
        }
    }
}
