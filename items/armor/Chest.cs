using Godot;

[GlobalClass]
[SaveTag("chest")]
public partial class Chest : Armor
{
    public override EquipmentSlot Slot => EquipmentSlot.Chest;
    protected override float SlotWeight => 2f;
}
