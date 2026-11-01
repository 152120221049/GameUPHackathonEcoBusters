using UnityEngine;

/// <summary>
/// Şehir Bölgesi Eko-Topluluk ve Gönüllü Merkezi (City Recruitment Hub).
/// - Şehir halkı burada toplanır.
/// - Oyuncunun çevre eylemleriyle kazandığı "Topluluk İtibarı" (Community Reputation) arttıkça,
///   yeni gönüllü vatandaşlar davaya inanarak takıma katılmak için öne çıkar.
/// - Oyuncu [E] ile gönüllüyü ekibine katar; takipçi NPC olarak oyuncunun peşine takılır ve hurda taşımaya başlar.
/// </summary>
public class CityRecruitmentHub : MonoBehaviour, IInteractable
{
    [Header("--- GÖNÜLLÜ AYARLARI ---")]
    [Tooltip("Varsayılan gönüllü takipçi prefabı")]
    [SerializeField] private GameObject followerPrefab;
    [Tooltip("Kadın Gönüllü Takipçi Prefabı (FemNPC)")]
    [SerializeField] private GameObject femaleFollowerPrefab;
    [Tooltip("Erkek Gönüllü Takipçi Prefabı (MaleNPC)")]
    [SerializeField] private GameObject maleFollowerPrefab;
    [Tooltip("İlk takipçiyi açmak için gereken asgari itibar puanı")]
    [SerializeField] private float reputationPerFollower = 20f;
    [Tooltip("Maksimum alınabilecek takipçi sayısı")]
    [SerializeField] private int maxRecruitableFollowers = 4;

    private int recruitedCount = 0;

    public string GetInteractionPrompt()
    {
        var inv = PlayerInventory.Instance;
        if (inv != null && inv.HasItem(ItemType.CleanEnergyGenerator, 1))
        {
            return "[E] Belediye Binası (TownHall): Temiz Enerji Jeneratörü Kur (+150 Eko-Kredi & +25 İtibar!)";
        }

        float currentRep = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CommunityReputation : 0f;
        int eligibleCount = Mathf.Min(maxRecruitableFollowers, Mathf.FloorToInt(currentRep / reputationPerFollower));

        if (recruitedCount < eligibleCount)
        {
            return "[E] Belediye Binası (TownHall): Yeni Bir Eko-Gönüllüyü Takıma Kat!";
        }

        if (recruitedCount >= maxRecruitableFollowers)
        {
            return "Belediye Binası (Tüm Gönüllüler Sahada! Temiz Enerji Jeneratörü Getirerek Adayı Güçlendir)";
        }

        float nextRequired = (recruitedCount + 1) * reputationPerFollower;
        return $"Belediye Binası (Yeni Gönüllü İçin İtibar: {Mathf.RoundToInt(currentRep)}/{Mathf.RoundToInt(nextRequired)} veya Temiz Enerji Jeneratörü Getir)";
    }

    public void Interact(GameObject interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv != null && inv.TrySpendItem(ItemType.CleanEnergyGenerator, 1))
        {
            inv.AddCoins(150);
            EcoSystemManager.Instance?.RegisterCleanEnergyInstalled();
            return;
        }

        float currentRep = EcoSystemManager.Instance != null ? EcoSystemManager.Instance.CommunityReputation : 0f;
        int eligibleCount = Mathf.Min(maxRecruitableFollowers, Mathf.FloorToInt(currentRep / reputationPerFollower));

        if (recruitedCount < eligibleCount)
        {
            SpawnFollower();
        }
        else
        {
            float nextRequired = (recruitedCount + 1) * reputationPerFollower;
            PlayerInventory.Instance?.Notify($"Yeni takipçi için Halk İtibarını {nextRequired}'a çıkar veya Atölyede [Temiz Enerji Jeneratörü] üretip buraya kur!");
        }
    }

    private void SpawnFollower()
    {
        recruitedCount++;
        Vector3 spawnPos = transform.position + transform.forward * 2.5f + Vector3.right * (recruitedCount * 0.9f);
        spawnPos.y = 0f;

        // Kadın ve Erkek modelleri dönüşümlü doğur (çeşitlilik sağla)
        GameObject prefabToSpawn = followerPrefab;
        if (femaleFollowerPrefab != null && maleFollowerPrefab != null)
        {
            prefabToSpawn = (recruitedCount % 2 == 1) ? femaleFollowerPrefab : maleFollowerPrefab;
        }
        else if (femaleFollowerPrefab != null)
        {
            prefabToSpawn = femaleFollowerPrefab;
        }
        else if (maleFollowerPrefab != null)
        {
            prefabToSpawn = maleFollowerPrefab;
        }

        GameObject newFollower = null;
        if (prefabToSpawn != null)
        {
            newFollower = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        }
        else
        {
            // Primitif fallback takipçi oluştur
            newFollower = CreateProceduralFollower(spawnPos, $"Gönüllü #{recruitedCount}");
        }

        if (EcoSystemManager.Instance != null)
        {
            EcoSystemManager.Instance.RegisterFollowerRecruited();
        }

        PlayerInventory.Instance?.Notify($"Yeni bir Eko-Gönüllü takıma katıldı! Yerdeki hurdaları toplayıp üsse taşımanda sana yardım edecek.");
    }

    private GameObject CreateProceduralFollower(Vector3 pos, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = pos;

        // Görsel kök
        GameObject visual = new GameObject("VisualRoot");
        visual.transform.SetParent(obj.transform, false);

        // Gövde (Sevimli Voxel/Kutu adam)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.9f, 0.4f);

        var bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = new Color(0.2f, 0.8f, 0.4f); // Doğa dostu yeşil yelek
        }

        // Sırt Çantası / Taşıma Göstergesi
        GameObject carriedVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carriedVisual.name = "CarriedScrap";
        carriedVisual.transform.SetParent(visual.transform, false);
        carriedVisual.transform.localPosition = new Vector3(0f, 0.7f, -0.32f);
        carriedVisual.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        var carriedRenderer = carriedVisual.GetComponent<Renderer>();
        if (carriedRenderer != null)
        {
            carriedRenderer.material.color = new Color(0.9f, 0.75f, 0.2f); // Hurda sarısı
        }

        // Collider
        BoxCollider box = obj.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.5f, 0f);
        box.size = new Vector3(0.6f, 1.0f, 0.6f);

        // Follower AI
        FollowerNPC npc = obj.AddComponent<FollowerNPC>();

        return obj;
    }

    public Vector3 GetPosition() => transform.position;
}

