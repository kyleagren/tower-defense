using Godot;

[GlobalClass]
public partial class Chest : Armor
{
    public override EquipmentSlot Slot => EquipmentSlot.Chest;
}
