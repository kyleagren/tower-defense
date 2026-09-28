using Godot;

[GlobalClass]
[SaveTag("boots")]
public partial class Boots : Armor
{
    public override EquipmentSlot Slot => EquipmentSlot.Boots;
}
