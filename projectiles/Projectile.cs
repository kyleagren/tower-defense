using Godot;
using System.Collections.Generic;

public partial class Projectile : Area3D
{
    public float Damage { get; private set; }
    public float Radius { get; private set; }
    public ProjectileHitEffect HitEffect { get; set; }

    private int pierceRemaining;
    private float speed = 20f;
    private bool hasExpired;
    private HashSet<Node3D> alreadyHit = new HashSet<Node3D>();

    public void Initialize(ProjectileCastContext context)
    {
        Damage = context.Damage;
        Radius = context.Radius;
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
        if (hasExpired || alreadyHit.Contains(body))
        {
            return;
        }

        alreadyHit.Add(body);
        HitEffect?.OnHit(this, body);

        if (pierceRemaining <= 0)
        {
            hasExpired = true;
            QueueFree();
        }
        else
        {
            pierceRemaining--;
        }
    }
}