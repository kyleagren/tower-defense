using Godot;

[GlobalClass]
public partial class FavoriteLoadout : RuneLoadout
{
    [Export] public string Name { get; set; } = "";
}
