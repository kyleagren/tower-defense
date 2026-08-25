using Godot;

public partial class ImpactFlash : Node3D
{
    [Export] public float Lifetime = 0.12f;

    public void Show(Vector3 at)
    {
        GlobalPosition = at;
        GetTree().CreateTimer(Lifetime).Timeout += QueueFree;
    }
}
