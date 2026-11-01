using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Unity Editöründe tek tıkla hem Oyuncunun (untitled, İleri, Geri) hem de
/// Kadın (FemNPC1) ve Erkek (MaleNPC) takipçilerin 3'er karelik yürüme ve duruş döngülerini kuran,
/// yeni zemin modellerini harita üreticisine bağlayan ve rotasyon/dönme sorunlarını çözen araç.
/// </summary>
[InitializeOnLoad]
public static class PlayerVoxelAnimatorSetupEditor
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string FemNPCPrefabPath = "Assets/Prefabs/NPC_Female_Follower.prefab";
    private const string MaleNPCPrefabPath = "Assets/Prefabs/NPC_Male_Follower.prefab";
    private const string ChildNPCPrefabPath = "Assets/Prefabs/NPC_Child_Citizen.prefab";

    static PlayerVoxelAnimatorSetupEditor()
    {
        EditorApplication.delayCall += AutoCheckAndSetup;
    }

    private static void AutoCheckAndSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SetupAllVoxelAssets();
    }

    [MenuItem("Tools/Eko-Mucit/Setup All Voxel Characters and Tiles", false, 15)]
    public static void SetupAllVoxelAssets()
    {
        // 1. Oyuncu Voxel Animasyonlarını Kur
        SetupPlayerCharacter();

        // 2. Kadın ve Erkek NPC Prefablarını Kur
        SetupNPCPrefabs();

        // 3. Yeni Zemin Modellerini (CityTile, FactoryBaseTile, ConnectionAreaBaseTile) Haritaya Bağla
        SetupTerrainTileAssets();

        // 4. Yeni Ses Kliplerini (Background_Loop, Button, Collect, Craft, Jump, Success) Bağla
        SetupAudioClips();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Eko-Mucit/Reimport All Vox Models (With Collider)", false, 20)]
    public static void ReimportAllVoxModels()
    {
        string[] allGuids = AssetDatabase.FindAssets("t:GameObject");
        foreach (var guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".vox", System.StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
        Debug.Log("[Eko-Mucit] Tüm .vox modelleri MeshCollider ile yeniden import edildi!");
    }

    private static void SetupPlayerCharacter()
    {
        GameObject idleAsset = FindVoxAsset("untitled");
        GameObject ileriAsset = FindVoxAsset("leri");
        GameObject geriAsset = FindVoxAsset("geri");
        GameObject vacuumAsset = FindVoxAsset("Vacuum");
        GameObject stickAsset = FindVoxAsset("trashcollectionstick");

        if (idleAsset == null || ileriAsset == null || geriAsset == null)
        {
            Debug.LogWarning("[Eko-Mucit] Oyuncu .vox modelleri eksik!");
            return;
        }

        // Sahnedeki Player nesnesinde modelleri ve çöp çubuğu / vakum ayarlarını eksiksiz kur
        var cc = Object.FindFirstObjectByType<CharacterController25D>();
        if (cc != null)
        {
            ApplyVoxelSetupToObject(cc.gameObject, idleAsset, ileriAsset, geriAsset, vacuumAsset, stickAsset, isPrefab: false);
            EditorSceneManager.MarkSceneDirty(cc.gameObject.scene);
            EditorSceneManager.SaveScene(cc.gameObject.scene);
        }

        // Player.prefab dosyasında modelleri ve çöp çubuğu / vakum ayarlarını güncelle
        if (File.Exists(PlayerPrefabPath))
        {
            try
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
                if (prefabRoot != null)
                {
                    ApplyVoxelSetupToObject(prefabRoot, idleAsset, ileriAsset, geriAsset, vacuumAsset, stickAsset, isPrefab: true);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Eko-Mucit] Player.prefab güncellenirken hata: {ex.Message}");
            }
        }
    }

    private static void SetupNPCPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // Kadın NPC Modelleri: FemNPC1Idle, FemNPC1first, FemNPC1second
        GameObject femIdle = FindVoxAsset("FemNPC1Idle");
        GameObject femFirst = FindVoxAsset("FemNPC1first");
        GameObject femSecond = FindVoxAsset("FemNPC1second");

        // Erkek NPC Modelleri: MaleNPCIdle, MaleNPCFirst, MaleNPCSecond
        GameObject maleIdle = FindVoxAsset("MaleNPCIdle");
        GameObject maleFirst = FindVoxAsset("MaleNPCFirst");
        GameObject maleSecond = FindVoxAsset("MaleNPCSecond");

        // Çocuk NPC Modeli: Child
        GameObject childModel = FindVoxAsset("Child");

        GameObject femPrefab = null;
        GameObject malePrefab = null;
        GameObject childPrefab = null;

        if (femIdle != null && femFirst != null && femSecond != null)
        {
            femPrefab = CreateOrUpdateNPCPrefab(FemNPCPrefabPath, "Gönüllü Aylin", femIdle, femFirst, femSecond);
        }

        if (maleIdle != null && maleFirst != null && maleSecond != null)
        {
            malePrefab = CreateOrUpdateNPCPrefab(MaleNPCPrefabPath, "Gönüllü Barış", maleIdle, maleFirst, maleSecond);
        }

        if (childModel != null)
        {
            childPrefab = CreateOrUpdateNPCPrefab(ChildNPCPrefabPath, "Küçük Ege", childModel, childModel, childModel);
        }

        // CityRecruitmentHub nesnelerine ve prefabına bağla
        var hubs = Object.FindObjectsByType<CityRecruitmentHub>(FindObjectsSortMode.None);
        foreach (var hub in hubs)
        {
            SerializedObject so = new SerializedObject(hub);
            var fProp = so.FindProperty("femaleFollowerPrefab");
            var mProp = so.FindProperty("maleFollowerPrefab");
            var defProp = so.FindProperty("followerPrefab");

            if (fProp != null && femPrefab != null) fProp.objectReferenceValue = femPrefab;
            if (mProp != null && malePrefab != null) mProp.objectReferenceValue = malePrefab;
            if (defProp != null && (femPrefab != null || malePrefab != null))
                defProp.objectReferenceValue = femPrefab != null ? femPrefab : malePrefab;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hub);
            EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
        }

        // CityRecruitmentHub Prefab'ı varsa onu da güncelle
        string hubPrefabPath = "Assets/Prefabs/Structures/CityRecruitmentHub.prefab";
        if (File.Exists(hubPrefabPath))
        {
            try
            {
                GameObject hubRoot = PrefabUtility.LoadPrefabContents(hubPrefabPath);
                var hubComp = hubRoot.GetComponent<CityRecruitmentHub>();
                if (hubComp != null)
                {
                    SerializedObject so = new SerializedObject(hubComp);
                    var fProp = so.FindProperty("femaleFollowerPrefab");
                    var mProp = so.FindProperty("maleFollowerPrefab");
                    var defProp = so.FindProperty("followerPrefab");

                    if (fProp != null && femPrefab != null) fProp.objectReferenceValue = femPrefab;
                    if (mProp != null && malePrefab != null) mProp.objectReferenceValue = malePrefab;
                    if (defProp != null && (femPrefab != null || malePrefab != null))
                        defProp.objectReferenceValue = femPrefab != null ? femPrefab : malePrefab;

                    so.ApplyModifiedProperties();
                    PrefabUtility.SaveAsPrefabAsset(hubRoot, hubPrefabPath);
                }
                PrefabUtility.UnloadPrefabContents(hubRoot);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Eko-Mucit] Hub prefab güncellenirken hata: {ex.Message}");
            }
        }
    }

    private static GameObject CreateOrUpdateNPCPrefab(string path, string followerName, GameObject idle, GameObject step1, GameObject step2)
    {
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path));

        // 1. CapsuleCollider
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.radius = 0.45f;
        capsule.height = 1.8f;

        // 2. Rigidbody (Kinematic + FreezeRotation - kesinlikle fırıldak gibi dönmez ve zemine batmaz)
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // 3. FollowerNPC
        var follower = root.AddComponent<FollowerNPC>();
        SerializedObject followerSo = new SerializedObject(follower);
        var nameProp = followerSo.FindProperty("followerName");
        if (nameProp != null) nameProp.stringValue = followerName;
        followerSo.ApplyModifiedProperties();

        // 4. VoxelCharacterAnimator
        var anim = root.AddComponent<VoxelCharacterAnimator>();
        anim.idleModel = idle;
        anim.walkStep1Model = step1;
        anim.walkStep2Model = step2;

        SerializedObject animSo = new SerializedObject(anim);
        var rotProp = animSo.FindProperty("modelYRotationOffset");
        if (rotProp != null) rotProp.floatValue = 180f;
        var stepProp = animSo.FindProperty("stepInterval");
        if (stepProp != null) stepProp.floatValue = 0.28f;
        var bounceProp = animSo.FindProperty("bounceHeight");
        if (bounceProp != null) bounceProp.floatValue = 0.04f;
        var squashProp = animSo.FindProperty("squashAmount");
        if (squashProp != null) squashProp.floatValue = 0.02f;

        var stickOffsetProp = animSo.FindProperty("stickHandOffset");
        if (stickOffsetProp != null) stickOffsetProp.vector3Value = new Vector3(-0.55f, 0.72f, -0.04f);
        var stickRotProp = animSo.FindProperty("stickLocalRotation");
        if (stickRotProp != null) stickRotProp.vector3Value = new Vector3(48f, 15f, -12f);
        var stickScaleProp = animSo.FindProperty("stickScale");
        if (stickScaleProp != null) stickScaleProp.floatValue = 2.35f;

        var vacOffsetProp = animSo.FindProperty("vacuumHandOffset");
        if (vacOffsetProp != null) vacOffsetProp.vector3Value = new Vector3(-0.22f, 1.05f, 0.06f);
        var vacRotProp = animSo.FindProperty("vacuumLocalRotation");
        if (vacRotProp != null) vacRotProp.vector3Value = Vector3.zero;
        var alwaysVacProp = animSo.FindProperty("alwaysShowVacuumBackpack");
        if (alwaysVacProp != null) alwaysVacProp.boolValue = false;

        animSo.ApplyModifiedProperties();

        anim.InitializeModelInstances();
        anim.StripAllChildColliders();

        // Prefab olarak kaydet
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log($"[Eko-Mucit] NPC Prefab oluşturuldu: {path}");
        return savedPrefab;
    }

    public static void SetupTerrainTileAssets()
    {
        GameObject cityTile = FindVoxAsset("CityTile");
        GameObject factoryTile = FindVoxAsset("FactoryBaseTile");
        GameObject connectionTile = FindVoxAsset("ConnectionAreaBaseTile");
        GameObject grassTile = FindVoxAsset("GrassBaseTile");

        // Yeni Tarla ve Saman Karo Assetleri (.vox)
        GameObject farmTile = FindVoxAsset("FarmTileTilledSOIL");
        GameObject hayTile = FindVoxAsset("Hay Ground tILE");

        // Yeni Yol Prefabları
        GameObject roadMiddle = FindVoxAsset("RoadMiddle");
        GameObject roadSides = FindVoxAsset("RoadSides");
        GameObject roadBase = FindVoxAsset("RoadBase");
        GameObject roadRails = FindVoxAsset("RoadRails");
        GameObject gravelRoad = FindVoxAsset("GravelRoad");

        // Yeni Bina ve Çevre Prefabları
        GameObject largeBuilding1 = FindVoxAsset("LargeBuilding");
        GameObject largeBuilding2 = FindVoxAsset("LargeBuilding2");
        GameObject tree = FindVoxAsset("Tree");

        var terrainGen = Object.FindFirstObjectByType<ProceduralTerrainGenerator>();
        if (terrainGen != null)
        {
            SerializedObject so = new SerializedObject(terrainGen);
            bool changed = false;

            void AssignIfFound(string propName, GameObject asset)
            {
                if (asset != null)
                {
                    var p = so.FindProperty(propName);
                    if (p != null && p.objectReferenceValue != asset)
                    {
                        p.objectReferenceValue = asset;
                        changed = true;
                    }
                }
            }

            AssignIfFound("cityTilePrefab", cityTile);
            AssignIfFound("farmTilePrefab", farmTile);
            AssignIfFound("hayTilePrefab", hayTile);
            AssignIfFound("factoryTilePrefab", factoryTile);
            AssignIfFound("transitionTilePrefab", connectionTile);
            AssignIfFound("playerBaseTilePrefab", grassTile ?? connectionTile);

            AssignIfFound("roadMiddlePrefab", roadMiddle);
            AssignIfFound("roadSidesPrefab", roadSides);
            AssignIfFound("roadBasePrefab", roadBase);
            AssignIfFound("roadRailsPrefab", roadRails);
            AssignIfFound("gravelRoadPrefab", gravelRoad);

            AssignIfFound("largeBuilding1Prefab", largeBuilding1);
            AssignIfFound("largeBuilding2Prefab", largeBuilding2);
            AssignIfFound("treePrefab", tree);

            GameObject trashCan = FindVoxAsset("Voxel Trash Can T2");
            AssignIfFound("trashCanPrefab", trashCan);

            // Yeni eklenen tüm Şehir Evlerini ve Binalarını (House1, House2, SmallHouse1-3, TownHall, LargeBuilding1-2) bağla
            var cityBuildProp = so.FindProperty("cityBuildingPrefabs");
            if (cityBuildProp != null)
            {
                cityBuildProp.ClearArray();
                string[] cityBuildNames = new[] { "House1", "House2", "SmallHouse", "SmallHouse2", "SmallHouse3", "TownHall", "LargeBuilding", "LargeBuilding2" };
                foreach (var name in cityBuildNames)
                {
                    GameObject p = FindVoxAsset(name);
                    if (p != null)
                    {
                        cityBuildProp.InsertArrayElementAtIndex(cityBuildProp.arraySize);
                        cityBuildProp.GetArrayElementAtIndex(cityBuildProp.arraySize - 1).objectReferenceValue = p;
                        changed = true;
                    }
                }
            }

            // Fabrika Binalarını (RecyclingBuilding, LargeBuilding2) bağla
            var factoryBuildProp = so.FindProperty("factoryBuildingPrefabs");
            if (factoryBuildProp != null)
            {
                factoryBuildProp.ClearArray();
                string[] facBuildNames = new[] { "RecyclingBuilding", "LargeBuilding2" };
                foreach (var name in facBuildNames)
                {
                    GameObject p = FindVoxAsset(name);
                    if (p != null)
                    {
                        factoryBuildProp.InsertArrayElementAtIndex(factoryBuildProp.arraySize);
                        factoryBuildProp.GetArrayElementAtIndex(factoryBuildProp.arraySize - 1).objectReferenceValue = p;
                        changed = true;
                    }
                }
            }

            // Şehirde Gezen Vatandaş NPC Prefablarını (Kadın, Erkek, Çocuk) bağla
            var citizenProp = so.FindProperty("cityCitizenNPCPrefabs");
            if (citizenProp != null)
            {
                citizenProp.ClearArray();
                string[] npcPaths = new[] { FemNPCPrefabPath, MaleNPCPrefabPath, ChildNPCPrefabPath };
                foreach (var npcPath in npcPaths)
                {
                    GameObject npcPref = AssetDatabase.LoadAssetAtPath<GameObject>(npcPath);
                    if (npcPref != null)
                    {
                        citizenProp.InsertArrayElementAtIndex(citizenProp.arraySize);
                        citizenProp.GetArrayElementAtIndex(citizenProp.arraySize - 1).objectReferenceValue = npcPref;
                        changed = true;
                    }
                }
            }

            // XeehSets Farm Source Doğrudan Voxel (.vox) Çiftlik Yapılarını, Çatılarını, Hayvanlarını ve Ekinlerini Bağla
            AssignIfFound("barnRoofPrefab", FindVoxAsset("farm-20-barn_roof"));
            AssignIfFound("windmillRoofPrefab", FindVoxAsset("farm-0-windmill_roof"));
            AssignIfFound("windmillBladePrefab", FindVoxAsset("farm-1-windmill_blade"));

            // Yeni Landmark & Eko-Cihaz Modelleri (TownHall, RecyclingBuilding, FactoryChimney, AirFilter, Buoy)
            AssignIfFound("townHallVoxPrefab", FindVoxAsset("TownHall"));
            AssignIfFound("recyclingBuildingVoxPrefab", FindVoxAsset("RecyclingBuilding"));
            AssignIfFound("factoryChimneyVoxPrefab", FindVoxAsset("FactoryChimney"));
            AssignIfFound("airFilterVoxPrefab", FindVoxAsset("AirFilter"));
            AssignIfFound("buoyVoxPrefab", FindVoxAsset("Buoy"));

            // Yeni Köprü ve Rampa Modelleri (Bridge ramp, BridgeTop)
            AssignIfFound("bridgeRampPrefab", FindVoxAsset("Bridge ramp"));
            AssignIfFound("bridgeTopPrefab", FindVoxAsset("BridgeTop"));

            // Yeni Toplanabilir Atık Modelleri (Tire, Electronicwaste)
            AssignIfFound("tireWastePrefab", FindVoxAsset("Tire"));
            AssignIfFound("electronicWastePrefab", FindVoxAsset("Electronicwaste"));

            // Yeni Temiz Enerji Modelleri (SolarPanel, WindTurbine)
            AssignIfFound("solarPanelPrefab", FindVoxAsset("SolarPanel"));
            AssignIfFound("windTurbinePrefab", FindVoxAsset("WindTurbine"));

            void AssignStageArray(string propName, string[] stageNames)
            {
                var arrProp = so.FindProperty(propName);
                if (arrProp == null) return;
                arrProp.ClearArray();
                foreach (var sName in stageNames)
                {
                    GameObject asset = FindVoxAsset(sName);
                    if (asset != null)
                    {
                        arrProp.InsertArrayElementAtIndex(arrProp.arraySize);
                        arrProp.GetArrayElementAtIndex(arrProp.arraySize - 1).objectReferenceValue = asset;
                        changed = true;
                    }
                }
            }

            AssignStageArray("farmTractorPrefabs", new[] { "farm-60-tractor", "farm-59-tractor2" });
            AssignStageArray("cornStagePrefabs", new[] { "farm-35-corn_growth1", "farm-29-corn_growth2", "farm-31-corn_growth3", "farm-32-corn_growth4" });
            AssignStageArray("tomatoStagePrefabs", new[] { "farm-56-tomato_growth1", "farm-58-tomato_growth2", "farm-57-tomato_growth3", "farm-55-tomato_growth4" });
            AssignStageArray("carrotStagePrefabs", new[] { "farm-39-carrot_growth1", "farm-41-carrot_growth2", "farm-38-carrot_growth3", "farm-40-carrot_growth4" });
            AssignStageArray("beetStagePrefabs", new[] { "farm-11-beet_growth1", "farm-12-beet_growth2", "farm-13-beet_growth3", "farm-14-beet_growth4" });

            var farmStructProp = so.FindProperty("farmStructurePrefabs");
            if (farmStructProp != null)
            {
                farmStructProp.ClearArray();
                string[] structNames = new[] { "farm-21-barn", "farm-2-windmill", "farm-49-coop", "farm-60-tractor", "farm-59-tractor2" };
                foreach (var name in structNames)
                {
                    GameObject p = FindVoxAsset(name);
                    if (p != null)
                    {
                        farmStructProp.InsertArrayElementAtIndex(farmStructProp.arraySize);
                        farmStructProp.GetArrayElementAtIndex(farmStructProp.arraySize - 1).objectReferenceValue = p;
                        changed = true;
                    }
                }
            }

            var farmAnimalProp = so.FindProperty("farmAnimalPrefabs");
            if (farmAnimalProp != null)
            {
                farmAnimalProp.ClearArray();
                string[] animalNames = new[] { "farm-19-cow_br", "farm-23-cow_bw", "farm-25-sheep_white", "farm-24-sheep_black", "farm-45-chicken_yellow", "farm-46-chicken_white", "farm-16-feeder", "farm-47-watertrough_S", "farm-43-fence_mid" };
                foreach (var name in animalNames)
                {
                    GameObject p = FindVoxAsset(name);
                    if (p != null)
                    {
                        farmAnimalProp.InsertArrayElementAtIndex(farmAnimalProp.arraySize);
                        farmAnimalProp.GetArrayElementAtIndex(farmAnimalProp.arraySize - 1).objectReferenceValue = p;
                        changed = true;
                    }
                }
            }

            var farmCropProp = so.FindProperty("farmCropPrefabs");
            if (farmCropProp != null)
            {
                farmCropProp.ClearArray();
                string[] cropNames = new[] { "farm-32-corn_growth4", "farm-40-carrot_growth4", "farm-55-tomato_growth4", "farm-14-beet_growth4", "farm-33-cauliflower_growth4", "farm-42-turnip_growth4" };
                foreach (var name in cropNames)
                {
                    GameObject p = FindVoxAsset(name);
                    if (p != null)
                    {
                        farmCropProp.InsertArrayElementAtIndex(farmCropProp.arraySize);
                        farmCropProp.GetArrayElementAtIndex(farmCropProp.arraySize - 1).objectReferenceValue = p;
                        changed = true;
                    }
                }
            }

            // Sahnedeki gerçek Player nesnesini jeneratöre bağla
            var playerCc = Object.FindFirstObjectByType<CharacterController25D>();
            if (playerCc != null)
            {
                var ptProp = so.FindProperty("playerTransform");
                if (ptProp != null && ptProp.objectReferenceValue != playerCc.transform)
                {
                    ptProp.objectReferenceValue = playerCc.transform;
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(terrainGen);
                EditorSceneManager.MarkSceneDirty(terrainGen.gameObject.scene);
                EditorSceneManager.SaveScene(terrainGen.gameObject.scene);
                Debug.Log("[Eko-Mucit] Tüm Yol, Ray ve Büyük Bina modelleri ProceduralTerrainGenerator'a bağlandı!");
            }
        }
    }

    public static GameObject FindVoxAsset(string searchKey)
    {
        string[] allGuids = AssetDatabase.FindAssets("t:GameObject");
        string lowerKey = searchKey.ToLower();

        // 1. Önce tam dosya adı eşleşmesi ara (Örn: "SmallHouse" ararken "SmallHouse2" gelmesini önler!)
        foreach (var guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".vox", System.StringComparison.OrdinalIgnoreCase))
            {
                string filename = Path.GetFileNameWithoutExtension(path).ToLower();
                if (filename.Equals(lowerKey))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) return go;
                }
            }
        }

        // 2. Tam eşleşme bulunamazsa alt-dize (contains) eşleşmesi ara
        foreach (var guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".vox", System.StringComparison.OrdinalIgnoreCase))
            {
                string filename = Path.GetFileNameWithoutExtension(path).ToLower();
                if ((lowerKey == "leri" && (filename.Contains("leri") || filename.Contains("ileri"))) ||
                    filename.Contains(lowerKey))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) return go;
                }
            }
        }
        return null;
    }

    private static void SetupAudioClips()
    {
        var manager = Object.FindFirstObjectByType<GameAudioAndJuiceManager>();
        if (manager != null)
        {
            manager.LoadSoundAssetsFromDisk();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log("[Eko-Mucit] Yeni ses dosyaları (Background_Loop, Button, Collect, Craft, Jump, Success) bağlandı!");
        }
    }

    private static GameObject LoadPrefabByName(string prefabName)
    {
        string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab");
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p).Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
        }
        return null;
    }

    private static void ApplyVoxelSetupToObject(GameObject targetObj, GameObject idleAsset, GameObject ileriAsset, GameObject geriAsset, GameObject vacuumAsset, GameObject stickAsset, bool isPrefab)
    {
        Undo.RegisterFullObjectHierarchyUndo(targetObj, "Setup Player Voxel Animation");

        if (!isPrefab && PrefabUtility.IsPartOfAnyPrefab(targetObj))
        {
            PrefabUtility.UnpackPrefabInstance(targetObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        // Rigidbody ayarları: Karakterin fiziksel olarak kendi kendine dönmesini kesinlikle dondur
        Rigidbody rb = targetObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.angularVelocity = Vector3.zero;
        }

        // Kök nesnedeki ilkel Capsule MeshRenderer'ı kapat
        MeshRenderer rootRend = targetObj.GetComponent<MeshRenderer>();
        if (rootRend != null)
        {
            rootRend.enabled = false;
        }

        // VoxelCharacterAnimator bileşenini ekle / güncelle
        VoxelCharacterAnimator anim = targetObj.GetComponent<VoxelCharacterAnimator>();
        if (anim == null)
        {
            anim = targetObj.AddComponent<VoxelCharacterAnimator>();
        }

        anim.idleModel = idleAsset;
        anim.walkStep1Model = ileriAsset;
        anim.walkStep2Model = geriAsset;
        anim.vacuumToolModel = vacuumAsset;
        anim.trashStickModel = stickAsset;

        SerializedObject animSo = new SerializedObject(anim);
        var rotProp = animSo.FindProperty("modelYRotationOffset");
        if (rotProp != null) rotProp.floatValue = 180f;
        var stepProp = animSo.FindProperty("stepInterval");
        if (stepProp != null) stepProp.floatValue = 0.28f;
        var bounceProp = animSo.FindProperty("bounceHeight");
        if (bounceProp != null) bounceProp.floatValue = 0.04f;
        var squashProp = animSo.FindProperty("squashAmount");
        if (squashProp != null) squashProp.floatValue = 0.02f;

        var stickOffsetProp = animSo.FindProperty("stickHandOffset");
        if (stickOffsetProp != null) stickOffsetProp.vector3Value = new Vector3(-0.55f, 0.72f, -0.04f);
        var stickRotProp = animSo.FindProperty("stickLocalRotation");
        if (stickRotProp != null) stickRotProp.vector3Value = new Vector3(48f, 15f, -12f);
        var stickScaleProp = animSo.FindProperty("stickScale");
        if (stickScaleProp != null) stickScaleProp.floatValue = 2.35f;

        var vacOffsetProp = animSo.FindProperty("vacuumHandOffset");
        if (vacOffsetProp != null) vacOffsetProp.vector3Value = new Vector3(-0.22f, 1.05f, 0.06f);
        var vacRotProp = animSo.FindProperty("vacuumLocalRotation");
        if (vacRotProp != null) vacRotProp.vector3Value = Vector3.zero;
        var alwaysVacProp = animSo.FindProperty("alwaysShowVacuumBackpack");
        if (alwaysVacProp != null) alwaysVacProp.boolValue = false;

        animSo.ApplyModifiedProperties();

        // VisualRoot altındaki nesneleri yeniden hizala
        anim.InitializeModelInstances();
        anim.StripAllChildColliders();

        EditorUtility.SetDirty(targetObj);
    }
}
