using Godot;

// The spot picked for a Ritual that always lands in one place (FixedSpotRune).
public class SpotPlacementData : PlacementData
{
    public const string NetworkKind = "spot";

    public SpotPlacementData(Vector3 point)
    {
        Point = point;
    }

    public Vector3 Point { get; }

    public override void ApplyTo(SpellCastContext context)
    {
        if (context.FixedSpot != null)
        {
            context.FixedSpot.Point = Point;
        }
    }

    public override Godot.Collections.Array ToNetwork() => new() { NetworkKind, Point };

    public static SpotPlacementData Decode(Godot.Collections.Array data) => new(data[1].AsVector3());
}
