using Godot;

[GlobalClass]
[SaveTag("gloves")]
public partial class Gloves : Armor
{
    public override EquipmentSlot Slot => EquipmentSlot.Gloves;
}
