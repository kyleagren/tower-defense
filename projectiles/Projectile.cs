using Godot;
using System.Collections.Generic;

public partial class Projectile : Area3D
{
    private float damage;
    private int pierceRemaining;
    private float speed = 20f;
    private HashSet<Node3D> alreadyHit = new HashSet<Node3D>();

    public void Initialize(ProjectileCastContext context)
    {
        damage = context.Damage;
        pierceRemaining = context.PierceCount;
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        Position -= Transform.Basis.Z * speed * (float)delta;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (alreadyHit.Contains(body))
        {
            return;
        }

        alreadyHit.Add(body);
        GD.Print($"Hit {body.Name} for {damage} damage.");

        if (pierceRemaining <= 0)
        {
            QueueFree();
        }
        else
        {
            pierceRemaining--;
        }
    }
}