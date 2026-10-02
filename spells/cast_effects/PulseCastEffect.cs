using Godot;

// Arcs out from the Ritual to its best few targets, several times in quick succession. Targets are
// re-picked every pulse, so a kill hands its share to the next in line. Pulses after the first wait
// on a physics-step timer that pauses with the game, and stop if the Ritual is gone.
public class PulseCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        PulseCastData data = context.Pulse;

        if (data == null)
        {
            return;
        }

        Pulse(context, spawnPoint);

        for (int i = 1; i < data.PulseCount; i++)
        {
            spawnPoint.GetTree().CreateTimer(data.PulseInterval * i, processAlways: false, processInPhysics: true).Timeout +=
                () => Pulse(context, spawnPoint);
        }
    }

    private static void Pulse(SpellCastContext context, Node3D spawnPoint)
    {
        if (!GodotObject.IsInstanceValid(spawnPoint) || !spawnPoint.IsInsideTree())
        {
            return;
        }

        Vector3 origin = spawnPoint.GlobalPosition;

        foreach (Enemy target in EnemyTargeting.FindTargets(origin, context, context.Pulse.TargetCount, origin))
        {
            Vector3 targetPoint = target.GetJitteredCenterMass(0.25f);
            Vector3 ragdollImpulse = (targetPoint - origin).Normalized() * context.RagdollForce.Value;

            // Every target was picked by the priority, so each counts as chosen for a targeting
            // rune's bonus.
            SpellHit spellHit = context.SpellHit with { ChosenTarget = target };
            target.TakeDamage(spellHit.CreateHit(context.Damage, ragdollImpulse));
            ChainResolver.SpawnArcVisual(spawnPoint, context.Pulse.ArcVisualScene, origin, targetPoint);
        }
    }
}
