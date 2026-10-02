using Godot;

[GlobalClass]
public partial class PlayerSaveData : Resource
{
    [Export] public int Level { get; set; } = 1;
    [Export] public int Xp { get; set; } = 0;
    [Export] public int Gold { get; set; } = 0;
    [Export] public Godot.Collections.Array<Spell> KnownSpells { get; set; } = new();
    [Export] public Godot.Collections.Array<RuneInventoryEntry> KnownRunes { get; set; } = new();
    // Each item under its top-left cell - see ItemGrid.
    [Export] public Godot.Collections.Dictionary<int, Item> Inventory { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<CharacterId, EquippedLoadout> Loadouts { get; set; } = new();
    [Export] public Godot.Collections.Array<FavoriteLoadout> Favorites { get; set; } = new();
    [Export] public Godot.Collections.Array<RuneLoadout> Tokens { get; set; } = new();

    // The pinned favorites wheel's slots, by favorite name - a name rather than the favorite itself,
    // since a favorite is written out in full wherever it appears and a second copy would load as a
    // separate object. An empty string is an empty slot.
    [Export] public Godot.Collections.Array<string> PinnedFavorites { get; set; } = new();

    // Set once the player places or removes a pin by hand; from then on new favorites no longer land
    // on the pinned wheel on their own.
    [Export] public bool PinnedWheelCustomized { get; set; }

    // Spells in the order the player arranged them on the Organize screen. Only the order within an
    // element matters, since each spell has one; a spell missing from it hasn't been arranged.
    [Export] public Godot.Collections.Array<Spell> SpellOrder { get; set; } = new();

    // Runes in the order the player arranged them on the Organize screen, one per rune - whichever
    // tier is stored, every tier of that rune takes its place.
    [Export] public Godot.Collections.Array<Rune> RuneOrder { get; set; } = new();

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

    public ItemGrid InventoryGrid => new ItemGrid(Inventory, GameRules.InventoryColumns, GameRules.InventoryRows);

    public EquippedLoadout GetOrCreateLoadout(CharacterId character)
    {
        if (!Loadouts.TryGetValue(character, out EquippedLoadout loadout))
        {
            loadout = new EquippedLoadout();
            Loadouts[character] = loadout;
        }

        return loadout;
    }

    // Pins a favorite to the first empty slot - unless it's already pinned, the wheel is full, or the
    // player has taken charge of the wheel.
    public void AutoPinFavorite(string name)
    {
        if (PinnedWheelCustomized || PinnedFavorites.Contains(name))
        {
            return;
        }

        int empty = PinnedFavorites.IndexOf("");

        if (empty >= 0)
        {
            PinnedFavorites[empty] = name;
        }
    }
}
