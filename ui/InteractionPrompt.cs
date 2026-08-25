using Godot;

public partial class InteractionPrompt : Node3D
{
    [Export] public Label3D Label { get; private set; }

    public void ShowPrompt(Vector3 worldPosition, string text)
    {
        GlobalPosition = worldPosition;
        Label.Text = text;
        Visible = true;
    }

    public void HidePrompt()
    {
        Visible = false;
    }
}
