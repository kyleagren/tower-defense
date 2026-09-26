using Godot;

// Opens VendorPanel to buy, sell, and buy back items.
public partial class VendorStation : StaticBody3D, IInteractable
{
    public string GetPromptText(PlayerCharacter character) => $"Press {InputPromptHelper.GetPrompt("interact")} to shop";

    public void Interact(PlayerCharacter character)
    {
        VendorPanel.Instance.Open();
    }
}
