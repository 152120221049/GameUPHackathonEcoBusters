using UnityEngine;

/// <summary>
/// Fabrika binaları ve bacaları için duman emisyon kontrolcüsü.
/// - Baca tepesini (çatıdaki en yüksek noktayı) otomatik tespit eder.
/// - FACTORYsMOKE.prefab veya optimize edilmiş parçacık sistemi ile koyu endüstriyel duman yayar.
/// - Baca filtrelendiğinde veya fabrika arındırıldığında dumanı temiz su buharına dönüştürür.
/// </summary>
public class FactorySmokeEmitter : MonoBehaviour
{
    [Header("--- DUMAN AYARLARI ---")]
    [Tooltip("Duman partikül sistemi")]
    [SerializeField] private ParticleSystem smokeParticle;
    [Tooltip("Önceden hazırlanmış duman prefabı (FACTORYsMOKE)")]
    [SerializeField] private GameObject smokePrefab;

    [Header("--- RENK VE FİLTRE DURUMU ---")]
    [SerializeField] private bool isFiltered = false;
    [SerializeField] private Color dirtySmokeColor = new Color(0.22f, 0.22f, 0.24f, 0.85f);
    [SerializeField] private Color cleanSteamColor = new Color(0.92f, 0.96f, 1.0f, 0.35f);

    [Header("--- BACA OFSETİ ---")]
    [SerializeField] private Vector3 customSmokeOffset = Vector3.zero;

    public bool IsFiltered => isFiltered;

    private void Start()
    {
        EnsureSmokeEmission();
        ApplySmokeState(isFiltered, instant: true);
    }

    public void SetFiltered(bool filtered)
    {
        isFiltered = filtered;
        ApplySmokeState(isFiltered, instant: false);
    }

    public void EnsureSmokeEmission()
    {
        if (smokeParticle != null) return;

        smokeParticle = GetComponentInChildren<ParticleSystem>();
        if (smokeParticle != null) return;

        // 1. Projedeki FACTORYsMOKE prefabını bul
        if (smokePrefab == null)
        {
            smokePrefab = Resources.Load<GameObject>("FACTORYsMOKE");
#if UNITY_EDITOR
            if (smokePrefab == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("FACTORYsMOKE t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    smokePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
#endif
        }

        Vector3 spawnLocalPos = CalculateTopEmissionPoint();

        if (smokePrefab != null)
        {
            GameObject smokeObj = Instantiate(smokePrefab, transform);
            smokeObj.name = "FactorySmoke_Instance";
            smokeObj.transform.localPosition = spawnLocalPos;
            smokeObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            smokeObj.transform.localScale = Vector3.one;

            smokeParticle = smokeObj.GetComponent<ParticleSystem>();
            if (smokeParticle != null)
            {
                var main = smokeParticle.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = true;
                if (!smokeParticle.isPlaying) smokeParticle.Play();
            }
        }
        else
        {
            // Prosedürel yedek parçacık oluştur
            CreateProceduralSmoke(spawnLocalPos);
        }
    }

    private Vector3 CalculateTopEmissionPoint()
    {
        if (customSmokeOffset != Vector3.zero) return customSmokeOffset;

        float maxWorldY = transform.position.y;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                if (r.bounds.max.y > maxWorldY) maxWorldY = r.bounds.max.y;
            }
        }

        float localY = (maxWorldY - transform.position.y) / Mathf.Max(0.001f, transform.lossyScale.y);
        localY += 0.2f; // Baca ağzının hafif üstü
        return new Vector3(0f, Mathf.Max(1.5f, localY), 0f);
    }

    private void CreateProceduralSmoke(Vector3 localPos)
    {
        GameObject smokeObj = new GameObject("ProceduralSmokeVFX");
        smokeObj.transform.SetParent(transform, false);
        smokeObj.transform.localPosition = localPos;
        smokeObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        smokeParticle = smokeObj.AddComponent<ParticleSystem>();
        var main = smokeParticle.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
        main.startColor = dirtySmokeColor;
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = smokeParticle.emission;
        emission.rateOverTime = 10f;

        var shape = smokeParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.4f;

        var renderer = smokeObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");
            if (sh != null)
            {
                renderer.material = new Material(sh);
            }
        }

        smokeParticle.Play();
    }

    public void ApplySmokeState(bool filtered, bool instant)
    {
        isFiltered = filtered;
        if (smokeParticle == null) return;

        var main = smokeParticle.main;
        Color targetColor = isFiltered ? cleanSteamColor : dirtySmokeColor;
        main.startColor = targetColor;

        var emission = smokeParticle.emission;
        emission.rateOverTime = isFiltered ? 3.5f : 12f;
    }
}

