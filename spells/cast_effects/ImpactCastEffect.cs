using Godot;

// Marks a spot and lands one blast on it after a delay: whatever stands there when it lands is hit,
// not whatever stood there when it was cast.
public class ImpactCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        ImpactCastData data = context.Impact;

        Vector3? point = caster.LandingPoint(context);

        if (data?.Scene == null || point == null)
        {
            return;
        }

        float radius = data.Radius * (context.Area?.Multiplier.Value ?? 1f);

        DelayedImpact impact = data.Scene.Instantiate<DelayedImpact>();
        LevelContext.Current.LevelRoot.AddChild(impact);
        impact.Initialize(point.Value, radius, data.Delay, context.Damage, context.RagdollForce.Value, context.SpellHit);
        LevelContext.Current.SpellEffects.ShowImpact(data.Scene, point.Value, radius, data.Delay);
    }
}
