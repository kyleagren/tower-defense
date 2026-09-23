using Godot;

// The two endpoints picked by WallPlacementFlow. Folds them into the resolved context along with
// the per-segment cost they imply, kept as placement cost so repair can leave it out.
public class WallPlacementData : PlacementData
{
    public WallPlacementData(Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
    }

    public Vector3 Start { get; }
    public Vector3 End { get; }

    public override void ApplyTo(SpellCastContext context)
    {
        if (context.Wall == null)
        {
            return;
        }

        int chargedSegments = IceWall.ChargedSegmentCount(
            IceWall.ComputeSegmentCount(Start.DistanceTo(End), context.Wall.SegmentWidth));

        context.Wall.Start = Start;
        context.Wall.End = End;
        context.PlacementManaCost += chargedSegments * context.Wall.ManaCostPerSegment;
        context.PlacementCapacityCost += chargedSegments * context.Wall.CapacityCostPerSegment;
    }
}
