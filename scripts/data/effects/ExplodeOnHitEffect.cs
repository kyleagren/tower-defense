using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ExplodeOnHitEffect : ProjectileHitEffect
{
    [Export] public float ExplosionRadius = 3f;
    [Export] public PackedScene VisualScene { get; private set; }

    public override void OnHit(Projectile projectile, Node3D body)
    {
        Vector3 explosionCenter = projectile.GlobalPosition;

        if (VisualScene != null)
        {
            ExplosionVisual visual = VisualScene.Instantiate<ExplosionVisual>();
            projectile.GetTree().CurrentScene.AddChild(visual);
            visual.Show(explosionCenter, ExplosionRadius);
        }

        PhysicsDirectSpaceState3D spaceState = projectile.GetWorld3D().DirectSpaceState;
        SphereShape3D explosionShape = new SphereShape3D { Radius = ExplosionRadius };
        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = explosionShape,
            Transform = projectile.GlobalTransform,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> results = spaceState.IntersectShape(query);
        HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

        foreach (Godot.Collections.Dictionary result in results)
        {
            if (result.TryGetValue("collider", out Variant colliderVariant)
                && colliderVariant.Obj is Enemy enemy
                && enemy != body)
            {
                hitEnemies.Add(enemy);
            }
        }

        foreach (Enemy enemy in hitEnemies)
        {
            Vector3 ragdollImpulse = (enemy.GlobalPosition - explosionCenter).Normalized() * projectile.RagdollForce;
            enemy.TakeDamage(projectile.Damage, ragdollImpulse);
        }
    }
}
