using Godot;

public partial class InteractionPrompt : Node3D
{
    [Export] public Label3D Label { get; private set; }

    // A separate label rather than a second line of Label: a Label3D has one color for all its
    // text, and a detail like a cost needs to turn red on its own.
    [Export] public Label3D DetailLabel { get; private set; }

    private string _currentText;

    public void ShowPrompt(Vector3 worldPosition, string text, string detail = null, Color? detailColor = null)
    {
        GlobalPosition = worldPosition;

        if (text != _currentText)
        {
            _currentText = text;
            Label.Text = text;
        }

        DetailLabel.Visible = !string.IsNullOrEmpty(detail);

        if (DetailLabel.Visible)
        {
            DetailLabel.Text = detail;
            DetailLabel.Modulate = detailColor ?? Colors.White;
        }

        Visible = true;
    }

    public void HidePrompt()
    {
        Visible = false;
    }
}
