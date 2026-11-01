using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Unity Editöründe tek tıkla Stardew + Factorio yapıları ve Eko-İstasyonlar için
/// gerçek .vox modellerini (Buoy.vox, FactoryChimney.vox, AirFilter.vox, Tree.vox) ve
/// composite prefabları üreten araç.
/// </summary>
[InitializeOnLoad]
public static class SampleStructuresBuilderEditor
{
    private const string TargetFolder = "Assets/Prefabs/Structures";

    static SampleStructuresBuilderEditor()
    {
        EditorApplication.delayCall += AutoCheckAndGenerate;
    }

    private static void AutoCheckAndGenerate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists($"{TargetFolder}/FactoryFilterChimney.prefab") ||
            !File.Exists($"{TargetFolder}/CityRecruitmentHub.prefab") ||
            !File.Exists($"{TargetFolder}/AutoVacuumTurret.prefab"))
        {
            GenerateSamplePrefabs();
        }
    }

    [MenuItem("Tools/Eko-Mucit/Create Sample Structure Prefabs", false, 12)]
    public static void GenerateSamplePrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Structures");
        }

        // 1. Temel Otomasyon ve Üs Yapıları
        CreateRecyclerPrefab();
        CreateShippingBinPrefab();
        CreateTreeSaplingPrefab();
        CreateConveyorBeltPrefab();
        CreateAutoVacuumTurretPrefab();
        CreateBridgePrefab();

        // 2. Bölgesel Eko-İstasyonlar ve Topluluk Merkezi (Gerçek .vox Modelleriyle)
        CreateChimneyPrefab();
        CreateCompostStationPrefab();
        CreateRiverBuoyPrefab();
        CreateCityHubPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Sahnede varsa bileşenlere otomatik bağla
        GameObject recyclerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/Recycler2x2.prefab");
        GameObject shippingBinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/ShippingBin.prefab");
        GameObject treeSaplingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/TreeSapling.prefab");
        GameObject conveyorBeltPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/ConveyorBelt.prefab");
        GameObject autoTurretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/AutoVacuumTurret.prefab");
        GameObject bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/WoodenBridge.prefab");

        GameObject chimneyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/FactoryFilterChimney.prefab");
        GameObject compostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/FarmlandCompostStation.prefab");
        GameObject buoyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/RiverBuoyStation.prefab");
        GameObject cityHubPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TargetFolder}/CityRecruitmentHub.prefab");

        var buildSystem = Object.FindFirstObjectByType<BuildingPlacementSystem>();
        if (buildSystem != null)
        {
            // BuildingPlacementSystem sırası: 0=Recycler, 1=ShippingBin, 2=ConveyorBelt, 3=TreeSapling, 4=AutoVacuumTurret
            buildSystem.SetRecipePrefab(0, recyclerPrefab);
            buildSystem.SetRecipePrefab(1, shippingBinPrefab);
            buildSystem.SetRecipePrefab(2, conveyorBeltPrefab);
            buildSystem.SetRecipePrefab(3, treeSaplingPrefab);
            buildSystem.SetRecipePrefab(4, autoTurretPrefab);
            EditorUtility.SetDirty(buildSystem);
        }

        var terrain = Object.FindFirstObjectByType<ProceduralTerrainGenerator>();
        if (terrain != null)
        {
            SerializedObject so = new SerializedObject(terrain);
            var bridgeProp = so.FindProperty("bridgePrefab");
            if (bridgeProp != null) bridgeProp.objectReferenceValue = bridgePrefab;

            var chimneyProp = so.FindProperty("chimneyPrefab");
            if (chimneyProp != null) chimneyProp.objectReferenceValue = chimneyPrefab;

            var compostProp = so.FindProperty("compostStationPrefab");
            if (compostProp != null) compostProp.objectReferenceValue = compostPrefab;

            var buoyProp = so.FindProperty("riverBuoyPrefab");
            if (buoyProp != null) buoyProp.objectReferenceValue = buoyPrefab;

            var cityProp = so.FindProperty("cityHubPrefab");
            if (cityProp != null) cityProp.objectReferenceValue = cityProp.objectReferenceValue != null ? cityProp.objectReferenceValue : cityHubPrefab;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(terrain);
        }

        Debug.Log("Eko-Mucit: Yapı, Otomasyon ve Eko-İstasyon prefabları (.vox modelleriyle) 'Assets/Prefabs/Structures' klasörüne oluşturuldu ve sahneye bağlandı!");
    }

    private static GameObject FindVoxModel(string assetName)
    {
        string directPath = $"Assets/ArtAssets/3d/{assetName}.vox";
        GameObject direct = AssetDatabase.LoadAssetAtPath<GameObject>(directPath);
        if (direct != null) return direct;

        string[] guids = AssetDatabase.FindAssets($"{assetName} t:GameObject");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".vox", System.StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
        return null;
    }

    private static void StripMeshColliders(GameObject root)
    {
        foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc != null) Object.DestroyImmediate(mc);
        }
    }

    private static Material GetOrCreateMaterial(string name, Color color, float smoothness = 0.2f)
    {
        string path = $"{TargetFolder}/M_{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static void CreateRecyclerPrefab()
    {
        GameObject root = new GameObject("Recycler2x2");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.8f, 1.4f, 1.8f);
        bc.center = new Vector3(0f, 0.7f, 0f);

        root.AddComponent<RecyclerMachine>();

        Material steelMat = GetOrCreateMaterial("RecyclerSteel", new Color(0.25f, 0.45f, 0.55f));
        Material brassMat = GetOrCreateMaterial("RecyclerBrass", new Color(0.85f, 0.65f, 0.25f));

        GameObject mainBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainBody.name = "Body";
        mainBody.transform.SetParent(root.transform);
        mainBody.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        mainBody.transform.localScale = new Vector3(1.8f, 1.2f, 1.8f);
        mainBody.GetComponent<Renderer>().sharedMaterial = steelMat;
        Object.DestroyImmediate(mainBody.GetComponent<Collider>());

        GameObject funnel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        funnel.name = "InputFunnel";
        funnel.transform.SetParent(root.transform);
        funnel.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        funnel.transform.localScale = new Vector3(0.9f, 0.4f, 0.9f);
        funnel.GetComponent<Renderer>().sharedMaterial = brassMat;
        Object.DestroyImmediate(funnel.GetComponent<Collider>());

        string prefabPath = $"{TargetFolder}/Recycler2x2.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateShippingBinPrefab()
    {
        GameObject root = new GameObject("ShippingBin");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.9f, 0.9f, 0.9f);
        bc.center = new Vector3(0f, 0.45f, 0f);

        root.AddComponent<ShippingBinMachine>();

        Material crateWood = GetOrCreateMaterial("CrateWood", new Color(0.55f, 0.35f, 0.2f));
        Material ironBand = GetOrCreateMaterial("IronBand", new Color(0.2f, 0.22f, 0.25f));

        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Chest";
        crate.transform.SetParent(root.transform);
        crate.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        crate.transform.localScale = new Vector3(0.88f, 0.8f, 0.88f);
        crate.GetComponent<Renderer>().sharedMaterial = crateWood;
        Object.DestroyImmediate(crate.GetComponent<Collider>());

        GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(root.transform);
        lid.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        lid.transform.localScale = new Vector3(0.94f, 0.16f, 0.94f);
        lid.GetComponent<Renderer>().sharedMaterial = ironBand;
        Object.DestroyImmediate(lid.GetComponent<Collider>());

        string prefabPath = $"{TargetFolder}/ShippingBin.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateTreeSaplingPrefab()
    {
        GameObject root = new GameObject("TreeSapling");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.8f, 1.8f, 0.8f);
        bc.center = new Vector3(0f, 0.9f, 0f);

        root.AddComponent<TreePlantation>();

        GameObject treeVox = FindVoxModel("Tree");
        if (treeVox != null)
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(treeVox);
            visual.name = "TreeVoxelModel";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;
            StripMeshColliders(visual);
        }
        else
        {
            Material trunkWood = GetOrCreateMaterial("TreeTrunk", new Color(0.4f, 0.25f, 0.15f));
            Material greenLeaves = GetOrCreateMaterial("TreeLeaves", new Color(0.2f, 0.72f, 0.3f));

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform);
            trunk.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            trunk.transform.localScale = new Vector3(0.3f, 1.0f, 0.3f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkWood;
            Object.DestroyImmediate(trunk.GetComponent<Collider>());

            GameObject leaves1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaves1.name = "LeavesLower";
            leaves1.transform.SetParent(root.transform);
            leaves1.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            leaves1.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
            leaves1.GetComponent<Renderer>().sharedMaterial = greenLeaves;
            Object.DestroyImmediate(leaves1.GetComponent<Collider>());
        }

        string prefabPath = $"{TargetFolder}/TreeSapling.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateConveyorBeltPrefab()
    {
        GameObject root = new GameObject("ConveyorBelt");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.95f, 0.25f, 0.95f);
        bc.center = new Vector3(0f, 0.12f, 0f);
        bc.isTrigger = true;

        root.AddComponent<ConveyorBelt>();

        Material beltBlack = GetOrCreateMaterial("BeltTread", new Color(0.12f, 0.12f, 0.13f));
        Material arrowYellow = GetOrCreateMaterial("BeltYellow", new Color(0.95f, 0.85f, 0.15f));

        GameObject tread = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tread.transform.SetParent(root.transform);
        tread.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        tread.transform.localScale = new Vector3(0.96f, 0.16f, 0.96f);
        tread.GetComponent<Renderer>().sharedMaterial = beltBlack;
        Object.DestroyImmediate(tread.GetComponent<Collider>());

        GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arrow.transform.SetParent(root.transform);
        arrow.transform.localPosition = new Vector3(0f, 0.17f, 0.1f);
        arrow.transform.localScale = new Vector3(0.35f, 0.03f, 0.45f);
        arrow.GetComponent<Renderer>().sharedMaterial = arrowYellow;
        Object.DestroyImmediate(arrow.GetComponent<Collider>());

        string prefabPath = $"{TargetFolder}/ConveyorBelt.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateAutoVacuumTurretPrefab()
    {
        GameObject root = new GameObject("AutoVacuumTurret");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.95f, 1.4f, 0.95f);
        bc.center = new Vector3(0f, 0.7f, 0f);

        root.AddComponent<AutoVacuumTurret>();

        Material baseMat = GetOrCreateMaterial("TurretBase", new Color(0.22f, 0.3f, 0.38f));
        Material cyanMat = GetOrCreateMaterial("TurretCyan", new Color(0.2f, 0.85f, 0.95f));

        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = "Pedestal";
        pedestal.transform.SetParent(root.transform);
        pedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        pedestal.transform.localScale = new Vector3(0.85f, 0.7f, 0.85f);
        pedestal.GetComponent<Renderer>().sharedMaterial = baseMat;
        Object.DestroyImmediate(pedestal.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "TurretHead";
        head.transform.SetParent(root.transform);
        head.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        head.transform.localScale = new Vector3(0.65f, 0.5f, 0.85f);
        head.GetComponent<Renderer>().sharedMaterial = cyanMat;
        Object.DestroyImmediate(head.GetComponent<Collider>());

        string prefabPath = $"{TargetFolder}/AutoVacuumTurret.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateBridgePrefab()
    {
        GameObject root = new GameObject("WoodenBridge");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.0f, 0.25f, 1.0f);
        bc.center = new Vector3(0f, 0.12f, 0f);

        Material woodPlank = GetOrCreateMaterial("BridgePlank", new Color(0.45f, 0.32f, 0.2f));
        Material railing = GetOrCreateMaterial("BridgeRailing", new Color(0.32f, 0.22f, 0.12f));

        GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.transform.SetParent(root.transform);
        plank.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        plank.transform.localScale = new Vector3(1.0f, 0.2f, 1.0f);
        plank.GetComponent<Renderer>().sharedMaterial = woodPlank;
        Object.DestroyImmediate(plank.GetComponent<Collider>());

        GameObject railL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railL.transform.SetParent(root.transform);
        railL.transform.localPosition = new Vector3(-0.45f, 0.35f, 0f);
        railL.transform.localScale = new Vector3(0.1f, 0.4f, 1.0f);
        railL.GetComponent<Renderer>().sharedMaterial = railing;
        Object.DestroyImmediate(railL.GetComponent<Collider>());

        GameObject railR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railR.transform.SetParent(root.transform);
        railR.transform.localPosition = new Vector3(0.45f, 0.35f, 0f);
        railR.transform.localScale = new Vector3(0.1f, 0.4f, 1.0f);
        railR.GetComponent<Renderer>().sharedMaterial = railing;
        Object.DestroyImmediate(railR.GetComponent<Collider>());

        string prefabPath = $"{TargetFolder}/WoodenBridge.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateChimneyPrefab()
    {
        GameObject root = new GameObject("FactoryFilterChimney");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.6f, 3.0f, 1.6f);
        bc.center = new Vector3(0f, 1.5f, 0f);

        root.AddComponent<FactoryFilterChimney>();

        GameObject chimneyVox = FindVoxModel("FactoryChimney");
        GameObject airFilterVox = FindVoxModel("AirFilter");

        if (chimneyVox != null)
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(chimneyVox);
            visual.name = "ChimneyVoxelModel";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 2.2f;
            StripMeshColliders(visual);

            if (airFilterVox != null)
            {
                GameObject filterVisual = (GameObject)PrefabUtility.InstantiatePrefab(airFilterVox);
                filterVisual.name = "InstalledAirFilter";
                filterVisual.transform.SetParent(root.transform, false);
                filterVisual.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                filterVisual.transform.localScale = Vector3.one * 2.0f;
                StripMeshColliders(filterVisual);
                filterVisual.SetActive(false);
            }
        }
        else
        {
            Material brickMat = GetOrCreateMaterial("ChimneyBrick", new Color(0.35f, 0.28f, 0.25f));
            Material rimMat = GetOrCreateMaterial("ChimneyRim", new Color(0.2f, 0.2f, 0.22f));

            GameObject baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCube.name = "ChimneyBase";
            baseCube.transform.SetParent(root.transform);
            baseCube.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            baseCube.transform.localScale = new Vector3(1.4f, 0.7f, 1.4f);
            baseCube.GetComponent<Renderer>().sharedMaterial = brickMat;
            Object.DestroyImmediate(baseCube.GetComponent<Collider>());

            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pipe.name = "ChimneyPipe";
            pipe.transform.SetParent(root.transform);
            pipe.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            pipe.transform.localScale = new Vector3(0.85f, 1.7f, 0.85f);
            pipe.GetComponent<Renderer>().sharedMaterial = brickMat;
            Object.DestroyImmediate(pipe.GetComponent<Collider>());

            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "ChimneyRim";
            rim.transform.SetParent(root.transform);
            rim.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            rim.transform.localScale = new Vector3(1.05f, 0.25f, 1.05f);
            rim.GetComponent<Renderer>().sharedMaterial = rimMat;
            Object.DestroyImmediate(rim.GetComponent<Collider>());
        }

        string prefabPath = $"{TargetFolder}/FactoryFilterChimney.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateCompostStationPrefab()
    {
        GameObject root = new GameObject("FarmlandCompostStation");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.5f, 0.8f, 1.5f);
        bc.center = new Vector3(0f, 0.4f, 0f);

        root.AddComponent<FarmlandCompostStation>();

        Material woodMat = GetOrCreateMaterial("CompostWood", new Color(0.5f, 0.32f, 0.18f));
        Material soilMat = GetOrCreateMaterial("CompostSoil", new Color(0.22f, 0.18f, 0.12f));

        GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bed.name = "SoilBed";
        bed.transform.SetParent(root.transform);
        bed.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        bed.transform.localScale = new Vector3(1.3f, 0.5f, 1.3f);
        bed.GetComponent<Renderer>().sharedMaterial = soilMat;
        Object.DestroyImmediate(bed.GetComponent<Collider>());

        GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        border.name = "Border";
        border.transform.SetParent(root.transform);
        border.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        border.transform.localScale = new Vector3(1.5f, 0.55f, 1.5f);
        border.GetComponent<Renderer>().sharedMaterial = woodMat;
        Object.DestroyImmediate(border.GetComponent<Collider>());

        string prefabPath = $"{TargetFolder}/FarmlandCompostStation.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateRiverBuoyPrefab()
    {
        GameObject root = new GameObject("RiverBuoyStation");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.2f, 1.4f, 1.2f);
        bc.center = new Vector3(0f, 0.7f, 0f);

        root.AddComponent<RiverBuoyStation>();

        GameObject buoyVox = FindVoxModel("Buoy");
        if (buoyVox != null)
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(buoyVox);
            visual.name = "BuoyVoxelModel";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 2.2f;
            StripMeshColliders(visual);
        }
        else
        {
            Material buoyMat = GetOrCreateMaterial("BuoyYellow", new Color(0.95f, 0.8f, 0.1f));
            Material beaconMat = GetOrCreateMaterial("BuoyBeacon", new Color(0.2f, 0.85f, 0.95f));

            GameObject floatHull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floatHull.name = "Hull";
            floatHull.transform.SetParent(root.transform);
            floatHull.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            floatHull.transform.localScale = new Vector3(0.9f, 0.35f, 0.9f);
            floatHull.GetComponent<Renderer>().sharedMaterial = buoyMat;
            Object.DestroyImmediate(floatHull.GetComponent<Collider>());

            GameObject lightDome = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lightDome.name = "Beacon";
            lightDome.transform.SetParent(root.transform);
            lightDome.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            lightDome.transform.localScale = new Vector3(0.35f, 0.3f, 0.35f);
            lightDome.GetComponent<Renderer>().sharedMaterial = beaconMat;
            Object.DestroyImmediate(lightDome.GetComponent<Collider>());
        }

        string prefabPath = $"{TargetFolder}/RiverBuoyStation.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateCityHubPrefab()
    {
        GameObject root = new GameObject("CityRecruitmentHub");
        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = new Vector3(1.8f, 1.8f, 1.8f);
        bc.center = new Vector3(0f, 0.9f, 0f);

        root.AddComponent<CityRecruitmentHub>();

        GameObject townHallVox = FindVoxModel("TownHall");
        if (townHallVox != null)
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(townHallVox);
            visual.name = "TownHallVoxelModel";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 2.4f;
            StripMeshColliders(visual);
        }
        else
        {
            Material stoneMat = GetOrCreateMaterial("HubStone", new Color(0.6f, 0.65f, 0.7f));
            Material bannerMat = GetOrCreateMaterial("HubBanner", new Color(0.2f, 0.75f, 0.35f));

            GameObject kiosk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kiosk.name = "KioskBody";
            kiosk.transform.SetParent(root.transform);
            kiosk.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            kiosk.transform.localScale = new Vector3(1.6f, 1.2f, 1.2f);
            kiosk.GetComponent<Renderer>().sharedMaterial = stoneMat;
            Object.DestroyImmediate(kiosk.GetComponent<Collider>());

            GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "EcoBanner";
            banner.transform.SetParent(root.transform);
            banner.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            banner.transform.localScale = new Vector3(1.7f, 0.5f, 0.2f);
            banner.GetComponent<Renderer>().sharedMaterial = bannerMat;
            Object.DestroyImmediate(banner.GetComponent<Collider>());
        }

        string prefabPath = $"{TargetFolder}/CityRecruitmentHub.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }
}
