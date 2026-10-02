using Godot;

// Opens VendorPanel to buy, sell, and buy back items.
public partial class VendorStation : PanelStation
{
    protected override Control Panel => VendorPanel.Instance;

    public override string GetPromptText(PlayerCharacter character) => $"Press {InputPromptHelper.GetPrompt("interact")} to shop";

    protected override void OpenPanel(PlayerCharacter character) => VendorPanel.Instance.Open();
}
