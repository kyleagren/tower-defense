using Godot;

public partial class InstantHitCastEffect : CastEffect
{
    [Export] public PackedScene ArcVisualScene { get; private set; }

    public override void Cast(SpellCastContext context, Node3D spawnPoint)
    {
        if (context is not InstantHitCastContext instantContext)
        {
            return;
        }

        Enemy target = EnemyTargeting.FindNearest(spawnPoint.GlobalPosition, instantContext.Range);
        if (target == null)
        {
            return;
        }

        Vector3 targetPoint = target.GetJitteredCenterMass(0.25f);
        Vector3 knockback = (targetPoint - spawnPoint.GlobalPosition).Normalized() * instantContext.KnockbackForce;
        float firstHitDamage = instantContext.Damage * Mathf.Pow(1f + instantContext.MoreDamagePerRemainingChain, instantContext.ChainCount);
        target.TakeDamage(firstHitDamage, knockback);

        if (ArcVisualScene != null)
        {
            ArcVisual visual = ArcVisualScene.Instantiate<ArcVisual>();
            GetTree().CurrentScene.AddChild(visual);
            visual.Show(spawnPoint.GlobalPosition, targetPoint);
        }

        ChainResolver.ResolveChain(target, targetPoint, instantContext.Damage, instantContext.KnockbackForce, instantContext.ChainCount, instantContext.ChainRange, instantContext.MoreDamagePerRemainingChain, ArcVisualScene);
    }
}
