using Godot;

public partial class BombardmentCastEffect : CastEffect
{
    public override bool CanHandle(SpellCastContext context) => context.Bombardment != null;

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
        caster.GetTree().CurrentScene.AddChild(bombardment);

        // One authored total spread over however many ticks land inside the window, so the scene's
        // tick cadence stays a tuning knob rather than a damage multiplier.
        int tickCount = Mathf.Max(1, Mathf.RoundToInt(data.Duration.Value / bombardment.TickInterval));

        bombardment.Initialize(target, data.Radius * areaMultiplier, data.Duration.Value,
            context.Damage * (1f / tickCount), context.SpellHit);

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
        caster.GetTree().CurrentScene.AddChild(ground);
        ground.Initialize(target, data.Radius * areaMultiplier, data.Duration.Value);
    }
}
