using Godot;

// A world object a StationInteractor can look at and press "interact" on - the shared shape
// behind every station (ArchitectSelectStation, CharacterSelectStation, and any future one), so
// one interactor component handles all of them instead of one dedicated class per station type.
public interface IInteractable
{
    string GetPromptText(PlayerCharacter character);
    void Interact(PlayerCharacter character);
}
