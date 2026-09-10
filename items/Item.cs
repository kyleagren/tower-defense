using Godot;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public abstract partial class Item : Resource
{
    [Export] public Rarity Rarity { get; private set; }
    [Export] public Texture2D Icon { get; private set; }
    [Export] public Godot.Collections.Array<RolledAffix> Affixes { get; private set; } = new();
    [Export] public string FlavorName { get; private set; }

    // AnySentinel for the shared, universal armor/jewelry pool. A Weapon always overrides this to
    // one specific Sentinel. Architect can never appear here - CharacterId has no value for him at
    // all (see the Sentinel class, which is where character-identity and gear-scaled stats live).
    [Export] public CharacterId EquippableCharacter { get; private set; } = CharacterId.AnySentinel;

    public bool IsCompatibleWith(CharacterId character) => EquippableCharacter == character || EquippableCharacter == CharacterId.AnySentinel;

    public abstract EquipmentSlot Slot { get; }

    // The vendor's buy/sell/buyback price, for now just the number of mods rolled - simple stand-in
    // until pricing needs to account for rarity, tier, or anything else.
    public virtual int SaleValue => Affixes.Count;

    // The mechanical descriptor (e.g. "Rare Boots"), as opposed to FlavorName below, which is the
    // actual display name. The slot comes from the concrete type rather than a separate tag that
    // would have to be kept in sync with it.
    public virtual string TypeLabel => SpacedTypeName();

    // The item's real display name - a generated flavor title for common drops (NameGenerator), or
    // a hand-authored one for uniques/tres items. Falls back to TypeLabel if nothing was ever set.
    public virtual string Name => string.IsNullOrEmpty(FlavorName) ? TypeLabel : FlavorName;

    // Splits a PascalCase concrete class name into words ("SwordAndShield" -> "Sword And Shield")
    // for player-facing display - matters for multi-word Item subclasses; single-word ones
    // (Helmet, Gun, ...) pass through unchanged.
    protected string SpacedTypeName()
    {
        return Regex.Replace(GetType().Name, "(?<!^)([A-Z])", " $1");
    }

    // Name first, then TypeLabel right below it if a FlavorName is actually in play (skipped
    // otherwise, so an un-flavored item doesn't show the same line twice), then an "X Only" line
    // if character-restricted - the shared header both the plain tooltip and ItemTooltip's own
    // structured advanced view build on top of.
    public string HeaderText
    {
        get
        {
            List<string> lines = new() { Name };

            if (!string.IsNullOrEmpty(FlavorName))
            {
                lines.Add(TypeLabel);
            }

            if (EquippableCharacter != CharacterId.AnySentinel)
            {
                lines.Add($"{EquippableCharacter} Only");
            }

            return string.Join("\n", lines);
        }
    }

    // The one place code (LootGenerator) is allowed to fill in a generated item's fields - keeps
    // the properties above effectively read-only to everyone else. Godot's own .tres loader
    // doesn't go through this at all (it sets exported properties directly via its own reflection,
    // bypassing normal C# access rules), so hand-authored items are unaffected.
    public void Initialize(Rarity rarity, Texture2D icon, CharacterId equippableCharacter, Godot.Collections.Array<RolledAffix> affixes, string flavorName = null)
    {
        Rarity = rarity;
        Icon = icon;
        EquippableCharacter = equippableCharacter;
        Affixes = affixes;
        FlavorName = flavorName;
    }

    // Multi-line hover text: header, then one line per rolled mod via that mod's own
    // DescriptionTemplate. Shared by InventoryPanel, VendorPanel, and ItemInteractor so UI and
    // world tooltips read identically. ItemTooltip's own advanced (Alt-held) view builds its own
    // structured per-mod layout via AffixRowView instead of this, since it needs each mod grouped
    // by type with its tier shown separately, not folded into one string.
    public virtual string BuildTooltip()
    {
        List<string> lines = new() { HeaderText };

        foreach (RolledAffix roll in Affixes)
        {
            lines.Add(roll.BuildDescription());
        }

        return string.Join("\n", lines);
    }
}
