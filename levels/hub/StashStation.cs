using Godot;

// Opens StashPanel to store items beyond Inventory.
public partial class StashStation : StaticBody3D, IInteractable
{
    public string GetPromptText(PlayerCharacter character) => $"Press {InputPromptHelper.GetPrompt("interact")} to open the stash";

    public void Interact(PlayerCharacter character)
    {
        StashPanel.Instance.Open();
    }
}
