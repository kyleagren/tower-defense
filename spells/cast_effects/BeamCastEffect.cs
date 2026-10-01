using Godot;

public class BeamCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        BeamCastData data = context.Beam;

        if (data?.Scene == null || caster.CurrentTarget == null)
        {
            return;
        }

        // Along the spawn point, which the Ritual has just aimed at its target.
        Vector3 origin = spawnPoint.GlobalPosition;
        Vector3 direction = -spawnPoint.GlobalBasis.Z;
        float length = LengthToTerrain(caster, origin, direction, context.Range.Value);
        float halfWidth = data.HalfWidth * (context.Area?.Multiplier.Value ?? 1f);

        Beam beam = data.Scene.Instantiate<Beam>();
        LevelContext.Current.LevelRoot.AddChild(beam);
        beam.Initialize(origin, direction, length, halfWidth, data, context.Damage, context.RagdollForce.Value, context.SpellHit);
        LevelContext.Current.SpellEffects.ShowBeam(data.Scene, origin, direction, length, halfWidth, beam.Duration);
    }

    // A beam stops at the first terrain along it, the same rule as line of sight.
    private static float LengthToTerrain(Node3D context, Vector3 origin, Vector3 direction, float range)
    {
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * range, PhysicsLayers.World);
        Godot.Collections.Dictionary hit = context.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? range : origin.DistanceTo((Vector3)hit["position"]);
    }
}
