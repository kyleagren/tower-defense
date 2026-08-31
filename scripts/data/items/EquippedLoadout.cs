using Godot;

[GlobalClass]
public partial class EquippedLoadout : Resource
{
    [Export] public Weapon Weapon { get; set; }
    [Export] public Helmet Helmet { get; set; }
    [Export] public Chest Chest { get; set; }
    [Export] public Legs Legs { get; set; }
    [Export] public Boots Boots { get; set; }
    [Export] public Gloves Gloves { get; set; }
}
