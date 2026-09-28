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

    // The hub stash's tabs, in order. Every profile has at least one, which is free.
    [Export] public Godot.Collections.Array<StashTab> Stash { get; set; } = new();

    // Shown only to this profile's owner, in the profile picker.
    [Export] public string ProfileName { get; set; } = "";

    // Unix seconds. Orders the picker, and picks the profile a menu-skipping boot or an invite plays on.
    [Export] public long LastPlayedUnix { get; set; }

    // The Sentinels this profile has already been given a starter weapon for - each gets one, once.
    [Export] public Godot.Collections.Array<CharacterId> StarterWeaponsGiven { get; set; } = new();

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
