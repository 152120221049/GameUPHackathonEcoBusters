using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nehir Saydam Su Yüzeyi, Akıntı Efektleri ve Dünya Sınırı Sis/Bulut Kuşağı Yöneticisi.
/// - Nehir yatağının üzerine kodla yarı-saydam (transparent), dalgalanan ve Ada Sağlığı / Nehir Temizliği
///   arttıkça bulanık yeşilden berrak turkuaza dönüşen bir Su Yüzeyi (Water Mesh & Material) ve akıntı partikülleri kurar.
/// - Adanın 360 derece dairesel dış kıyı sınırlarına yoğun, yavaşça dalgalanan bir Sis/Bulut Duvarı (Perimeter Fog Ring)
///   ve yumuşak sınır bariyeri ekleyerek harita kenarındaki keskin boşluk görüntüsünü tamamen gizler.
/// </summary>
public class RiverWaterAndFogSystem : MonoBehaviour
{
    public static RiverWaterAndFogSystem Instance { get; private set; }

    [Header("--- NEHİR SU YÜZEYİ MATERYALİ (URP) ---")]
    [SerializeField] private Material waterMaterialAsset;

    [Header("--- NEHİR SU YÜZEYİ RENKLERİ ---")]
    [SerializeField] private Color pollutedWaterColor = new Color(0.18f, 0.42f, 0.35f, 0.85f);
    [SerializeField] private Color cleanWaterColor = new Color(0.12f, 0.65f, 0.95f, 0.75f);

    private Material sharedWaterMaterial;
    private Material sharedFogCloudMaterial;
    private Transform waterRoot;
    private Transform borderFogRoot;
    private Transform playerTransform;
    private float islandRadiusMeters = 280f;
    private float colorUpdateTimer;
    private float lastBorderNotifyTime;

    public Material SharedWaterMaterial
    {
        get
        {
            if (sharedWaterMaterial == null) CreateSharedWaterMaterial();
            return sharedWaterMaterial;
        }
    }

    private void Awake()
    {
        Instance = this;
        CreateSharedWaterMaterial();
    }

    private void CreateSharedWaterMaterial()
    {
        if (sharedWaterMaterial != null) return;

        // 1. Önce projede hazır bulunan URP Lit M_RiverWater_URP materyalini yüklemeyi dene
        if (waterMaterialAsset != null)
        {
            sharedWaterMaterial = new Material(waterMaterialAsset);
        }
        else
        {
            Material loadedMat = Resources.Load<Material>("M_RiverWater_URP");
            if (loadedMat == null)
            {
                loadedMat = Resources.Load<Material>("M_FallbackLit");
            }
#if UNITY_EDITOR
            if (loadedMat == null)
            {
                loadedMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Structures/M_RiverWater_URP.mat");
            }
            if (loadedMat == null)
            {
                loadedMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Structures/M_BridgePlank.mat");
            }
#endif
            if (loadedMat != null)
            {
                sharedWaterMaterial = new Material(loadedMat);
            }
            else
            {
                // Mevcut sahnede veya projede URP Lit kullanan herhangi bir materyalden shader al
                Renderer sampleRend = FindFirstObjectByType<Renderer>();
                Shader sh = (sampleRend != null && sampleRend.sharedMaterial != null && sampleRend.sharedMaterial.shader != null)
                    ? sampleRend.sharedMaterial.shader
                    : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard"));

                sharedWaterMaterial = (sh != null) ? new Material(sh) : null;
            }
        }

        if (sharedWaterMaterial == null)
        {
            Debug.LogError("[RiverWaterAndFogSystem] Su materyali olusturulamadi!");
            return;
        }

        sharedWaterMaterial.name = "M_RiverDynamicWater_Transparent";

        // URP Lit ve Standard Shader için Yarı-Saydam (Transparent) Mod Ayarları
        sharedWaterMaterial.SetFloat("_Surface", 1f);
        sharedWaterMaterial.SetFloat("_Blend", 0f);
        sharedWaterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        sharedWaterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        sharedWaterMaterial.SetInt("_ZWrite", 0);
        sharedWaterMaterial.DisableKeyword("_ALPHATEST_ON");
        sharedWaterMaterial.EnableKeyword("_ALPHABLEND_ON");
        sharedWaterMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        sharedWaterMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sharedWaterMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (sharedWaterMaterial.HasProperty("_Smoothness"))
            sharedWaterMaterial.SetFloat("_Smoothness", 0.92f);
        if (sharedWaterMaterial.HasProperty("_Glossiness"))
            sharedWaterMaterial.SetFloat("_Glossiness", 0.92f);

        ApplyWaterColor(0.25f);
    }

    private void ApplyWaterColor(float purity01)
    {
        if (sharedWaterMaterial == null) return;
        Color c = Color.Lerp(pollutedWaterColor, cleanWaterColor, Mathf.Clamp01(purity01));
        if (sharedWaterMaterial.HasProperty("_BaseColor"))
            sharedWaterMaterial.SetColor("_BaseColor", c);
        if (sharedWaterMaterial.HasProperty("_Color"))
            sharedWaterMaterial.SetColor("_Color", c);
    }

    public void InitializeRiverAndBorderFog(
        Transform mapRoot,
        float worldIslandRadius,
        List<Vector3> riverCenterPoints)
    {
        islandRadiusMeters = Mathf.Max(60f, worldIslandRadius);

        CreateSharedWaterMaterial();
        BuildRiverFlowParticles(mapRoot, riverCenterPoints);
        BuildWorldBorderFogRing(mapRoot, islandRadiusMeters);
    }

    private void BuildRiverFlowParticles(Transform mapRoot, List<Vector3> riverPoints)
    {
        if (riverPoints == null || riverPoints.Count == 0) return;

        if (waterRoot != null) Destroy(waterRoot.gameObject);
        GameObject wr = new GameObject("RiverSurfaceEffects");
        wr.transform.SetParent(mapRoot, false);
        waterRoot = wr.transform;

        Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        Material foamMat = pShader != null ? new Material(pShader) : null;
        if (foamMat != null)
        {
            foamMat.color = new Color(0.85f, 0.96f, 1.0f, 0.48f);
        }

        int step = Mathf.Max(1, riverPoints.Count / 18);
        for (int i = 0; i < riverPoints.Count; i += step)
        {
            Vector3 pt = riverPoints[i];
            GameObject pObj = new GameObject($"RiverFlowVFX_{i}");
            pObj.transform.SetParent(waterRoot, false);
            pObj.transform.position = new Vector3(pt.x, -0.16f, pt.z);

            if (i + 1 < riverPoints.Count)
            {
                Vector3 dir = (riverPoints[i + 1] - pt);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    pObj.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startColor = new Color(0.82f, 0.96f, 1.0f, 0.45f);
            main.maxParticles = 28;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6.0f, 0.1f, 5.0f);

            var rend = pObj.GetComponent<ParticleSystemRenderer>();
            if (rend != null && foamMat != null)
            {
                rend.renderMode = ParticleSystemRenderMode.Stretch;
                rend.lengthScale = 2.4f;
                rend.sharedMaterial = foamMat;
            }
        }
    }

    private void BuildWorldBorderFogRing(Transform mapRoot, float radiusM)
    {
        if (borderFogRoot != null) Destroy(borderFogRoot.gameObject);

        GameObject fogRootObj = new GameObject("WorldBorderFogRing");
        fogRootObj.transform.SetParent(mapRoot, false);
        borderFogRoot = fogRootObj.transform;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        sharedFogCloudMaterial = new Material(sh);
        sharedFogCloudMaterial.name = "M_BorderCloudMist";
        sharedFogCloudMaterial.SetFloat("_Surface", 1f);
        sharedFogCloudMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        sharedFogCloudMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        sharedFogCloudMaterial.SetInt("_ZWrite", 0);
        sharedFogCloudMaterial.EnableKeyword("_ALPHABLEND_ON");
        sharedFogCloudMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sharedFogCloudMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Color mistColor = new Color(0.78f, 0.84f, 0.88f, 0.82f);
        if (sharedFogCloudMaterial.HasProperty("_BaseColor"))
            sharedFogCloudMaterial.SetColor("_BaseColor", mistColor);
        if (sharedFogCloudMaterial.HasProperty("_Color"))
            sharedFogCloudMaterial.SetColor("_Color", mistColor);

        // Adanın dairesel kıyı şeridi boyunca 3 katmanlı yoğun sis/bulut halkası oluştur
        int segments = 72;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // İç sis kuşağı (tam kıyı sınırında)
            SpawnCloudPuff(dir * (radiusM - 12f), 26f, 7.5f, i);
            // Orta yoğun bulut duvarı
            SpawnCloudPuff(dir * (radiusM + 4f), 34f, 11.5f, i + 100);
            // Dış ufuk bulut örtüsü
            SpawnCloudPuff(dir * (radiusM + 22f), 42f, 14.0f, i + 200);
        }
    }

    private void SpawnCloudPuff(Vector3 pos, float horizontalSize, float verticalSize, int seedIdx)
    {
        GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        puff.name = "BorderCloudPuff";
        puff.transform.SetParent(borderFogRoot, false);
        float jitterY = Mathf.Sin(seedIdx * 1.7f) * 1.4f;
        puff.transform.position = new Vector3(pos.x, 1.0f + jitterY, pos.z);
        puff.transform.localScale = new Vector3(horizontalSize, verticalSize, horizontalSize);

        Collider col = puff.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer r = puff.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = sharedFogCloudMaterial;
    }

    private void Update()
    {
        colorUpdateTimer -= Time.deltaTime;
        if (colorUpdateTimer <= 0f)
        {
            colorUpdateTimer = 0.25f;
            float purity = 0.25f;
            if (EcoSystemManager.Instance != null)
            {
                purity = (EcoSystemManager.Instance.RiverPurity * 0.65f + EcoSystemManager.Instance.GlobalEcoScore * 0.35f) / 100f;
            }
            ApplyWaterColor(purity);
        }

        if (borderFogRoot != null)
        {
            float bob = Mathf.Sin(Time.time * 0.85f) * 0.45f;
            borderFogRoot.localPosition = new Vector3(0f, bob, 0f);
            borderFogRoot.Rotate(Vector3.up, 0.35f * Time.deltaTime, Space.Self);
        }

        // Oyuncu adanın dış sis kuşağına girerse yumuşakça adanın içine geri it
        if (playerTransform == null)
        {
            var cc = FindFirstObjectByType<CharacterController25D>();
            if (cc != null) playerTransform = cc.transform;
        }

        if (playerTransform != null)
        {
            Vector3 p = playerTransform.position;
            Vector2 flatPos = new Vector2(p.x, p.z);
            float maxSafeRadius = islandRadiusMeters - 22f;

            if (flatPos.magnitude > maxSafeRadius && maxSafeRadius > 20f)
            {
                Vector2 clampedFlat = flatPos.normalized * maxSafeRadius;
                Vector3 corrected = new Vector3(clampedFlat.x, p.y, clampedFlat.y);
                playerTransform.position = corrected;

                Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = corrected;
                }

                if (Time.time - lastBorderNotifyTime > 3.5f)
                {
                    lastBorderNotifyTime = Time.time;
                    PlayerInventory.Instance?.Notify("Adanın dış sis sınırına ulaştın! Yoğun sis nedeniyle geri döndün.");
                }
            }
        }
    }
}
