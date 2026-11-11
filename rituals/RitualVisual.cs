using Godot;
using System.Collections.Generic;

// What a Ritual looks like: the energy core, the crystals and runes orbiting it, the floating rocks
// beneath, the rune circle on the ground and the element's particles. Purely cosmetic: every copy runs
// it on its own, off state the Ritual already syncs.
public partial class RitualVisual : Node3D
{
    private const float BobFrequency = 1.7f;
    private const float RockBobFrequency = 1.1f;
    private const float RockBobHeight = 0.03f;
    private const float RockRoughness = 0.25f;
    private const float MainRockHeight = 0.72f;
    private const float FragmentRingRadius = 0.42f;
    private const int SymbolFontSize = 40;
    private const string ElderFuthark = "ᚠᚢᚦᚨᚱᚲᚷᚹᚺᚾᛁᛃᛇᛈᛉᛊᛏᛒᛖᛗᛚᛜᛞᛟ";

    private static readonly Vector3 _mainRockSize = new Vector3(0.34f, 0.2f, 0.34f);

    [Export] public MeshInstance3D Core { get; private set; }
    [Export] public Node3D CrystalOrbit { get; private set; }
    [Export] public Node3D SymbolOrbit { get; private set; }
    [Export] public Node3D Base { get; private set; }
    [Export] public MeshInstance3D RuneCircle { get; private set; }
    [Export] public Label3D Glyph { get; private set; }
    [Export] public GpuParticles3D Particles { get; private set; }
    [Export] public ShaderMaterial CrystalMaterial { get; private set; }
    [Export] public ShaderMaterial RockMaterial { get; private set; }
    [Export] public RitualAppearance[] Appearances { get; private set; } = System.Array.Empty<RitualAppearance>();
    // The small rocks drifting around the main one.
    [Export] public int RockFragmentCount { get; private set; } = 3;
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

    private sealed class Rock
    {
        public MeshInstance3D Mesh;
        public Vector3 RestPosition;
        // How far it falls to rest on the ground while broken.
        public float SettleDrop;
        public float Phase;
    }

    private readonly List<Crystal> _crystals = new();
    private readonly List<Rock> _rocks = new();
    private readonly List<Label3D> _symbols = new();
    private Ritual _ritual;
    private RitualAppearance _appearance;
    private Color _glyphColor;
    private Color _glyphOutline;
    private Color _symbolColor;
    // Advanced by delta scaled by vitality, so everything coasts to a stop on breaking rather than freezing.
    private float _time;
    private float _orbitAngle;
    private float _symbolAngle;
    private float _circleAngle;
    private float _vitality = 1f;

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
        RuneCircle.Visible = false;
        Glyph.Text = "";
        Particles.Emitting = false;
    }

    public void Setup(Spell spell)
    {
        _appearance = FindAppearance(ElementTags.Of(spell));
        Color color = DamageTypeColors.GetForSpell(spell);

        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Randomize();

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

        BuildCrystals(color, rng);
        BuildBase(color, rng);
        BuildSymbols(color, rng);
        StartParticles(color);
    }

    // A placement preview's footprint: the bare circle in a preview color, with no crystals, glyph or charge.
    public void ShowFootprint(Color color)
    {
        RuneCircle.Visible = true;
        RuneCircle.SetInstanceShaderParameter("circle_color", color);
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

    private void BuildCrystals(Color color, RandomNumberGenerator rng)
    {
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

            _crystals.Add(new Crystal
            {
                Mesh = mesh,
                OrbitOffset = Mathf.Tau * i / count,
                Tilt = new Basis(RandomFlatAxis(rng), Mathf.DegToRad(rng.RandfRange(0f, _appearance.CrystalMaxTilt))),
                Phase = rng.Randf() * Mathf.Tau
            });
        }
    }

    private void BuildBase(Color color, RandomNumberGenerator rng)
    {
        AddRock(new Vector3(0f, MainRockHeight, 0f), _mainRockSize, color, rng);

        for (int i = 0; i < RockFragmentCount; i++)
        {
            float angle = Mathf.Tau * i / RockFragmentCount + rng.RandfRange(-0.4f, 0.4f);
            float size = rng.RandfRange(0.08f, 0.13f);
            Vector3 position = new Vector3(Mathf.Cos(angle) * FragmentRingRadius, rng.RandfRange(0.42f, 0.58f), Mathf.Sin(angle) * FragmentRingRadius);
            AddRock(position, new Vector3(size, size * 0.8f, size), color, rng);
        }
    }

    private void AddRock(Vector3 position, Vector3 size, Color color, RandomNumberGenerator rng)
    {
        MeshInstance3D mesh = new MeshInstance3D
        {
            Mesh = RockMeshBuilder.Rock(size, RockRoughness, rng),
            MaterialOverride = RockMaterial,
            Basis = new Basis(Vector3.Up, rng.Randf() * Mathf.Tau),
            Position = position
        };
        mesh.SetInstanceShaderParameter("glow_color", color);
        Base.AddChild(mesh);

        _rocks.Add(new Rock
        {
            Mesh = mesh,
            RestPosition = position,
            SettleDrop = position.Y - size.Y,
            Phase = rng.Randf() * Mathf.Tau
        });
    }

    private void BuildSymbols(Color color, RandomNumberGenerator rng)
    {
        _symbolColor = color.Lightened(0.3f);

        for (int i = 0; i < _appearance.SymbolCount; i++)
        {
            Label3D symbol = new Label3D
            {
                Text = ElderFuthark[rng.RandiRange(0, ElderFuthark.Length - 1)].ToString(),
                Font = Glyph.Font,
                FontSize = SymbolFontSize,
                OutlineSize = 0,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            SymbolOrbit.AddChild(symbol);
            _symbols.Add(symbol);
        }
    }

    private void StartParticles(Color color)
    {
        if (_appearance.Particles == null)
        {
            return;
        }

        Particles.ProcessMaterial = _appearance.Particles;
        Particles.Amount = _appearance.ParticleAmount;
        Particles.Lifetime = _appearance.ParticleLifetime;
        Particles.SetInstanceShaderParameter("particle_color", color);
        Particles.Emitting = true;
    }

    private static Vector3 RandomFlatAxis(RandomNumberGenerator rng)
    {
        float heading = rng.Randf() * Mathf.Tau;
        return new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
    }

    public override void _Process(double delta)
    {
        if (_appearance == null)
        {
            return;
        }

        float dt = (float)delta;
        bool broken = _ritual.Health.IsBroken;
        _vitality = Mathf.MoveToward(_vitality, broken ? 0f : 1f, dt / VitalityFadeTime);

        float motion = dt * _vitality;
        _time += motion;
        _orbitAngle += _appearance.OrbitSpeed * motion;
        _symbolAngle += _appearance.SymbolOrbitSpeed * motion;
        _circleAngle += _appearance.CircleSpinSpeed * motion;

        Core.SetInstanceShaderParameter("charge", _ritual.Readiness.Fill);
        Core.SetInstanceShaderParameter("vitality", _vitality);
        RuneCircle.SetInstanceShaderParameter("rotation", _circleAngle);
        RuneCircle.SetInstanceShaderParameter("vitality", _vitality);
        RuneCircle.SetInstanceShaderParameter("fill", _ritual.Readiness.Fill);
        RuneCircle.SetInstanceShaderParameter("ready", _ritual.Readiness.IsReady ? 1f : 0f);
        Glyph.Modulate = new Color(_glyphColor, _vitality);
        Glyph.OutlineModulate = new Color(_glyphOutline, _vitality);

        UpdateCrystals();
        UpdateRocks();
        UpdateSymbols();

        bool emit = _appearance.Particles != null && !broken;
        if (Particles.Emitting != emit)
        {
            Particles.Emitting = emit;
        }
    }

    private void UpdateCrystals()
    {
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

    private void UpdateRocks()
    {
        foreach (Rock rock in _rocks)
        {
            float bob = Mathf.Sin(_time * RockBobFrequency + rock.Phase) * RockBobHeight;
            float settle = (1f - _vitality) * rock.SettleDrop;
            rock.Mesh.Position = rock.RestPosition + Vector3.Up * (bob - settle);
            rock.Mesh.SetInstanceShaderParameter("vitality", _vitality);
        }
    }

    private void UpdateSymbols()
    {
        Basis tilt = new Basis(Vector3.Right, Mathf.DegToRad(_appearance.SymbolOrbitTilt));
        int count = _symbols.Count;

        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Tau * i / count + _symbolAngle;
            Vector3 flat = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _appearance.SymbolOrbitRadius;
            _symbols[i].Position = tilt * flat;
            _symbols[i].Modulate = new Color(_symbolColor, _vitality * 0.8f);
        }
    }
}
