using Godot;

[GlobalClass]
[SaveTag("belt")]
public partial class Belt : Accessory
{
    public override EquipmentSlot Slot => EquipmentSlot.Belt;
}
