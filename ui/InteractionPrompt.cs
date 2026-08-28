using Godot;

public partial class InteractionPrompt : Node3D
{
    [Export] public Label3D Label { get; private set; }

    private string _currentText;

    public void ShowPrompt(Vector3 worldPosition, string text)
    {
        GlobalPosition = worldPosition;

        if (text != _currentText)
        {
            _currentText = text;
            Label.Text = text;
        }

        Visible = true;
    }

    public void HidePrompt()
    {
        Visible = false;
    }
}
