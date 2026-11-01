using UnityEngine;

/// <summary>
/// İnşa edilebilir Güneş Paneli (SolarPanel.vox) veya Rüzgar Türbini (WindTurbine.vox) İstasyonu.
/// - Belirli aralıklarla adaya pasif Temiz Enerji (Eko-Kredi) ve Ada Sağlığı katkısı sağlar.
/// - Rüzgar türbini rüzgarda hafif salınım ve aerodinamik parçacık dalgası yayar.
/// - Oyuncu yaklaştığında [E] ile biriken eko-enerjiyi toplar.
/// </summary>
public class CleanEnergyStation : MonoBehaviour, IInteractable
{
    public enum EnergyType { Solar, Wind }

    [SerializeField] private EnergyType energyType = EnergyType.Solar;
    [SerializeField] private float productionInterval = 12f;
    [SerializeField] private int coinsPerCycle = 10;
    [SerializeField] private float ecoContribution = 2.5f;

    private float timer = 0f;
    private int storedCoins = 0;
    private GameObject visualModel;

    public void ConfigureStation(EnergyType type)
    {
        energyType = type;
        gameObject.name = type == EnergyType.Solar ? "Station_SolarPanel" : "Station_WindTurbine";

        EnsureCollider();
        EnsureVisualModel();

        if (EcoSystemManager.Instance != null)
        {
            EcoSystemManager.Instance.RegisterCleanEnergyInstalled(type == EnergyType.Solar ? "Güneş Paneli" : "Rüzgar Türbini");
        }
    }

    private void Start()
    {
        EnsureCollider();
        EnsureVisualModel();
    }

    private void EnsureCollider()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null)
        {
            bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.6f, energyType == EnergyType.Wind ? 3.5f : 1.2f, 1.6f);
            bc.center = new Vector3(0f, bc.size.y * 0.5f, 0f);
        }
    }

    private void EnsureVisualModel()
    {
        if (visualModel != null) return;

        string voxName = energyType == EnergyType.Solar ? "SolarPanel" : "WindTurbine";
        GameObject voxAsset = Resources.Load<GameObject>(voxName);

#if UNITY_EDITOR
        if (voxAsset == null)
        {
            voxAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ArtAssets/3d/{voxName}.vox");
        }
#endif

        if (voxAsset != null)
        {
            visualModel = Instantiate(voxAsset, transform);
            visualModel.name = "VisualModel";
            visualModel.transform.localPosition = Vector3.zero;
            visualModel.transform.localRotation = Quaternion.identity;
            visualModel.transform.localScale = Vector3.one * (energyType == EnergyType.Wind ? 1.8f : 2.2f);

            foreach (var mc in visualModel.GetComponentsInChildren<MeshCollider>(true))
            {
                Destroy(mc);
            }
        }
        else
        {
            // Fallback
            visualModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualModel.transform.SetParent(transform, false);
            visualModel.transform.localPosition = new Vector3(0f, energyType == EnergyType.Wind ? 1.5f : 0.4f, 0f);
            visualModel.transform.localScale = new Vector3(1.2f, energyType == EnergyType.Wind ? 3f : 0.6f, 1.2f);
            Destroy(visualModel.GetComponent<Collider>());
            var rend = visualModel.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = energyType == EnergyType.Solar ? new Color(0.15f, 0.45f, 0.85f) : new Color(0.9f, 0.95f, 1f);
            }
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= productionInterval)
        {
            timer = 0f;
            storedCoins += coinsPerCycle;

            if (EcoSystemManager.Instance != null)
            {
                EcoSystemManager.Instance.ModifyEcoScore(ecoContribution * 0.2f);
            }
        }

        // Rüzgar türbini ise hafif salınım
        if (energyType == EnergyType.Wind && visualModel != null)
        {
            float sway = Mathf.Sin(Time.time * 2.2f) * 1.5f;
            visualModel.transform.localRotation = Quaternion.Euler(sway, 0f, 0f);
        }
    }

    public string GetInteractionPrompt()
    {
        string nameStr = energyType == EnergyType.Solar ? "Güneş Paneli" : "Rüzgar Türbini";
        if (storedCoins > 0)
        {
            return $"[E] {nameStr}: Üretilen +{storedCoins} Eko-Krediyi Topla";
        }
        int pct = Mathf.RoundToInt((timer / productionInterval) * 100f);
        return $"{nameStr} (Temiz Enerji Üretiyor: %{pct})";
    }

    public void Interact(GameObject interactor)
    {
        if (storedCoins > 0)
        {
            int c = storedCoins;
            storedCoins = 0;
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddCoins(c);
                PlayerInventory.Instance.Notify($"Temiz Enerjiden +{c} Eko-Kredi toplandı!");
            }
            if (GameAudioAndJuiceManager.Instance != null)
            {
                GameAudioAndJuiceManager.Instance.TriggerHarvestOomph(transform.position, $"+{c} Eko-Kredi", new Color(0.35f, 0.9f, 1f), transform);
            }
        }
        else
        {
            PlayerInventory.Instance?.Notify("Jeneratör şu an enerji depoluyor, birazdan tekrar gelin.");
        }
    }

    public Vector3 GetPosition() => transform.position;
}

