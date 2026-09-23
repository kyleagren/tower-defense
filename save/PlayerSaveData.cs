using Godot;

[GlobalClass]
public partial class PlayerSaveData : Resource
{
    [Export] public int Level { get; set; } = 1;
    [Export] public int Xp { get; set; } = 0;
    [Export] public int Gold { get; set; } = 0;
    [Export] public Godot.Collections.Array<Spell> KnownSpells { get; set; } = new();
    [Export] public Godot.Collections.Array<RuneInventoryEntry> KnownRunes { get; set; } = new();
    [Export] public Godot.Collections.Array<Item> Inventory { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<CharacterId, EquippedLoadout> Loadouts { get; set; } = new();
    [Export] public Godot.Collections.Array<FavoriteLoadout> Favorites { get; set; } = new();
    [Export] public Godot.Collections.Array<RuneLoadout> Tokens { get; set; } = new();

    // The item currently sitting on the crafting table, if any - separate from Inventory so it
    // survives a crash mid-crafting-session (saved immediately, not debounced, after every change).
    [Export] public Item CraftingSlotItem { get; set; }

    // Which Sentinel this player played most recently - also the sensible starting pick for a
    // brand-new save. Read/written through PlayerProfile, not directly.
    [Export] public CharacterId LastPlayedCharacter { get; set; } = CharacterId.Soldier;

    public EquippedLoadout GetOrCreateLoadout(CharacterId character)
    {
        if (!Loadouts.TryGetValue(character, out EquippedLoadout loadout))
        {
            loadout = new EquippedLoadout();
            Loadouts[character] = loadout;
        }

        return loadout;
    }
}
