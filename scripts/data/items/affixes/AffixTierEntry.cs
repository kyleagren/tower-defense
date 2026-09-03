// One (Affix, tier) pairing from the flat weighted pool a roll actually draws from - see
// AffixTable.GetTierEntries. Not stored anywhere; built fresh each time a slot needs rolling.
public readonly struct AffixTierEntry
{
    public readonly Affix Affix;
    public readonly AffixTierRange Tier;

    public AffixTierEntry(Affix affix, AffixTierRange tier)
    {
        Affix = affix;
        Tier = tier;
    }
}
