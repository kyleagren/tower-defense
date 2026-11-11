using Godot;

// How a Ritual of one element looks. The spell itself adds only its glyph.
[GlobalClass]
public partial class RitualAppearance : Resource
{
    [Export] public Tag Element { get; private set; }

    [ExportGroup("Core")]
    // 0 is an even glow, 1 is churning plasma.
    [Export(PropertyHint.Range, "0,1")] public float CoreTurbulence { get; private set; } = 0.3f;
    [Export] public float CoreFlowSpeed { get; private set; } = 0.5f;

    [ExportGroup("Crystals")]
    [Export] public int CrystalCount { get; private set; } = 4;
    [Export] public int CrystalSides { get; private set; } = 4;
    [Export] public float CrystalLength { get; private set; } = 0.45f;
    [Export] public float CrystalRadius { get; private set; } = 0.07f;
    // How much each crystal's length may vary, as a fraction, so a ring doesn't read as copies.
    [Export(PropertyHint.Range, "0,1")] public float CrystalLengthJitter { get; private set; } = 0.2f;
    // Degrees off vertical, at most.
    [Export] public float CrystalMaxTilt { get; private set; } = 10f;
    [Export] public float OrbitRadius { get; private set; } = 0.6f;
    // Radians per second; negative orbits the other way.
    [Export] public float OrbitSpeed { get; private set; } = 0.6f;
    // Each crystal turning about its own axis, radians per second.
    [Export] public float CrystalSpin { get; private set; } = 0.5f;
    [Export] public float BobHeight { get; private set; } = 0.05f;

    [ExportGroup("Rune circle")]
    [Export] public float CircleSpinSpeed { get; private set; } = 0.2f;
    [Export(PropertyHint.Range, "0,1")] public float CircleFlicker { get; private set; } = 0f;
    [Export] public int CircleGlyphCount { get; private set; } = 12;
}
