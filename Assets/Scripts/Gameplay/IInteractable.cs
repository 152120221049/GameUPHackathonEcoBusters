using UnityEngine;

/// <summary>
/// Oyuncunun [E] tuşu ile etkileşime girebildiği tüm nesne, istasyon, makine ve NPC'lerin ortak arayüzü.
/// </summary>
public interface IInteractable
{
    string GetInteractionPrompt();
    void Interact(GameObject interactor);
    Vector3 GetPosition();
}

