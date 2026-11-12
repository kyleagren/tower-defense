using Godot;

// A marked spot that takes one blast after a delay - what falls onto it is the countdown. Damage lands
// on whatever is inside the radius at that moment. Copies on other machines run the same countdown and
// visuals, but never deal damage.
public partial class DelayedImpact : Node3D
{
    // A flat disc of radius 1, scaled to the blast. Its shader draws the blast's edge from the start and
    // fills in toward it as the impact nears.
    [Export] public GeometryInstance3D Marker { get; private set; }
    // Carries everything that falls - the rock, its glow and its trails - and is never scaled.
    [Export] public Node3D FallingBody { get; private set; }
    // A sphere of radius 1, sized off the blast.
    [Export] public Node3D Rock { get; private set; }
    [Export] public OmniLight3D Glow { get; private set; }
    // Emitting in world space, so what they leave behind stays where it was left.
    [Export] public GpuParticles3D[] Trails { get; private set; } = System.Array.Empty<GpuParticles3D>();
    [Export] public PackedScene LandingScene { get; private set; }
    [Export] public float FallHeight { get; private set; } = 20f;
    // Degrees off vertical. The body comes in over the Ritual that cast it, heading away from it.
    [Export] public float EntryAngle { get; private set; } = 35f;
    // Radians per second, rolling the way it travels.
    [Export] public float TumbleSpeed { get; private set; } = 3f;
    [Export] public float GlowStartEnergy { get; private set; } = 0.5f;
    [Export] public float GlowEndEnergy { get; private set; } = 4f;

    // The falling body's radius as a share of the blast's - big enough to read as what made the
    // crater, small enough not to swallow the screen.
    private const float BodyToBlastRatio = 0.75f;

    // Mostly outward, partly up, so the blast throws bodies rather than sliding them.
    private const float RagdollLift = 0.5f;

    private float _radius;
    private float _delay;
    private float _elapsed;
    private Vector3 _startPosition;
    private Vector3 _restPosition;
    private Vector3 _tumbleAxis;
    private DamagePacket _damage;
    private float _ragdollForce;
    private SpellHit _spellHit;
    private bool _isCopy;

    public void Initialize(Vector3 point, Vector3 from, float radius, float delay, DamagePacket damage, float ragdollForce, SpellHit spellHit)
    {
        Place(point, from, radius, delay);
        _damage = damage;
        _ragdollForce = ragdollForce;
        _spellHit = spellHit;
    }

    public void InitializeCopy(Vector3 point, Vector3 from, float radius, float delay)
    {
        _isCopy = true;
        Place(point, from, radius, delay);
    }

    private void Place(Vector3 point, Vector3 from, float radius, float delay)
    {
        GlobalPosition = point;
        _radius = radius;
        _delay = delay;

        Vector3 away = point - from;
        away.Y = 0f;
        Vector3 heading = away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward;

        float bodyRadius = radius * BodyToBlastRatio;
        _restPosition = Vector3.Up * bodyRadius;
        _startPosition = _restPosition + Vector3.Up * FallHeight - heading * FallHeight * Mathf.Tan(Mathf.DegToRad(EntryAngle));
        _tumbleAxis = Vector3.Up.Cross(heading);

        Rock.Scale = Vector3.One * bodyRadius;
        FallingBody.Position = _startPosition;
        Glow.LightEnergy = GlowStartEnergy;
        Marker.Scale = new Vector3(radius, 1f, radius);
        Marker.SetInstanceShaderParameter("progress", 0f);

        foreach (GpuParticles3D trail in Trails)
        {
            trail.Emitting = true;
        }
    }

    // Eased in, so it starts slow and slams down.
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _elapsed += dt;
        float progress = Mathf.Min(_elapsed / _delay, 1f);
        float fall = progress * progress;

        FallingBody.Position = _startPosition.Lerp(_restPosition, fall);
        Rock.Rotate(_tumbleAxis, TumbleSpeed * dt);
        Glow.LightEnergy = Mathf.Lerp(GlowStartEnergy, GlowEndEnergy, fall);
        Marker.SetInstanceShaderParameter("progress", progress);

        if (progress >= 1f)
        {
            Land();
        }
    }

    // Every machine shows its own landing off the same countdown, so nothing more is sent. The trails
    // are left to finish before this frees itself; a tween rather than a timer, so it dies with the
    // level if that goes first.
    private void Land()
    {
        SetPhysicsProcess(false);

        if (LandingScene != null)
        {
            Node3D landing = LandingScene.Instantiate<Node3D>();
            GetParent().AddChild(landing);
            (landing as IImpactVisual)?.Show(GlobalPosition, _radius);
        }

        if (!_isCopy)
        {
            foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(GlobalPosition, _radius, enemy => !enemy.IsDead))
            {
                Vector3 outward = enemy.GlobalPosition - GlobalPosition;
                outward.Y = 0f;
                Vector3 impulse = (outward.Normalized() + Vector3.Up * RagdollLift).Normalized() * _ragdollForce;
                enemy.TakeDamage(_spellHit.CreateHit(_damage, impulse));
            }
        }

        Marker.Visible = false;
        Rock.Visible = false;
        Glow.Visible = false;

        float lingerTime = 0f;

        foreach (GpuParticles3D trail in Trails)
        {
            trail.Emitting = false;
            lingerTime = Mathf.Max(lingerTime, (float)trail.Lifetime);
        }

        Tween linger = CreateTween();
        linger.TweenInterval(lingerTime);
        linger.TweenCallback(Callable.From(QueueFree));
    }
}
