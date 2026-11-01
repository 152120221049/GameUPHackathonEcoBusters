using UnityEngine;

/// <summary>
/// Fabrika Bölgesi Eko-Müdahale İstasyonu: Filtresiz Baca (FactoryChimney.vox + AirFilter.vox).
/// - Başlangıçta siyah/gri zehirli duman salar ve fabrikayı kirletir.
/// - Oyuncu ürettiği [CarbonFilter] (Baca Filtresi) ile etkileşime girip AirFilter.vox modelini bacaya monte eder.
/// - Filtre takıldıktan sonra duman berrak buhara dönüşür, hava temizlenir ve Fabrika Merkez Tesisi (RecyclingBuilding) güçlenir.
/// </summary>
public class FactoryFilterChimney : MonoBehaviour, IInteractable
{
    [Header("--- BACA DURUMU ---")]
    [SerializeField] private bool isFiltered = false;
    [Tooltip("Dumanın temizlenme ve çevreye yayılma yarıçapı")]
    [SerializeField] private float effectRadius = 10f;

    [Header("--- GÖRSEL VE PARTİKÜL ---")]
    [SerializeField] private ParticleSystem smokeParticle;
    [SerializeField] private GameObject installedFilterVisual;
    [SerializeField] private Color dirtySmokeColor = new Color(0.14f, 0.14f, 0.15f, 0.88f);
    [SerializeField] private Color cleanSteamColor = new Color(0.92f, 0.96f, 1.0f, 0.32f);

    private float cleanTransitionTimer;
    private float purifyTimer = 0.6f;
    private const float CleanDuration = 8f;

    public bool IsFiltered => isFiltered;

    public void SetFilterVisualObject(GameObject filterObj)
    {
        installedFilterVisual = filterObj;
        if (installedFilterVisual != null)
        {
            installedFilterVisual.SetActive(isFiltered);
        }
    }

    private void Start()
    {
        if (installedFilterVisual == null)
        {
            Transform child = transform.Find("InstalledAirFilter");
            if (child != null) installedFilterVisual = child.gameObject;
        }

        if (installedFilterVisual != null)
        {
            installedFilterVisual.SetActive(isFiltered);
        }

        EnsureSmokeParticle();
        UpdateSmokeVisual(instant: true);
    }

    private void EnsureSmokeParticle()
    {
        var emitter = GetComponent<FactorySmokeEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<FactorySmokeEmitter>();
        emitter.EnsureSmokeEmission();

        if (smokeParticle == null)
            smokeParticle = GetComponentInChildren<ParticleSystem>();

        if (smokeParticle != null) return;

        // En yüksek noktayı bul (baca tepesi)
        float topY = 3.5f;
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            float maxWorldY = transform.position.y;
            foreach (var r in rends)
            {
                if (r.bounds.max.y > maxWorldY) maxWorldY = r.bounds.max.y;
            }
            topY = Mathf.Max(2.2f, (maxWorldY - transform.position.y) / Mathf.Max(0.1f, transform.localScale.y));
        }

        GameObject smokeObj = new GameObject("ChimneySmokeVFX");
        smokeObj.transform.SetParent(transform, false);
        smokeObj.transform.localPosition = new Vector3(0f, topY, 0f);
        smokeObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        smokeParticle = smokeObj.AddComponent<ParticleSystem>();
        var main = smokeParticle.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startColor = dirtySmokeColor;
        main.maxParticles = 35;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = smokeParticle.emission;
        emission.rateOverTime = 8f;

        var shape = smokeParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.35f;

        var renderer = smokeObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                Material m = new Material(sh);
                m.color = dirtySmokeColor;
                renderer.sharedMaterial = m;
            }
        }
    }

    private void Update()
    {
        if (isFiltered)
        {
            if (cleanTransitionTimer < CleanDuration)
            {
                cleanTransitionTimer += Time.deltaTime;
                UpdateSmokeVisual(instant: false);
            }

            // Her karede değil saniyede 1 kez çevreyi temizle (takılmaları önler)
            purifyTimer -= Time.deltaTime;
            if (purifyTimer <= 0f)
            {
                purifyTimer = 1.0f;
                PurifySurroundingTiles();
            }
        }
    }

    public string GetInteractionPrompt()
    {
        if (isFiltered)
        {
            return "Filtreli Eko-Baca (AirFilter Aktif - Fabrika Temiz Hava Modunda)";
        }

        if (PlayerInventory.Instance != null && PlayerInventory.Instance.GetCount(ItemType.CarbonFilter) >= 1)
        {
            return "[E] Baca Filtresi Tak (1 Baca Filtresi Kullan)";
        }

        return "Zehirli Fabrika Bacası (Atölyede 1 Baca Filtresi Üretip [E] ile Tak)";
    }

    public void Interact(GameObject interactor)
    {
        if (isFiltered)
        {
            PlayerInventory.Instance?.Notify("Bu bacada AirFilter zaten takılı ve havayı temizliyor!");
            return;
        }

        if (PlayerInventory.Instance != null && PlayerInventory.Instance.RemoveItem(ItemType.CarbonFilter, 1))
        {
            isFiltered = true;
            cleanTransitionTimer = 0f;

            if (installedFilterVisual != null)
            {
                installedFilterVisual.SetActive(true);
            }

            GetComponent<FactorySmokeEmitter>()?.SetFiltered(true);

            PlayerInventory.Instance.AddCoins(25);
            if (EcoSystemManager.Instance != null)
            {
                EcoSystemManager.Instance.RegisterChimneyFilterInstalled(transform.position);
            }
            else
            {
                PlayerInventory.Instance.Notify("Baca Filtresi Takıldı! Duman temizleniyor (+25 Eko-Kredi).");
            }
            GameAudioAndJuiceManager.Instance?.TriggerPurifyOrRepairOomph(transform.position, "BACA FİLTRESİ TAKILDI! (+25 Kredi)", new Color(0.45f, 0.95f, 1f), transform);
        }
        else
        {
            PlayerInventory.Instance?.Notify("Yetersiz malzeme! Atölyede 1 Baca Filtresi üretmelisin.");
            GameAudioAndJuiceManager.Instance?.PlayErrorSound(transform.position, "BACA FİLTRESİ GEREKLİ!");
        }
    }

    private void PurifySurroundingTiles()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, effectRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            GroundTile tile = hits[i].GetComponent<GroundTile>();
            if (tile != null && tile.targetPollutionLevel > 0f)
            {
                tile.ModifyPollution(-6f);
            }
        }
    }

    private void UpdateSmokeVisual(bool instant)
    {
        if (smokeParticle == null) return;

        var main = smokeParticle.main;
        float t = isFiltered ? (instant ? 1f : Mathf.Clamp01(cleanTransitionTimer / CleanDuration)) : 0f;
        main.startColor = Color.Lerp(dirtySmokeColor, cleanSteamColor, t);

        var emission = smokeParticle.emission;
        emission.rateOverTime = isFiltered ? 4f : 9f;
    }

    public Vector3 GetPosition() => transform.position;
}
