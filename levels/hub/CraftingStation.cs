using Godot;

// Opens CraftingPanel to add/remove/reroll mods on an item.
public partial class CraftingStation : StaticBody3D, IInteractable
{
    public string GetPromptText(PlayerCharacter character) => $"Press {InputPromptHelper.GetPrompt("interact")} to craft";

    public void Interact(PlayerCharacter character)
    {
        CraftingPanel.Instance.Open();
    }
}
