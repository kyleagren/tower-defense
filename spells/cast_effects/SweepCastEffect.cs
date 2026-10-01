using Godot;

public class SweepCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        SweepCastData data = context.Sweep;

        if (data?.Scene == null || caster.CurrentTarget == null)
        {
            return;
        }

        // From the Ritual's base, flat toward the target - or along the Ritual's aim, for an enemy
        // standing right on top of it.
        Vector3 start = caster.GlobalPosition;
        Vector3 toward = caster.CurrentTarget.GlobalPosition - start;
        toward.Y = 0f;

        if (toward.LengthSquared() < 0.0001f)
        {
            toward = -spawnPoint.GlobalBasis.Z;
            toward.Y = 0f;
        }

        Vector3 direction = toward.Normalized();
        float radius = data.Radius * (context.Area?.Multiplier.Value ?? 1f);

        GroundSweep sweep = data.Scene.Instantiate<GroundSweep>();
        LevelContext.Current.LevelRoot.AddChild(sweep);

        // Announced before its first hit, which can already infuse it, so every copy exists before
        // any infusion reaches it.
        LevelContext.Current.SpellEffects.ShowSweep(sweep, data.Scene, start, direction, data.Speed.Value, radius, data.Duration.Value);
        sweep.Initialize(start, direction, data.Speed.Value, radius, data.Duration.Value, context.Damage, context.RagdollForce.Value, data.InfusionShare, context.SpellHit);
    }
}
