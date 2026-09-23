using Godot;

// The toggle: press it as a Sentinel to become the Architect for DPS testing, press it again as
// the Architect to return to whichever Sentinel is actually selected.
public partial class ArchitectSelectStation : StaticBody3D, IInteractable
{
    public string GetPromptText(PlayerCharacter character) =>
        character is Architect ? "Press E to return to your Sentinel" : "Press E to test as Architect";

    public void Interact(PlayerCharacter character)
    {
        SentinelSpawner spawner = LevelContext.Current.SentinelSpawners[character.OwnerId];

        if (character is Architect)
        {
            spawner.SelectSentinel(PlayerSentinels.Get(character.OwnerId));
        }
        else
        {
            spawner.SelectArchitect();
        }
    }
}
