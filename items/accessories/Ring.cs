using Godot;

// Slot-agnostic until equipped - a dropped ring doesn't know which hand it's for, so LeftRing and
// RightRing are just two EquippedLoadout slots holding this same type, not separate item classes.
[GlobalClass]
[SaveTag("ring")]
public partial class Ring : Accessory
{
    // Slot-agnostic in practice - AffixTable's accessory pool already allows both LeftRing and
    // RightRing, so which one this reports never actually changes affix eligibility.
    public override EquipmentSlot Slot => EquipmentSlot.LeftRing;
}
