using Godot;

public partial class SigilPreview : Control
{
    [Export] public Line2D SigilLine { get; private set; }
    [Export] public LineEdit NameInput { get; private set; }
    [Export] public float Radius = 150f;

    public override void _Ready()
    {
        NameInput.TextChanged += OnTextChanged;
        SigilLine.Position = Size / 2f;
        OnTextChanged(NameInput.Text);
    }

    private void OnTextChanged(string newText)
    {
        SigilLine.Points = SigilGenerator.GeneratePoints(newText, Vector2.Zero, Radius);
    }
}
