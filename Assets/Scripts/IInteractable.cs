public interface IInteractable
{
    /// <summary>
    /// Called when the player interacts with this object
    /// </summary>
    /// <param name="context">Player context containing transform, camera, and motor</param>
    void OnInteract(PlayerContext context);

    /// <summary>
    /// Optional: Returns whether this object can currently be interacted with
    /// </summary>
    bool CanInteract();
}