using Godot;

public abstract partial class Item : Resource
{
    [Export] public Rarity Rarity { get; private set; }
    [Export] public Texture2D Icon { get; private set; }
    [Export] public Godot.Collections.Array<RolledAffix> Affixes { get; private set; } = new();

    // AnySentinel for the shared, universal armor/jewelry pool. A Weapon always overrides this to
    // one specific Sentinel. Architect can never appear here - CharacterId has no value for him at
    // all (see the Sentinel class, which is where character-identity and gear-scaled stats live).
    [Export] public CharacterId EquippableCharacter { get; private set; } = CharacterId.AnySentinel;

    // No hand-authored flavor names yet - just rarity + slot, matching the "name corresponds
    // to slot + rarity for now" call. The slot itself comes from the concrete type, not a
    // separate tag that would have to be kept in sync with it.
    public string Name => $"{Rarity} {GetType().Name}";

    // The one place code (LootGenerator) is allowed to fill in a generated item's fields - keeps
    // the properties above effectively read-only to everyone else. Godot's own .tres loader
    // doesn't go through this at all (it sets exported properties directly via its own reflection,
    // bypassing normal C# access rules), so hand-authored items are unaffected.
    public void Initialize(Rarity rarity, Texture2D icon, CharacterId equippableCharacter, Godot.Collections.Array<RolledAffix> affixes)
    {
        Rarity = rarity;
        Icon = icon;
        EquippableCharacter = equippableCharacter;
        Affixes = affixes;
    }
}
