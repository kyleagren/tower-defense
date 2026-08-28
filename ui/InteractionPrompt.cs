using Godot;

public partial class InteractionPrompt : Node3D
{
    [Export] public Label3D Label { get; private set; }

    private string currentText;

    public void ShowPrompt(Vector3 worldPosition, string text)
    {
        GlobalPosition = worldPosition;

        if (text != currentText)
        {
            currentText = text;
            Label.Text = text;
        }

        Visible = true;
    }

    public void HidePrompt()
    {
        Visible = false;
    }
}
