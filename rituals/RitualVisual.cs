using Godot;
using System.Collections.Generic;

// What a Ritual looks like: the energy core, the crystals orbiting it, the rune circle beneath and
// the spell's glyph. Purely cosmetic: every copy runs it on its own, off state the Ritual already syncs.
public partial class RitualVisual : Node3D
{
    private const float BobFrequency = 1.7f;

    [Export] public MeshInstance3D Core { get; private set; }
    [Export] public Node3D CrystalOrbit { get; private set; }
    [Export] public MeshInstance3D RuneCircle { get; private set; }
    [Export] public Label3D Glyph { get; private set; }
    [Export] public ShaderMaterial CrystalMaterial { get; private set; }
    [Export] public RitualAppearance[] Appearances { get; private set; } = System.Array.Empty<RitualAppearance>();
    // Seconds to go dark on breaking, and to come back on repair.
    [Export] public float VitalityFadeTime { get; private set; } = 0.6f;
    // How far the crystals sink while broken.
    [Export] public float BrokenDrop { get; private set; } = 0.9f;

    private sealed class Crystal
    {
        public MeshInstance3D Mesh;
        public float OrbitOffset;
        public Basis Tilt;
        public float Phase;
    }

    private readonly List<Crystal> _crystals = new();
    private Ritual _ritual;
    private RitualAppearance _appearance;
    private Color _glyphColor;
    private Color _glyphOutline;
    // Advanced by delta scaled by vitality, so everything coasts to a stop on breaking rather than freezing.
    private float _time;
    private float _orbitAngle;
    private float _circleAngle;
    private float _vitality = 1f;

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
        RuneCircle.Visible = false;
        Glyph.Text = "";
    }

    public void Setup(Spell spell)
    {
        _appearance = FindAppearance(ElementTags.Of(spell));
        Color color = DamageTypeColors.GetForSpell(spell);

        Core.SetInstanceShaderParameter("core_color", color);
        Core.SetInstanceShaderParameter("turbulence", _appearance.CoreTurbulence);
        Core.SetInstanceShaderParameter("flow_speed", _appearance.CoreFlowSpeed);

        RuneCircle.Visible = true;
        RuneCircle.SetInstanceShaderParameter("circle_color", color);
        RuneCircle.SetInstanceShaderParameter("flicker", _appearance.CircleFlicker);
        RuneCircle.SetInstanceShaderParameter("glyph_count", (float)_appearance.CircleGlyphCount);

        _glyphColor = color.Lightened(0.7f);
        _glyphOutline = color.Darkened(0.6f);
        Glyph.Text = spell.Glyph;

        BuildCrystals(color);
    }

    private RitualAppearance FindAppearance(Tag? element)
    {
        foreach (RitualAppearance appearance in Appearances)
        {
            if (element.HasValue && appearance.Element == element.Value)
            {
                return appearance;
            }
        }

        GD.PushWarning($"No RitualAppearance for element {element}; using defaults.");
        return new RitualAppearance();
    }

    private void BuildCrystals(Color color)
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Randomize();

        int count = _appearance.CrystalCount;

        for (int i = 0; i < count; i++)
        {
            float length = _appearance.CrystalLength * (1f + rng.RandfRange(-1f, 1f) * _appearance.CrystalLengthJitter);

            MeshInstance3D mesh = new MeshInstance3D
            {
                Mesh = CrystalMeshBuilder.Bipyramid(_appearance.CrystalSides, _appearance.CrystalRadius, length * 0.65f, length * 0.35f),
                MaterialOverride = CrystalMaterial
            };
            mesh.SetInstanceShaderParameter("crystal_color", color);
            CrystalOrbit.AddChild(mesh);

            float tiltHeading = rng.Randf() * Mathf.Tau;
            Vector3 tiltAxis = new Vector3(Mathf.Cos(tiltHeading), 0f, Mathf.Sin(tiltHeading));

            _crystals.Add(new Crystal
            {
                Mesh = mesh,
                OrbitOffset = Mathf.Tau * i / count,
                Tilt = new Basis(tiltAxis, Mathf.DegToRad(rng.RandfRange(0f, _appearance.CrystalMaxTilt))),
                Phase = rng.Randf() * Mathf.Tau
            });
        }
    }

    public override void _Process(double delta)
    {
        if (_appearance == null)
        {
            return;
        }

        float dt = (float)delta;
        float target = _ritual.Health.IsBroken ? 0f : 1f;
        _vitality = Mathf.MoveToward(_vitality, target, dt / VitalityFadeTime);

        float motion = dt * _vitality;
        _time += motion;
        _orbitAngle += _appearance.OrbitSpeed * motion;
        _circleAngle += _appearance.CircleSpinSpeed * motion;

        Core.SetInstanceShaderParameter("charge", _ritual.Readiness.Fill);
        Core.SetInstanceShaderParameter("vitality", _vitality);
        RuneCircle.SetInstanceShaderParameter("rotation", _circleAngle);
        RuneCircle.SetInstanceShaderParameter("vitality", _vitality);
        Glyph.Modulate = new Color(_glyphColor, _vitality);
        Glyph.OutlineModulate = new Color(_glyphOutline, _vitality);

        float drop = (1f - _vitality) * BrokenDrop;

        foreach (Crystal crystal in _crystals)
        {
            float angle = crystal.OrbitOffset + _orbitAngle;
            float bob = Mathf.Sin(_time * BobFrequency + crystal.Phase) * _appearance.BobHeight;
            Vector3 position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _appearance.OrbitRadius
                + Vector3.Up * (bob - drop);
            Basis spin = new Basis(Vector3.Up, _time * _appearance.CrystalSpin + crystal.Phase);

            crystal.Mesh.Transform = new Transform3D(crystal.Tilt * spin, position);
            crystal.Mesh.SetInstanceShaderParameter("vitality", _vitality);
        }
    }
}
