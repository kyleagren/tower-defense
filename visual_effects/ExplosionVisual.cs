using Godot;

public partial class ExplosionVisual : Node3D
{
    [Export] public MeshInstance3D Sphere { get; private set; }
    [Export] public float Lifetime = 0.25f;

    public void Show(Vector3 position, float radius)
    {
        GlobalPosition = position;
        Sphere.Scale = Vector3.One * radius;
        GetTree().CreateTimer(Lifetime).Timeout += QueueFree;
    }
}
