using Godot;

public class InstantHitCastData
{
    public PackedScene ArcVisualScene;

    // How far toward the first target the arc splits when a strike forks - purely visual.
    public float ForkPointRatio = 0.3f;

    // Above 0, each strike's arc comes down from this far above its target instead of out of the
    // Ritual - purely visual.
    public float SkyStrikeHeight;
}
