using Godot;

[GlobalClass]
public partial class Weapon : Item
{
    // Which character this weapon belongs to - unlike Armor, Weapon is always restricted to one
    // specific character, never shared across the roster.
    [Export] public CharacterId EquippableCharacter { get; private set; }
}
