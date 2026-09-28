using Godot;

[GlobalClass]
[SaveTag("rune_inventory_entry")]
public partial class RuneInventoryEntry : Resource
{
    [Export] public Rune Rune { get; set; }
    [Export] public bool Unlimited { get; set; } = true;
    [Export] public int Quantity { get; set; } = 0;
}
