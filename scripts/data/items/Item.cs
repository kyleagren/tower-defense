using Godot;

public abstract partial class Item : Resource
{
    [Export] public Rarity Rarity { get; private set; }
    [Export] public Texture2D Icon { get; private set; }
    [Export] public Godot.Collections.Array<RolledAffix> Affixes { get; private set; } = new();

    // AnySentinel for the shared Sentinel armor pool. A Weapon always overrides this to one
    // specific character; Armor normally leaves it at the default, except the Architect's own
    // armor, which gets explicitly tagged CharacterId.Architect to keep his pool separate from
    // the Sentinel one entirely, rather than mixing in the same shared armor list.
    [Export] public CharacterId EquippableCharacter { get; private set; } = CharacterId.AnySentinel;

    // No hand-authored flavor names yet - just rarity + slot, matching the "name corresponds
    // to slot + rarity for now" call. The slot itself comes from the concrete type, not a
    // separate tag that would have to be kept in sync with it.
    public string Name => $"{Rarity} {GetType().Name}";
}
