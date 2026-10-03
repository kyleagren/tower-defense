// Whatever a spell's PlacementFlow picked beyond the Ritual's own position. Kept by the Ritual's
// SpellInstance for its whole life and folded into every resolve, so the cast, any trigger and the
// cost that was paid all read the same values. Null for a spell placed with nothing but the Ritual.
public abstract class PlacementData
{
    public abstract void ApplyTo(SpellCastContext context);

    // Anything the placement leaves on the Ritual itself. Runs on every machine, from Configure.
    public virtual void AttachTo(Ritual ritual, SpellCastContext context)
    {
    }

    // Sent as an array led by the kind of placement - an empty one for none.
    public abstract Godot.Collections.Array ToNetwork();

    public static PlacementData FromNetwork(Godot.Collections.Array data)
    {
        if (data.Count == 0)
        {
            return null;
        }

        return data[0].AsString() switch
        {
            WallPlacementData.NetworkKind => WallPlacementData.Decode(data),
            AimPlacementData.NetworkKind => AimPlacementData.Decode(data),
            SpotPlacementData.NetworkKind => SpotPlacementData.Decode(data),
            _ => null
        };
    }
}
