using Godot;

// Opens CharacterSelectPanel to pick which Sentinel to play as next.
public partial class CharacterSelectStation : StaticBody3D, IInteractable
{
    public string GetPromptText(PlayerCharacter character) => $"Press {InputPromptHelper.GetPrompt("interact")} to choose your Sentinel";

    public void Interact(PlayerCharacter character)
    {
        CharacterSelectPanel.Instance.Open(character.OwnerId);
    }
}
