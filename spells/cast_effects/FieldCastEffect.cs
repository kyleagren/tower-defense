using Godot;

// Lays a patch of terrain where the spell lands, and nothing else - a field spell's whole effect is
// the ground it leaves.
public class FieldCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        Vector3? point = caster.LandingPoint(context);

        if (point != null)
        {
            SpawnGround(context, point.Value);
        }
    }

    // The context's ground effect at point, Area included. Shared with anything that leaves ground
    // behind as part of a larger cast, like Ice Storm's chilled ground.
    public static void SpawnGround(SpellCastContext context, Vector3 point)
    {
        GroundEffectCastData data = context.GroundEffect;

        if (data?.Scene == null)
        {
            return;
        }

        float radius = data.Radius * (context.Area?.Multiplier.Value ?? 1f);

        GroundEffect ground = data.Scene.Instantiate<GroundEffect>();
        LevelContext.Current.LevelRoot.AddChild(ground);
        ground.Initialize(point, radius, data.Duration.Value, data.Magnitude);
        LevelContext.Current.SpellEffects.ShowVolume(data.Scene, point, radius, data.Duration.Value);
    }
}
