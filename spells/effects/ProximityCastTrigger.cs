using Godot;

[GlobalClass]
public partial class ProximityCastTrigger : CastTrigger
{
    [Export] public float Range = 6f;

    public override bool ShouldCast(Ritual caster, SpellCastContext context)
    {
        if (caster.WallStart == null || caster.WallEnd == null)
        {
            return false;
        }

        return EnemyTargeting.FindNearestToSegment(caster.WallStart.Value, caster.WallEnd.Value, Range) != null;
    }
}
