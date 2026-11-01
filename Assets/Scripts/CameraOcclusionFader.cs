using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2.5D İzometrik Görüş Engelleyici Şeffaflaştırıcı (Occlusion Fader).
/// Karakter ile kamera arasına giren binaların, ağaçların ve nesnelerin
/// opaklığını (Alpha) yumuşakça azaltarak karakterin daima görünür kalmasını sağlar.
/// Karakter engelin arkasından çıktığında nesne eski opaklığına geri döner.
/// </summary>
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("--- HEDEF TAKİBİ ---")]
    [Tooltip("Görüşün odaklandığı karakter (Boşsa otomatik bulur)")]
    [SerializeField] private Transform target;
    [Tooltip("Karakterin göğüs/baş hizası için dikey ofset")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("--- ŞEFFAFLAŞTIRMA (FADE) AYARLARI ---")]
    [Tooltip("Engelin arkasındayken ulaşacağı hedef opaklık (0.0 = Tamamen görünmez, 0.3 = Hafif hayalet)")]
    [Range(0.05f, 0.8f)]
    [SerializeField] private float fadedAlpha = 0.25f;
    [Tooltip("Opaklığın değişme hızı")]
    [SerializeField] private float fadeSpeed = 7f;
    [Tooltip("Raycast ışınının kalınlığı (SphereCast)")]
    [SerializeField] private float rayRadius = 0.45f;

    [Header("--- FİLTRE VE KATMANLAR ---")]
    [Tooltip("Engelleri tespit ederken kontrol edilecek katmanlar")]
    [SerializeField] private LayerMask obstacleLayer = ~0;
    [Tooltip("Zemin karolarını yanlışlıkla şeffaflaştırmayı engeller")]
    [SerializeField] private bool ignoreGroundTiles = true;

    // Şeffaflaştırılan nesneleri takip eden iç sınıf
    private class FadeTarget
    {
        public Renderer renderer;
        public Material materialInstance;
        public Color originalColor;
        public float currentAlpha;
        public float targetAlpha;
        public int shaderPropertyId;
    }

    private readonly Dictionary<Renderer, FadeTarget> activeFaders = new Dictionary<Renderer, FadeTarget>();
    private readonly HashSet<Renderer> currentHits = new HashSet<Renderer>();

    private void Start()
    {
        if (target == null || !target.gameObject.scene.IsValid())
        {
            var pc = FindFirstObjectByType<CharacterController25D>();
            if (pc != null) target = pc.transform;
            else
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) target = tagged.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        CheckOcclusion();
        UpdateFading();
    }

    private void CheckOcclusion()
    {
        currentHits.Clear();

        Vector3 cameraPos = transform.position;
        Vector3 targetPos = target.position + targetOffset;
        Vector3 direction = targetPos - cameraPos;
        float distance = direction.magnitude;

        if (distance < 0.1f) return;

        // Kamera ile oyuncu arasındaki tüm engelleri tara
        RaycastHit[] hits = Physics.SphereCastAll(cameraPos, rayRadius, direction.normalized, distance, obstacleLayer, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            // Karakterin kendisini şeffaflaştırma
            if (hit.transform == target || hit.transform.IsChildOf(target))
                continue;

            // Zemin tile'ı veya toplanabilir eşyaları şeffaflaştırma (parlayıp sönmeyi önler)
            if (hit.transform.GetComponentInParent<GroundTile>() != null || hit.transform.GetComponent<GroundTile>() != null)
                continue;

            if (hit.transform.GetComponentInParent<CollectibleItem>() != null || hit.transform.GetComponent<CollectibleItem>() != null)
                continue;

            // Atölye masasını da şeffaflaştırma
            if (hit.transform.GetComponentInParent<WorkbenchInteractable>() != null || hit.transform.GetComponent<WorkbenchInteractable>() != null)
                continue;

            Renderer rend = hit.transform.GetComponent<Renderer>();
            if (rend != null && rend.enabled && rend.sharedMaterial != null)
            {
                if (!rend.sharedMaterial.HasProperty("_BaseColor") && !rend.sharedMaterial.HasProperty("_Color"))
                    continue;

                currentHits.Add(rend);

                if (!activeFaders.TryGetValue(rend, out var fader))
                {
                    // Yeni bir engel tespit edildi
                    Material mat = rend.material; // Instance oluşturur
                    int propId = mat.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : Shader.PropertyToID("_Color");
                    Color origCol = mat.HasProperty(propId) ? mat.GetColor(propId) : Color.white;

                    fader = new FadeTarget
                    {
                        renderer = rend,
                        materialInstance = mat,
                        originalColor = origCol,
                        currentAlpha = origCol.a,
                        targetAlpha = fadedAlpha,
                        shaderPropertyId = propId
                    };

                    EnableTransparency(mat);
                    activeFaders[rend] = fader;
                }
                else
                {
                    fader.targetAlpha = fadedAlpha;
                }
            }
        }

        // Artık engellemeyen eski nesneleri normale döndür
        foreach (var kvp in activeFaders)
        {
            if (!currentHits.Contains(kvp.Key))
            {
                kvp.Value.targetAlpha = kvp.Value.originalColor.a;
            }
        }
    }

    private void UpdateFading()
    {
        List<Renderer> toRemove = null;

        foreach (var kvp in activeFaders)
        {
            FadeTarget fader = kvp.Value;
            if (fader.renderer == null || fader.materialInstance == null)
            {
                if (toRemove == null) toRemove = new List<Renderer>();
                toRemove.Add(kvp.Key);
                continue;
            }

            fader.currentAlpha = Mathf.MoveTowards(fader.currentAlpha, fader.targetAlpha, fadeSpeed * Time.deltaTime);

            Color col = fader.originalColor;
            col.a = fader.currentAlpha;
            fader.materialInstance.SetColor(fader.shaderPropertyId, col);

            // Eğer orijinal opaklığına ulaştıysa ve artık engel değilse listeden çıkar
            if (!currentHits.Contains(kvp.Key) && Mathf.Approximately(fader.currentAlpha, fader.originalColor.a))
            {
                if (toRemove == null) toRemove = new List<Renderer>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                activeFaders.Remove(toRemove[i]);
            }
        }
    }

    private void EnableTransparency(Material mat)
    {
        // URP Lit Shader şeffaflık modunu etkinleştir
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            // Standart Standalone shader şeffaflığı
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.magenta;
            Vector3 targetPos = target.position + targetOffset;
            Gizmos.DrawLine(transform.position, targetPos);
            Gizmos.DrawWireSphere(targetPos, rayRadius);
        }
    }
}

