using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ExplodeOnHitEffect : ProjectileHitEffect
{
    [Export] public float ExplosionRadius = 1f;
    [Export] public PackedScene VisualScene { get; private set; }
    [Export] public float RagdollForceMultiplier = 20f;

    public override void OnHit(Projectile projectile, Node3D body)
    {
        Vector3 explosionCenter = projectile.GlobalPosition;
        float radius = ExplosionRadius * projectile.AreaMultiplier;

        if (VisualScene != null)
        {
            ExplosionVisual visual = VisualScene.Instantiate<ExplosionVisual>();
            projectile.GetTree().CurrentScene.AddChild(visual);
            visual.Show(explosionCenter, radius);
        }

        if (body is Enemy directHitEnemy)
        {
            Vector3 boostedImpulse = projectile.LastRagdollImpulse * RagdollForceMultiplier;
            Vector3 bonusImpulse = boostedImpulse - projectile.LastRagdollImpulse;
            directHitEnemy.ApplyExtraRagdollImpulse(bonusImpulse);
        }

        HashSet<Enemy> hitEnemies = FindEnemiesInRadius(projectile, explosionCenter, radius, body);

        foreach (Enemy enemy in hitEnemies)
        {
            Vector3 ragdollImpulse = (enemy.GlobalPosition - explosionCenter).Normalized() * projectile.RagdollForce * RagdollForceMultiplier;
            enemy.TakeDamage(projectile.Damage, ragdollImpulse, spellHit: projectile.SpellHit);
        }
    }

    private static HashSet<Enemy> FindEnemiesInRadius(Projectile projectile, Vector3 center, float radius, Node3D excludeBody)
    {
        PhysicsDirectSpaceState3D spaceState = projectile.GetWorld3D().DirectSpaceState;
        SphereShape3D explosionShape = new SphereShape3D { Radius = radius };
        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = explosionShape,
            Transform = projectile.GlobalTransform,
            CollisionMask = PhysicsLayers.Enemies,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> results = spaceState.IntersectShape(query);
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

        foreach (Godot.Collections.Dictionary result in results)
        {
            if (result.TryGetValue("collider", out Variant colliderVariant)
                && colliderVariant.Obj is Enemy enemy
                && enemy != excludeBody)
            {
                hitEnemies.Add(enemy);
            }
        }

        return hitEnemies;
    }
}
