using System;
using UnityEngine;

/// <summary>
/// Oyuncu üssünde (PlayerBase) yer alan Hurdalık Atölyesi (Crafting Workbench).
/// IInteractable arayüzünü uygular; oyuncu yaklaşıp [E] tuşuna bastığında üretim menüsünü açar.
/// </summary>
public class WorkbenchInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string stationName = "Hurdalık Atölyesi";

    public static event Action<WorkbenchInteractable> OnWorkbenchToggled;

    public string GetInteractionPrompt()
    {
        return $"[E] {stationName} (Üretim Yap)";
    }

    public void Interact(GameObject interactor)
    {
        OnWorkbenchToggled?.Invoke(this);
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
}

