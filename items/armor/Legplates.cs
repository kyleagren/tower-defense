using Godot;

[GlobalClass]
[SaveTag("legplates")]
public partial class Legplates : Armor
{
    public override EquipmentSlot Slot => EquipmentSlot.Legplates;
    protected override float SlotWeight => 2f;
}
