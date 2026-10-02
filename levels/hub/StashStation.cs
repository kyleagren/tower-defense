using Godot;

// Opens StashPanel to store items beyond Inventory.
public partial class StashStation : PanelStation
{
    protected override Control Panel => StashPanel.Instance;

    public override string GetPromptText(PlayerCharacter character) => $"Press {InputPromptHelper.GetPrompt("interact")} to open the stash";

    protected override void OpenPanel(PlayerCharacter character) => StashPanel.Instance.Open();
}
