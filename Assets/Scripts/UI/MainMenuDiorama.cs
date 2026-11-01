using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eko-Mucit Ana Menü Prosedürel Mini-Dünya Dioraması.
/// - Menünün arkasında yüzen, masalsı, voxel tabanlı bir ada dioraması (Mini Floating Island).
/// - Sağlam ve estetik bir ada kaya tabanı (Floating Island Cliff Foundation) üzerinde yükselir.
/// - Modeller doğrudan oyunun kurallarına göre (ProceduralTerrainGenerator) orantılı ölçeklendirilir
///   (Fabrika bacası devasa olmaz, zemin karoları görünür, taban hizalamaları Y=0'a kusursuz oturur).
/// - BÖLGELER:
///   1. Nehir & Köprü Hattı: Nehir yatağı, köprü rampaları ve tabliyesi, dalgalanan duba (Buoy).
///   2. Çiftlik Bölgesi: Açık yeşil çim, samanlık avlusu, traktör, sürülmüş tarla (Rotation=0) ve mısırlar.
///   3. Şehir & Temiz Enerji: Şehir karoları, taş yol, küçük ev, ağaç, dönen rüzgar türbini ve güneş panelleri.
///   4. Sanayi & Fabrika: Fabrika zemini, fabrika binası, orantılı baca (FactoryChimney), filtre ve tatlı duman.
///   5. Organik Çöp Kümeleri: Lastik, elektronik atık, hurda metal, nehir kenarında şişeler.
///   6. Kahraman: Elinde çöp toplama maşasıyla nehir kenarında çevre nöbetçisi.
/// </summary>
public class MainMenuDiorama : MonoBehaviour
{
    [Header("--- DİORAMA DİNAMİKLERİ ---")]
    [Tooltip("Diorama adasının dönüş hızı (derece/saniye)")]
    [SerializeField] private float rotationSpeed = 6.0f;

    [Tooltip("Hafif su/hava süzülme yaylanması açık mı?")]
    [SerializeField] private bool enableBobbing = true;

    [Tooltip("Süzülme yaylanma hızı")]
    [SerializeField] private float bobSpeed = 1.0f;

    [Tooltip("Süzülme yaylanma genliği")]
    [SerializeField] private float bobAmount = 0.08f;

    private Transform islandRoot;
    private Transform turbineBladesTr;
    private Transform riverBuoyTr;
    private Vector3 initialIslandPos;

    // Projedeki saf .vox varlıklarının GUID haritası
    private static readonly Dictionary<string, string> VoxRegistry = new Dictionary<string, string>
    {
        { "GrassTile", "b164182d833aac042982b37219c7fc92" },       // GrassBaseTile.vox
        { "HayTile", "55fbbc4b00bc75145a5f32111c12522f" },         // Hay Ground tILE.vox
        { "FarmSoilTile", "7c3bdaaa8c1cb0b4097fd527f2d8bc1d" },    // FarmTileTilledSOIL.vox
        { "CityTile", "f7523c4893c1cec4584dabbddb7d3577" },        // CityTile.vox
        { "RoadTile", "fa0915f7925e23448a27e73644160645" },        // RoadMiddle.vox
        { "FactoryTile", "6e0760b08c585a14f8704bcc85d103aa" },     // FactoryBaseTile.vox
        { "BridgeRamp", "a62c909adf3b1b347b6c9699cdb2b09a" },      // Bridge ramp.vox
        { "BridgeTop", "8013b5ed7e83fc64da9cb5530cde66d4" },       // BridgeTop.vox
        { "Buoy", "7c4eaa33ba768f14aae4347f0ec1f9d3" },            // Buoy.vox
        { "FactoryBuilding", "3e00ac6fd9e2d944280b1e692fa709db" }, // FactoryBaseBuilding.vox
        { "FactoryChimney", "17db9620bd3da2f4bb245f603ec4c273" },  // FactoryChimney.vox
        { "AirFilter", "e53b0c2ece00a9245a9eb9a68f3b5127" },       // AirFilter.vox
        { "SmallHouse", "7ca0d442736fcc647b49ccb9bc96ddca" },      // SmallHouse.vox
        { "WindTurbine", "d684ac4bcbf9fe54094da9489bb0e94e" },     // WindTurbine.vox
        { "SolarPanel", "162aaffdc2fd22f4d854ce3583348980" },      // SolarPanel.vox
        { "Tree", "d0f9a1ea1e797e045953f9641aedfa82" },            // Tree.vox
        { "Character", "919a66fe4786fbc45a0f3eabe27c6cae" },       // SimpleCharacter.vox
        { "Stick", "87e4406918fcc534a8403ed8b8432193" },           // trashcollectionstick.vox
        { "Tire", "b037059c1c5788746aa82e56c93097c2" },            // Tire.vox
        { "ElectronicWaste", "c60204c6c5a6d2e488d97dd4f812a46e" }, // Electronicwaste.vox
        { "Bottler", "efc18f608aa05d64e9cc5a4bb3c59d4e" },         // Bottler.vox
        { "MetalScrap", "d635c08a879284f4aa3d1ccaadb0eac6" },      // SmallMetalScrap.vox
        { "Tractor", "ed5c653e5d034364a11df119126b0257" },         // farm-60-tractor.vox
        { "Corn", "b617d8fd10ae4b529ef43dead79ce672" }             // farm-32-corn_growth4.vox
    };

    private void Awake()
    {
        // Otomatik üretim kaldırıldı. Eğer kullanıcı bu scripti kendi diorama köküne eklediyse transform'u islandRoot olarak kullanır.
        if (islandRoot == null)
        {
            islandRoot = transform;
        }
    }

    private void Start()
    {
        if (islandRoot != null)
        {
            initialIslandPos = islandRoot.localPosition;
        }
    }

    private void Update()
    {
        if (islandRoot == null) return;

        // 1. Adanın yavaşça dönmesi
        islandRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. Hafif süzülme (bobbing)
        if (enableBobbing)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            islandRoot.localPosition = initialIslandPos + new Vector3(0f, bob, 0f);
        }

        // 3. Rüzgar türbini kanatlarının dönüşü
        if (turbineBladesTr != null)
        {
            turbineBladesTr.Rotate(Vector3.forward, 75f * Time.deltaTime, Space.Self);
        }

        // 4. Duba dalgalanması
        if (riverBuoyTr != null)
        {
            float bBob = Mathf.Sin(Time.time * 2.5f) * 0.03f;
            riverBuoyTr.localPosition = new Vector3(riverBuoyTr.localPosition.x, 0.05f + bBob, riverBuoyTr.localPosition.z);
            riverBuoyTr.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2f) * 4f, 0f, Mathf.Cos(Time.time * 1.8f) * 4f);
        }
    }

    private void SetupCameraAndLighting()
    {
        Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.32f, 0.46f, 1f); // Şık gökyüzü mavisi
            cam.fieldOfView = 50f;
            // 5x5 adayı butonların çevresinde harika bir derinlikle kadraja alan açı
            cam.transform.position = new Vector3(0f, 6.2f, -12.5f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
        }

        Light sun = FindFirstObjectByType<Light>();
        if (sun == null)
        {
            GameObject sunObj = new GameObject("DioramaSunLight");
            sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.color = new Color(1.0f, 0.96f, 0.88f, 1f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(46f, -35f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.52f, 0.62f, 1f);
    }

    [ContextMenu("Mini Dünyayı Yeniden Kur")]
    public void GenerateMiniWorldDiorama()
    {
        var old = transform.Find("MiniWorldIslandRoot");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        GameObject rootObj = new GameObject("MiniWorldIslandRoot");
        rootObj.transform.SetParent(transform, false);
        rootObj.transform.position = new Vector3(0f, -0.9f, 0f);
        rootObj.transform.localScale = Vector3.one;
        islandRoot = rootObj.transform;

        // Varlıkları yükle
        GameObject grassTile = LoadVox("GrassTile");
        GameObject hayTile = LoadVox("HayTile");
        GameObject farmSoilTile = LoadVox("FarmSoilTile");
        GameObject cityTile = LoadVox("CityTile");
        GameObject roadTile = LoadVox("RoadTile");
        GameObject factoryTile = LoadVox("FactoryTile");
        GameObject bridgeRamp = LoadVox("BridgeRamp");
        GameObject bridgeTop = LoadVox("BridgeTop");
        GameObject buoy = LoadVox("Buoy");
        GameObject factoryBuilding = LoadVox("FactoryBuilding");
        GameObject chimney = LoadVox("FactoryChimney");
        GameObject airFilter = LoadVox("AirFilter");
        GameObject smallHouse = LoadVox("SmallHouse");
        GameObject turbine = LoadVox("WindTurbine");
        GameObject solarPanel = LoadVox("SolarPanel");
        GameObject tree = LoadVox("Tree");
        GameObject hero = LoadVox("Character");
        GameObject stick = LoadVox("Stick");
        GameObject tire = LoadVox("Tire");
        GameObject eWaste = LoadVox("ElectronicWaste");
        GameObject bottle = LoadVox("Bottler");
        GameObject scrap = LoadVox("MetalScrap");
        GameObject tractor = LoadVox("Tractor");
        GameObject corn = LoadVox("Corn");

        float tileSize = 2.0f; // Voxel tile standart grid boyutu (2m)

        // =========================================================================
        // 0. SAĞLAM VE ESTETİK YÜZEN ADA TABANI (Floating Rock / Earth Foundation)
        // Karoların havada boşlukta kalmasını önleyen alt kaya kütlesi
        // =========================================================================
        CreateFloatingIslandBaseFoundation(tileSize);

        // =========================================================================
        // 1. ZEMİN GENERASYONU (5x5 Mini Ada: X[-2..+2], Z[-2..+2])
        // =========================================================================
        for (int gx = -2; gx <= 2; gx++)
        {
            for (int gz = -2; gz <= 2; gz++)
            {
                // Dairesel yumuşak ada hissi: 4 köşeyi boş bırak
                if (Mathf.Abs(gx) == 2 && Mathf.Abs(gz) == 2) continue;

                Vector3 tilePos = new Vector3(gx * tileSize, 0f, gz * tileSize);

                // A) Ortadan Geçen Nehir (gx == 0)
                if (gx == 0)
                {
                    GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    water.transform.SetParent(islandRoot, false);
                    water.transform.localPosition = tilePos + new Vector3(0f, -0.15f, 0f);
                    water.transform.localScale = new Vector3(tileSize * 0.99f, 0.3f, tileSize * 0.99f);
                    var rend = water.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        Material wMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        wMat.color = new Color(0.16f, 0.65f, 0.88f, 0.92f);
                        rend.material = wMat;
                    }
                    StripColliders(water);
                    continue;
                }

                // Zemin Karosu Seçimi
                GameObject tilePrefab;
                Quaternion tileRot = Quaternion.identity;

                // B) Fabrika Bölgesi (gx >= 1) -> FactoryBaseTile
                if (gx >= 1)
                {
                    tilePrefab = factoryTile;
                }
                // C) Çiftlik Bölgesi (gx <= -1 && gz >= 1)
                else if (gx <= -1 && gz >= 1)
                {
                    // Sürülmüş Tarla Alanı (gx == -1, gz == 2): KESİNLİKLE ROTATION = 0!
                    if (gx == -1 && gz == 2)
                    {
                        tilePrefab = farmSoilTile;
                        tileRot = Quaternion.identity; // Rotation = 0 kuralı!
                    }
                    // Samanlık Alanı (gx == -2, gz == 1): Hay Ground
                    else if (gx == -2 && gz == 1)
                    {
                        tilePrefab = hayTile;
                    }
                    else
                    {
                        tilePrefab = grassTile;
                    }
                }
                // D) Şehir & Yol Bölgesi (gx <= -1 && gz <= 0)
                else
                {
                    if (gx == -1 && gz == 0)
                    {
                        tilePrefab = roadTile;
                    }
                    else if (gx == -2 && gz == 0)
                    {
                        tilePrefab = cityTile;
                    }
                    else
                    {
                        tilePrefab = grassTile;
                    }
                }

                SpawnGroundTile(tilePrefab, tilePos, tileRot, tileSize);
            }
        }

        // =========================================================================
        // 2. NEHİR & KÖPRÜ GENERASYONU (Kıyılarda rampalar, ortada 1 kot yüksek tabliye)
        // =========================================================================
        SpawnDioramaProp(bridgeRamp, new Vector3(-1.0f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 2.0f);
        SpawnDioramaProp(bridgeRamp, new Vector3(1.0f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 2.0f);
        SpawnDioramaProp(bridgeTop, new Vector3(0f, 0.35f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one * 2.0f);

        // Nehirde Yüzen Temizleme Dubası (Buoy)
        GameObject buoyObj = SpawnDioramaProp(buoy, new Vector3(0f, -0.1f, -2.5f), Quaternion.identity, Vector3.one * 1.3f);
        if (buoyObj != null) riverBuoyTr = buoyObj.transform;

        // =========================================================================
        // 3. ÇİFTLİK BÖLGESİ YAPILARI (Traktör, Sıralı Mısırlar, Ağaç)
        // =========================================================================
        // Samanlı bölgede traktör (farm-60-tractor.vox)
        SpawnDioramaProp(tractor, new Vector3(-3.8f, 0f, 2.0f), Quaternion.Euler(0f, 40f, 0f), Vector3.one * 2.4f);

        // Sürülmüş Tarlada Sıralı Mısırlar (farm-32-corn_growth4.vox)
        if (corn != null)
        {
            SpawnDioramaProp(corn, new Vector3(-2.4f, 0f, 3.6f), Quaternion.identity, Vector3.one * 1.6f);
            SpawnDioramaProp(corn, new Vector3(-2.4f, 0f, 4.3f), Quaternion.identity, Vector3.one * 1.6f);
            SpawnDioramaProp(corn, new Vector3(-1.6f, 0f, 3.6f), Quaternion.identity, Vector3.one * 1.6f);
            SpawnDioramaProp(corn, new Vector3(-1.6f, 0f, 4.3f), Quaternion.identity, Vector3.one * 1.6f);
        }

        // Çiftlik Ağacı
        SpawnDioramaProp(tree, new Vector3(-4.0f, 0f, 3.8f), Quaternion.identity, Vector3.one * 2.2f);

        // =========================================================================
        // 4. ŞEHİR & YENİLENEBİLİR ENERJİ (Türbin, Paneller, Ev)
        // =========================================================================
        // Rüzgar Türbini (Dönen Kanatlar)
        GameObject turbObj = SpawnDioramaProp(turbine, new Vector3(-4.1f, 0f, -0.3f), Quaternion.Euler(0f, -30f, 0f), Vector3.one * 1.8f);
        if (turbObj != null)
        {
            Transform blades = turbObj.transform.Find("blades") ?? turbObj.transform.Find("Blade") ?? turbObj.transform;
            if (blades == turbObj.transform && turbObj.transform.childCount > 0)
            {
                blades = turbObj.transform.GetChild(0);
            }
            turbineBladesTr = blades;
        }

        // Şehir Evi
        SpawnDioramaProp(smallHouse, new Vector3(-3.8f, 0f, -2.3f), Quaternion.Euler(0f, 25f, 0f), Vector3.one * 2.6f);

        // Güneş Enerji Panelleri
        SpawnDioramaProp(solarPanel, new Vector3(-1.8f, 0f, -3.8f), Quaternion.Euler(0f, 25f, 0f), Vector3.one * 1.5f);
        SpawnDioramaProp(solarPanel, new Vector3(-2.6f, 0f, -3.8f), Quaternion.Euler(0f, 25f, 0f), Vector3.one * 1.5f);

        // =========================================================================
        // 5. FABRİKA BÖLGESİ (Ana Fabrika, Baca, Filtre ve Tatlı Minik Duman)
        // =========================================================================
        // Fabrika Binası
        SpawnDioramaProp(factoryBuilding, new Vector3(3.2f, 0f, -1.0f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 2.2f);

        // Fabrika Bacası (Orantılı 2.2x ölçek, ekranı kaplamayan estetik duruş)
        GameObject chimObj = SpawnDioramaProp(chimney, new Vector3(2.6f, 0f, 2.6f), Quaternion.identity, Vector3.one * 2.2f);
        if (chimObj != null)
        {
            // Bacanın üstüne filtre
            if (airFilter != null)
            {
                GameObject f = Instantiate(airFilter, chimObj.transform);
                f.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                f.transform.localScale = Vector3.one * 1.15f;
                NormalizeLocalPivotBottom(f);
                StripColliders(f);
            }

            // KONTROLLÜ, TATLI BACA DUMANI: Bacanın ağzında tütüp dağılır
            AttachDelicateChimneySmoke(chimObj);
        }

        // =========================================================================
        // 6. ORGANİK ÇÖP KÜMELERİ (Lastik, Elektronik Atık, Hurda Metal, Şişeler)
        // =========================================================================
        SpawnDioramaProp(tire, new Vector3(2.3f, 0f, 0.8f), Quaternion.Euler(0f, 30f, 0f), Vector3.one * 1.2f);
        SpawnDioramaProp(tire, new Vector3(2.8f, 0f, 1.2f), Quaternion.Euler(0f, -40f, 0f), Vector3.one * 1.0f);
        SpawnDioramaProp(eWaste, new Vector3(2.1f, 0f, 1.4f), Quaternion.Euler(0f, 75f, 0f), Vector3.one * 1.2f);
        SpawnDioramaProp(scrap, new Vector3(3.1f, 0f, 0.5f), Quaternion.Euler(0f, 15f, 0f), Vector3.one * 1.2f);

        // =========================================================================
        // 7. KAHRAMAN VE ÇÖP TOPLAMA ÇUBUĞU
        // =========================================================================
        GameObject playerObj = SpawnDioramaProp(hero, new Vector3(-1.2f, 0f, -1.2f), Quaternion.Euler(0f, 40f, 0f), Vector3.one * 1.1f);
        if (playerObj != null && stick != null)
        {
            GameObject st = Instantiate(stick, playerObj.transform);
            st.transform.localPosition = new Vector3(-0.35f, 0.65f, 0.18f);
            st.transform.localRotation = Quaternion.Euler(45f, 20f, -10f);
            st.transform.localScale = Vector3.one * 1.5f;
            StripColliders(st);
        }

        // Kıyıdaki Şişe Kümesi
        SpawnDioramaProp(bottle, new Vector3(-0.85f, 0f, -0.8f), Quaternion.Euler(0f, 20f, 0f), Vector3.one * 1.0f);
        SpawnDioramaProp(bottle, new Vector3(-0.65f, 0f, -1.0f), Quaternion.Euler(0f, -35f, 0f), Vector3.one * 1.0f);
    }

    /// <summary>
    /// Karoların altına oturan şık yüzen ada kaya/toprak tabanı oluşturur.
    /// </summary>
    private void CreateFloatingIslandBaseFoundation(float tileSize)
    {
        GameObject baseFoundation = new GameObject("IslandRockFoundation");
        baseFoundation.transform.SetParent(islandRoot, false);
        baseFoundation.transform.localPosition = Vector3.zero;

        Material rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        rockMat.color = new Color(0.26f, 0.22f, 0.18f, 1f); // Doğal toprak/kaya rengi

        // 1. Ana Üst Taban Plakası (Karoların hemen altı)
        GameObject topSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topSlab.transform.SetParent(baseFoundation.transform, false);
        topSlab.transform.localPosition = new Vector3(0f, -0.42f, 0f);
        topSlab.transform.localScale = new Vector3(10.2f, 0.75f, 10.2f);
        var r1 = topSlab.GetComponent<Renderer>();
        if (r1 != null) r1.material = rockMat;
        StripColliders(topSlab);

        // 2. Orta Kaya Kütlesi
        GameObject midSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        midSlab.transform.SetParent(baseFoundation.transform, false);
        midSlab.transform.localPosition = new Vector3(0f, -1.05f, 0f);
        midSlab.transform.localScale = new Vector3(8.4f, 0.70f, 8.4f);
        var r2 = midSlab.GetComponent<Renderer>();
        if (r2 != null) r2.material = rockMat;
        StripColliders(midSlab);

        // 3. Alt Sivrilen Uç (Floating Island Voxel Cone/Keystone)
        GameObject botSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        botSlab.transform.SetParent(baseFoundation.transform, false);
        botSlab.transform.localPosition = new Vector3(0f, -1.65f, 0f);
        botSlab.transform.localScale = new Vector3(5.6f, 0.65f, 5.6f);
        var r3 = botSlab.GetComponent<Renderer>();
        if (r3 != null) r3.material = rockMat;
        StripColliders(botSlab);

        // 4. En Alt Sivri Kaya
        GameObject tipSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tipSlab.transform.SetParent(baseFoundation.transform, false);
        tipSlab.transform.localPosition = new Vector3(0f, -2.15f, 0f);
        tipSlab.transform.localScale = new Vector3(2.8f, 0.50f, 2.8f);
        var r4 = tipSlab.GetComponent<Renderer>();
        if (r4 != null) r4.material = rockMat;
        StripColliders(tipSlab);
    }

    /// <summary>
    /// Zemin karolarını ProceduralTerrainGenerator ile %100 uyumlu (2m x 2m) ölçek ve sıfırlanmış yüzey kotu ile yerleştirir.
    /// </summary>
    private GameObject SpawnGroundTile(GameObject prefab, Vector3 localPos, Quaternion localRot, float tileSize)
    {
        if (prefab == null) return null;

        GameObject tile = Instantiate(prefab, islandRoot);
        tile.transform.localPosition = localPos;
        tile.transform.localRotation = localRot;
        tile.transform.localScale = Vector3.one * (tileSize / 1.0f); // 2.0x ölçek (Grid tam kapanır)

        // ProceduralTerrainGenerator kuralı: tile mesh'inin üst yüzeyini Y=0 hizasına oturt
        float localTopY = 0f;
        MeshFilter mf = tile.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            float childLocalY = (mf.transform != tile.transform) ? mf.transform.localPosition.y : 0f;
            localTopY = childLocalY + mf.sharedMesh.bounds.max.y;
        }

        float worldTopShift = localTopY * tile.transform.localScale.y;
        tile.transform.localPosition -= new Vector3(0f, worldTopShift, 0f);

        StripColliders(tile);
        return tile;
    }

    /// <summary>
    /// Binalar, nesneler ve dekorasyonları verilen ölçek çarpanıyla üretip tabanını zemin yüzeyine (Y=0) oturtur.
    /// </summary>
    private GameObject SpawnDioramaProp(GameObject prefab, Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        if (prefab == null) return null;

        GameObject obj = Instantiate(prefab, islandRoot);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = localRot;
        obj.transform.localScale = localScale;

        NormalizeLocalPivotBottom(obj);
        StripColliders(obj);
        return obj;
    }

    /// <summary>
    /// Nesnenin en alt tabanını (min Y) tam olarak localPos.y kotuna hizalar.
    /// </summary>
    private void NormalizeLocalPivotBottom(GameObject obj)
    {
        if (obj == null) return;

        if (TryGetCombinedLocalBounds(obj, out Bounds localBounds))
        {
            float minY = localBounds.min.y;
            if (Mathf.Abs(minY) > 0.005f)
            {
                obj.transform.localPosition -= new Vector3(0f, minY * obj.transform.localScale.y, 0f);
            }
        }
    }

    private bool TryGetCombinedLocalBounds(GameObject obj, out Bounds combinedBounds)
    {
        combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        foreach (var mf in obj.GetComponentsInChildren<MeshFilter>(true))
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
    /// Sadece bacanın ağzında tütüp yavaşça dağılan narin duman partikülleri.
    /// </summary>
    private void AttachDelicateChimneySmoke(GameObject chimneyObj)
    {
        if (chimneyObj == null) return;

        Renderer[] rends = chimneyObj.GetComponentsInChildren<Renderer>();
        Vector3 topPos = chimneyObj.transform.position;
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            topPos = new Vector3(b.center.x, b.max.y, b.center.z);
        }

        GameObject smokeObj = new GameObject("ChimneyDelicateSmoke");
        smokeObj.transform.SetParent(chimneyObj.transform, true);
        smokeObj.transform.position = topPos;
        smokeObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem ps = smokeObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 3f;
        main.startLifetime = 2.2f;
        main.startSpeed = 0.8f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.40f);
        main.startColor = new Color(0.25f, 0.25f, 0.27f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 18;

        var emission = ps.emission;
        emission.rateOverTime = 4.0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.08f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.7f);
        sizeCurve.AddKey(0.4f, 1.0f);
        sizeCurve.AddKey(1.0f, 1.4f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.25f, 0.25f, 0.27f), 0f),
                new GradientColorKey(new Color(0.45f, 0.45f, 0.48f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.45f, 0f),
                new GradientAlphaKey(0.30f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var rend = smokeObj.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            Material smokeMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                                             Shader.Find("Particles/Standard Unlit") ??
                                             Shader.Find("Mobile/Particles/Alpha Blended"));
            rend.material = smokeMat;
        }

        ps.Play();
    }

    private void StripColliders(GameObject obj)
    {
        if (obj == null) return;
        foreach (var c in obj.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
    }

    private GameObject LoadVox(string key)
    {
#if UNITY_EDITOR
        if (VoxRegistry.TryGetValue(key, out string guid))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
#endif
        return null;
    }
}
