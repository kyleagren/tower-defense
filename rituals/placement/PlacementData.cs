// Whatever a spell's PlacementFlow picked beyond the Ritual's own position. Kept by the Ritual's
// SpellInstance for its whole life and folded into every resolve, so the cast, any trigger and the
// cost that was paid all read the same values. Null for a spell placed with nothing but the Ritual.
public abstract class PlacementData
{
    public abstract void ApplyTo(SpellCastContext context);
}
