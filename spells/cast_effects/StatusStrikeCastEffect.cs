using Godot;

// Strikes every enemy carrying a Status within a radius of the target, each from the sky, all at
// once. Enemies without it are left alone, so this pays off only where something has already marked
// the crowd.
public class StatusStrikeCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        StatusStrikeCastData data = context.StatusStrike;
        Vector3? center = caster.LandingPoint(context);

        if (data == null || center == null)
        {
            return;
        }

        float radius = data.Radius * (context.Area?.Multiplier.Value ?? 1f);

        foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(center.Value, radius,
            enemy => !enemy.IsDead && enemy.Status == data.Status))
        {
            Vector3 point = enemy.GetJitteredCenterMass(0.25f);
            enemy.TakeDamage(context.SpellHit.CreateHit(context.Damage, Vector3.Down * context.RagdollForce.Value));
            ChainResolver.SpawnArcVisual(spawnPoint, data.ArcVisualScene, point + Vector3.Up * data.StrikeHeight, point);
        }
    }
}
