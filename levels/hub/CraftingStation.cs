using Godot;

// Opens CraftingPanel to add/remove/reroll mods on an item.
public partial class CraftingStation : PanelStation
{
    protected override Control Panel => CraftingPanel.Instance;

    public override string GetPromptText(PlayerCharacter character) => $"Press {InputPromptHelper.GetPrompt("interact")} to craft";

    protected override void OpenPanel(PlayerCharacter character) => CraftingPanel.Instance.Open();
}
