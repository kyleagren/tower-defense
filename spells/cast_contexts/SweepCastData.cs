using Godot;

public class SweepCastData
{
    public PackedScene Scene;
    public float Radius;
    // Metres per second; how far it gets is Speed x Duration.
    public ScaledStat Speed = 0f;
    public ScaledStat Duration = 0f;
}
