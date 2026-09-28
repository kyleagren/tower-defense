using Godot;

[GlobalClass]
[SaveTag("helmet")]
public partial class Helmet : Armor
{
    public override EquipmentSlot Slot => EquipmentSlot.Helmet;
}
