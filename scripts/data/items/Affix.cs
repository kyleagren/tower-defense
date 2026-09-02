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
}
