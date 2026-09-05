using Godot;

// Opens LevelSelectPanel to pick which level to play.
public partial class LevelSelectStation : StaticBody3D, IInteractable
{
    public string GetPromptText(PlayerCharacter character) => "Press E to choose a level";

    public void Interact(PlayerCharacter character)
    {
        LevelSelectPanel.Instance.Open();
    }
}
