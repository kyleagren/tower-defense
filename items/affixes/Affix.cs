// The shared, hand-authored definition of a mod - everything that's identical across every roll
// of it. What actually varies per item (Tier, Value) lives on RolledAffix instead.
public class Affix
{
    public string Name;
    public string DescriptionTemplate;
    public AffixType AffixType;
    public StatId Stat;
    public EquipmentSlot[] AllowedSlots = System.Array.Empty<EquipmentSlot>();
    public CharacterId RestrictedCharacter = CharacterId.AnySentinel;
    public AffixTierRange[] TierTable = System.Array.Empty<AffixTierRange>();
    public AffixTag[] Tags = System.Array.Empty<AffixTag>();
    public bool IsFlatValue;
    public AffixGroup Group = AffixGroup.None;

    // The ability slot a per-slot mod belongs to, or -1. Its template carries an {ability} token for
    // that slot's name.
    public int AbilitySlot = -1;

    // The template with {ability} filled in from the player's selected Sentinel's abilities in that
    // slot, so the same item reads "Converge / Repel Duration" on a Soldier and names Tank's slot on
    // a Tank - whether or not that Sentinel is in the level right now.
    public string ResolvedTemplate => AbilitySlot < 0
        ? DescriptionTemplate
        : DescriptionTemplate.Replace("{ability}",
            SentinelCatalog.SlotName(PlayerProfiles.Local.SelectedCharacter, AbilitySlot) ?? $"Ability {AbilitySlot + 1}");

    public bool ConflictsWith(Affix other) => other.Stat == Stat || (Group != AffixGroup.None && other.Group == Group);

    // The character half of "can this mod roll here", parallel to AllowedSlots' slot half. An
    // unrestricted mod rolls on anything; a restricted one only on an item already bound to that
    // same character, so an unbound (AnySentinel) item can never pick up a character-specific mod.
    // Nothing in AffixTable sets this yet - it's the lever for character-signature weapon mods.
    public bool IsAvailableTo(CharacterId character)
    {
        return RestrictedCharacter == CharacterId.AnySentinel || RestrictedCharacter == character;
    }
}
