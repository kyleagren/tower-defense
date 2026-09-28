using Godot;

[GlobalClass]
[SaveTag("relic")]
public partial class Relic : Accessory
{
    public override EquipmentSlot Slot => EquipmentSlot.Relic;
}
