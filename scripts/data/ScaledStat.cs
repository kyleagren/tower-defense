// One stat resolved PoE-style: Base * (1 + sum of Increased) * (product of More).
// "Increased" sums across sources; "More" multiplies. Value is computed on read rather than
// baked in, so applying runes is idempotent — nothing overwrites a base value in place.
//
// Note: default(ScaledStat) leaves More at 0, which resolves everything to 0. Always initialize
// a field, either with the constructor or by assigning a float (see the implicit conversion).
public struct ScaledStat
{
    public float Base;
    public float Increased;
    public float More;

    public ScaledStat(float baseValue)
    {
        Base = baseValue;
        Increased = 0f;
        More = 1f;
    }

    // The combined modifier on its own, for cases that need to scale an extra amount the same
    // way the base is scaled (e.g. per-segment wall costs added after the fact).
    public readonly float Multiplier => (1f + Increased) * More;

    public readonly float Value
    {
        get
        {
#if DEBUG
            if (More == 0f)
            {
                Godot.GD.PushError("ScaledStat.More is 0 — this stat was left at default(ScaledStat). Initialize the field.");
            }
#endif
            return Base * Multiplier;
        }
    }

    public static implicit operator ScaledStat(float baseValue) => new ScaledStat(baseValue);
}
