using Godot;

// Slot-agnostic until equipped - a dropped ring doesn't know which hand it's for, so LeftRing and
// RightRing are just two EquippedLoadout slots holding this same type, not separate item classes.
[GlobalClass]
public partial class Ring : Accessory
{
}
