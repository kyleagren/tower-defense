using Godot;

// A minimal analog to BuildMenu for the hub's capsule station: pick which Sentinel to play as
// next, highlighting whichever one is already selected. Lives at the hub scene root (like
// InventoryPanel) rather than on any one character, since the active character gets destroyed
// and replaced by the very selection this panel makes - a panel owned by it would need to
// survive its own trigger.
public partial class CharacterSelectPanel : Control
{
    public static CharacterSelectPanel Instance { get; private set; }

    // Kept in lockstep with SentinelSpawner.ScenePathFor - only characters with a real scene
    // belong here.
    private static readonly CharacterId[] _selectableCharacters = { CharacterId.Soldier, CharacterId.Tank };

    [Export] public Control ListContainer { get; private set; }

    private PlayerId _owner;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public void Open(PlayerId owner)
    {
        _owner = owner;
        RefreshList();
        Visible = true;
        UiPanelStack.Push(this, Close);
    }

    private void RefreshList()
    {
        foreach (Node child in ListContainer.GetChildren())
        {
            child.QueueFree();
        }

        CharacterId current = PlayerProfiles.Get(_owner).SelectedCharacter;

        foreach (CharacterId character in _selectableCharacters)
        {
            bool isCurrent = character == current;

            Button button = new Button
            {
                Text = isCurrent ? $"{character} (current)" : character.ToString(),
                Disabled = isCurrent
            };

            if (!isCurrent)
            {
                button.Pressed += () => OnCharacterSelected(character);
            }

            ListContainer.AddChild(button);
        }
    }

    private void OnCharacterSelected(CharacterId character)
    {
        PlayerId owner = _owner;
        Close();
        LevelContext.Current.SentinelSpawners[owner].SelectSentinel(character);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !UiPanelStack.IsTop(this))
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            UiPanelStack.CloseAll();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Close()
    {
        Visible = false;
        UiPanelStack.Pop(this);
    }
}
