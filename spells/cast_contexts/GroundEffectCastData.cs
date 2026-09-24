using Godot;

public class GroundEffectCastData
{
    public PackedScene Scene;
    public float Radius;
    public ScaledStat Duration = 0f;

    // The terrain's own strength - a slow fraction for chilled ground. Supplied by whatever spawns it,
    // so the number lives on that spell or rune rather than on the terrain's scene.
    public float Magnitude;
}
