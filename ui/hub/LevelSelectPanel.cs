using Godot;

// A minimal analog to CharacterSelectPanel for the hub's cube station: pick which level to play.
// Lives at the hub scene root, same reasoning as CharacterSelectPanel - though here it's even
// more clear-cut, since starting a level unloads the hub scene entirely.
public partial class LevelSelectPanel : Control
{
    public static LevelSelectPanel Instance { get; private set; }

    // Grows alongside the level roster - one entry per playable level.
    private static readonly (string Name, string ScenePath)[] _levels =
    {
        ("Test Level", "uid://vqr88ehteuiw")
    };

    [Export] public Control ListContainer { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public void Open()
    {
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

        foreach ((string name, string scenePath) in _levels)
        {
            Button button = new Button { Text = name };
            button.Pressed += () => OnLevelSelected(scenePath);
            ListContainer.AddChild(button);
        }
    }

    private void OnLevelSelected(string scenePath)
    {
        GetTree().ChangeSceneToFile(scenePath);
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
