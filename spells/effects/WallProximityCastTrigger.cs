using Godot;

// Casts once an enemy comes within Range of any point along the wall's line rather than near the
// Ritual, so a long wall spanning lanes still triggers.
[GlobalClass]
public partial class WallProximityCastTrigger : CastTrigger
{
    [Export] public float Range = 6f;

    public override bool ShouldCast(Ritual caster, SpellCastContext context)
    {
        if (context.Wall == null)
        {
            return false;
        }

        return EnemyTargeting.FindNearestToSegment(context.Wall.Start, context.Wall.End, Range) != null;
    }
}
