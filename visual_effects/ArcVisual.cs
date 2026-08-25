using Godot;

public partial class ArcVisual : Node3D
{
    [Export] public float Lifetime = 0.15f;

    public void Show(Vector3 from, Vector3 to)
    {
        GlobalPosition = (from + to) / 2f;
        LookAt(to, Vector3.Up);

        Scale = new Vector3(Scale.X, Scale.Y, from.DistanceTo(to));

        GetTree().CreateTimer(Lifetime).Timeout += QueueFree;
    }
}
