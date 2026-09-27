using Godot;

// Opens LevelSelectPanel to pick which level to play. Only the host picks - everyone else follows
// the host's level.
public partial class LevelSelectStation : StaticBody3D, IInteractable
{
    public string GetPromptText(PlayerCharacter character) =>
        NetworkSession.Instance.IsHost
            ? $"Press {InputPromptHelper.GetPrompt("interact")} to choose a level"
            : "The host chooses the level";

    public void Interact(PlayerCharacter character)
    {
        if (NetworkSession.Instance.IsHost)
        {
            LevelSelectPanel.Instance.Open();
        }
    }
}
