using Godot;

[GlobalClass]
public partial class Modifier : Resource
{
    [Export] public string Name { get; private set; }
    [Export] public string Description { get; private set; }
    [Export] public ModifierType ModifierType { get; private set; }
    [Export] public int Tier { get; private set; } = 5;

    // Empty means valid on any slot. Non-empty restricts which items this mod can roll on or be
    // crafted onto (e.g. movement speed only on Boots).
    [Export] public Godot.Collections.Array<EquipmentSlot> AllowedSlots { get; private set; } = new();

    // AnySentinel means this mod is a generic stat roll usable by any Sentinel. Set to a specific
    // character only for bespoke mods that reference that character's own kit - the item itself
    // still equips fine on anyone, this mod just isn't meaningful for someone else.
    [Export] public CharacterId RestrictedCharacter { get; private set; } = CharacterId.AnySentinel;

    [Export] public StatId Stat { get; private set; }
    [Export] public float Value { get; private set; }
}
