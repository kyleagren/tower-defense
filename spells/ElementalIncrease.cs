// The elemental sibling of ScaledStat.Increased - one Increased% per element instead of one flat
// number. Its own type, not DamagePacket, so it's never confused with an actual damage amount.
// Every field defaults to 0, which is already a safe no-op (unlike ScaledStat, where a missing
// More would zero out everything).
public struct ElementalIncrease
{
    public float Physical;
    public float Fire;
    public float Water;
    public float Cold;
    public float Lightning;
    public float Wind;
}
