using Godot;

public class BeamCastData
{
    public PackedScene Scene;
    public float HalfWidth;
    public int TickCount;
    public float TickInterval;
    // kg·m/s per tick along the beam, flattened - divided by each enemy's mass like any knockback.
    public float PushImpulse;
}
