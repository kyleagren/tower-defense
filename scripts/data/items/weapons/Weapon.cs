using Godot;

// Gun and SwordAndShield both occupy the same equipment slot, unlike Armor's subclasses (which
// each map to a different one) - Slot lives here on the shared base instead of being repeated
// identically on both leaves.
public abstract partial class Weapon : Item
{
    public override EquipmentSlot Slot => EquipmentSlot.Weapon;
}
