using Godot;

// One wedge's content. A wheel's item list holds null for an empty slot rather than leaving it out,
// so every other item keeps its direction.
public class RadialWheelItem
{
    public string Label { get; init; } = "";
    public Color Color { get; init; } = Colors.Gray;

    // Drawn dimmed and can't be committed - unaffordable, conflicting, or not yet available.
    public bool Enabled { get; init; } = true;

    // This slot's share of the ring's angle relative to its neighbors. The slot stays centered on
    // its own direction either way; weight only moves the boundaries, changing how much pointing
    // reaches it.
    public float Weight { get; init; } = 1f;
}
