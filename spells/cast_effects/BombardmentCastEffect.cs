using Godot;

public class BombardmentCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        BombardmentCastData data = context.Bombardment;

        if (data?.Scene == null || caster.CurrentTarget == null)
        {
            return;
        }

        Vector3 target = caster.CurrentTarget.GlobalPosition;
        float areaMultiplier = context.Area?.Multiplier.Value ?? 1f;

        Bombardment bombardment = data.Scene.Instantiate<Bombardment>();
        LevelContext.Current.LevelRoot.AddChild(bombardment);
        bombardment.Initialize(target, data.Radius * areaMultiplier, data.Duration.Value, context.Damage, context.SpellHit);

        SpawnGroundEffect(context, caster, target, areaMultiplier);
    }

    // Spawned beside the bombardment rather than by it: the two are independent, and a cast effect
    // that leaves ground behind without a bombardment reads the same component.
    private static void SpawnGroundEffect(SpellCastContext context, Ritual caster, Vector3 target, float areaMultiplier)
    {
        GroundEffectCastData data = context.GroundEffect;

        if (data?.Scene == null)
        {
            return;
        }

        GroundEffect ground = data.Scene.Instantiate<GroundEffect>();
        LevelContext.Current.LevelRoot.AddChild(ground);
        ground.Initialize(target, data.Radius * areaMultiplier, data.Duration.Value, data.Magnitude);
    }
}
