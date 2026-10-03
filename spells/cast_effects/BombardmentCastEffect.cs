using Godot;

public class BombardmentCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        BombardmentCastData data = context.Bombardment;

        Vector3? point = caster.LandingPoint(context);

        if (data?.Scene == null || point == null)
        {
            return;
        }

        Vector3 target = point.Value;
        float radius = data.Radius * (context.Area?.Multiplier.Value ?? 1f);

        Bombardment bombardment = data.Scene.Instantiate<Bombardment>();
        LevelContext.Current.LevelRoot.AddChild(bombardment);
        bombardment.Initialize(target, radius, data.Duration.Value, context.Damage, context.SpellHit);
        LevelContext.Current.SpellEffects.ShowVolume(data.Scene, target, radius, data.Duration.Value);

        // Spawned beside the bombardment rather than by it: the two are independent.
        FieldCastEffect.SpawnGround(context, target);
    }
}
