using Godot;

[GlobalClass]
[SaveTag("amulet")]
public partial class Amulet : Accessory
{
    public override EquipmentSlot Slot => EquipmentSlot.Amulet;
}
