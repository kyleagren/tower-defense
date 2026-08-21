using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ExplodeOnHitEffect : ProjectileHitEffect
{
    public override void OnHit(Projectile projectile, Node3D body)
    {
        PhysicsDirectSpaceState3D spaceState = projectile.GetWorld3D().DirectSpaceState;
        SphereShape3D explosionShape = new SphereShape3D { Radius = projectile.Radius };
        PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
        {
            Shape = explosionShape,
            Transform = projectile.GlobalTransform,
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> results = spaceState.IntersectShape(query);
        HashSet<Node3D> hitBodies = new HashSet<Node3D>();

        foreach (Godot.Collections.Dictionary result in results)
        {
            if (result.TryGetValue("collider", out Variant colliderVariant)
                && colliderVariant.Obj is Node3D hitBody)
            {
                hitBodies.Add(hitBody);
            }
        }

        foreach (Node3D hitBody in hitBodies)
        {
            GD.Print($"Hit {hitBody.Name} for {projectile.Damage} damage.");
        }
    }
}
