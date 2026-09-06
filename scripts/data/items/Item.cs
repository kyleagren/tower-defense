using Godot;
using System.Collections.Generic;

public abstract partial class Item : Resource
{
    [Export] public Rarity Rarity { get; private set; }
    [Export] public Texture2D Icon { get; private set; }
    [Export] public Godot.Collections.Array<RolledAffix> Affixes { get; private set; } = new();

    // AnySentinel for the shared, universal armor/jewelry pool. A Weapon always overrides this to
    // one specific Sentinel. Architect can never appear here - CharacterId has no value for him at
    // all (see the Sentinel class, which is where character-identity and gear-scaled stats live).
    [Export] public CharacterId EquippableCharacter { get; private set; } = CharacterId.AnySentinel;

    public bool IsCompatibleWith(CharacterId character) => EquippableCharacter == character || EquippableCharacter == CharacterId.AnySentinel;

    public abstract EquipmentSlot Slot { get; }

    // The vendor's buy/sell/buyback price, for now just the number of mods rolled - simple stand-in
    // until pricing needs to account for rarity, tier, or anything else.
    public int SaleValue => Affixes.Count;

    // No hand-authored flavor names yet - just rarity + slot, matching the "name corresponds
    // to slot + rarity for now" call. The slot itself comes from the concrete type, not a
    // separate tag that would have to be kept in sync with it.
    public string Name => $"{Rarity} {GetType().Name}";

    // Name plus an "X Only" line if character-restricted - the shared header both the plain
    // tooltip and ItemTooltip's own structured advanced view build on top of.
    public string HeaderText => EquippableCharacter != CharacterId.AnySentinel
        ? $"{Name}\n{EquippableCharacter} Only"
        : Name;

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

    // Multi-line hover text: header, then one line per rolled mod via that mod's own
    // DescriptionTemplate. Shared by InventoryPanel, VendorPanel, and ItemInteractor so UI and
    // world tooltips read identically. ItemTooltip's own advanced (Alt-held) view builds its own
    // structured per-mod layout via AffixRowView instead of this, since it needs each mod grouped
    // by type with its tier shown separately, not folded into one string.
    public string BuildTooltip()
    {
        List<string> lines = new() { HeaderText };

        foreach (RolledAffix roll in Affixes)
        {
            lines.Add(roll.BuildDescription());
        }

        return string.Join("\n", lines);
    }
}
