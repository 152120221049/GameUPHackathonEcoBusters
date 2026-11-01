using System.Collections;
using UnityEngine;

/// <summary>
/// Kot farkı olan falezlerde, kayalıklarda veya nehir yataklarında oyuncunun
/// [E] tuşuna basarak yukarı tırmanmasını veya aşağı inmesini sağlayan etkileşim noktası.
/// "E ile gir, E ile çık" mantığında çalışır; fiziğe takılmadan pürüzsüz geçiş sunar.
/// </summary>
public class ClimbingPointInteractable : MonoBehaviour, IInteractable
{
    [Header("--- TIRMANMA NOKTALARI ---")]
    [Tooltip("Aşağıdaki zemin iniş/başlangıç noktası")]
    [SerializeField] private Vector3 lowerOffset = new Vector3(0, -1f, 0);
    [Tooltip("Yukarıdaki plato iniş/başlangıç noktası")]
    [SerializeField] private Vector3 upperOffset = new Vector3(0, 1.2f, 0);

    [Tooltip("Tırmanma geçiş süresi (saniye)")]
    [SerializeField] private float climbDuration = 0.55f;

    private bool isClimbing;

    public Vector3 LowerWorldPos => transform.position + lowerOffset;
    public Vector3 UpperWorldPos => transform.position + upperOffset;

    public string GetInteractionPrompt()
    {
        if (isClimbing) return "Tırmanılıyor...";

        var player = FindFirstObjectByType<CharacterController25D>();
        if (player == null) return "[E] Tırman";

        float distToLower = Vector3.Distance(player.transform.position, LowerWorldPos);
        float distToUpper = Vector3.Distance(player.transform.position, UpperWorldPos);

        return distToLower < distToUpper ? "[E] Yukarı Tırman" : "[E] Aşağı İn";
    }

    public void Interact(GameObject interactor)
    {
        if (isClimbing || interactor == null) return;

        float distToLower = Vector3.Distance(interactor.transform.position, LowerWorldPos);
        float distToUpper = Vector3.Distance(interactor.transform.position, UpperWorldPos);

        Vector3 targetPos = (distToLower < distToUpper) ? UpperWorldPos : LowerWorldPos;
        StartCoroutine(ClimbRoutine(interactor, targetPos));
    }

    private IEnumerator ClimbRoutine(GameObject playerObj, Vector3 destination)
    {
        isClimbing = true;
        Rigidbody rb = playerObj.GetComponent<Rigidbody>();
        CharacterController25D cc = playerObj.GetComponent<CharacterController25D>();

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        Vector3 startPos = playerObj.transform.position;
        float elapsed = 0f;

        while (elapsed < climbDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / climbDuration);

            // Yay şeklinde hafif yukarı kavislendirme
            float arc = Mathf.Sin(t * Mathf.PI) * 0.4f;
            Vector3 current = Vector3.Lerp(startPos, destination, t) + Vector3.up * arc;

            playerObj.transform.position = current;
            yield return null;
        }

        playerObj.transform.position = destination;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }

        isClimbing = false;
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(LowerWorldPos, 0.4f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(UpperWorldPos, 0.4f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(LowerWorldPos, UpperWorldPos);
    }
}

