using Godot;

public partial class SpellGrid : GridContainer
{
    [Export] public int Rows { get; private set; } = 4;
}
