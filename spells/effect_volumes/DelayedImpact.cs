using Godot;

// A marked spot that takes one blast after a delay - what falls onto it is the countdown. Damage lands
// on whatever is inside the radius at that moment. Copies on other machines run the same countdown and
// visuals, but never deal damage.
public partial class DelayedImpact : Node3D
{
    // A flat disc of radius 1, growing from a point to the full blast radius - and fading in - as the
    // impact nears.
    [Export] public GeometryInstance3D Marker { get; private set; }
    // A sphere of radius 1, sized off the blast, falling from FallHeight until it rests on the spot.
    [Export] public Node3D FallingBody { get; private set; }
    [Export] public float FallHeight { get; private set; } = 20f;
    [Export] public PackedScene ExplosionScene { get; private set; }

    private const float MarkerStartTransparency = 0.8f;
    private const float MarkerEndTransparency = 0.3f;

    // The marker's starting size as a share of the blast. Not zero - a zero-scaled transform can't be
    // inverted.
    private const float MarkerStartShare = 0.05f;

    // The falling body's radius as a share of the blast's - big enough to read as what made the
    // crater, small enough not to swallow the screen.
    private const float BodyToBlastRatio = 0.75f;

    // Mostly outward, partly up, so the blast throws bodies rather than sliding them.
    private const float RagdollLift = 0.5f;

    private float _radius;
    private float _delay;
    private float _elapsed;
    private DamagePacket _damage;
    private float _ragdollForce;
    private SpellHit _spellHit;
    private bool _isCopy;

    public void Initialize(Vector3 point, float radius, float delay, DamagePacket damage, float ragdollForce, SpellHit spellHit)
    {
        Place(point, radius, delay);
        _damage = damage;
        _ragdollForce = ragdollForce;
        _spellHit = spellHit;
    }

    public void InitializeCopy(Vector3 point, float radius, float delay)
    {
        _isCopy = true;
        Place(point, radius, delay);
    }

    private void Place(Vector3 point, float radius, float delay)
    {
        GlobalPosition = point;
        _radius = radius;
        _delay = delay;
        SetMarkerSize(0f);
        Marker.Transparency = MarkerStartTransparency;
        float bodyRadius = radius * BodyToBlastRatio;
        FallingBody.Scale = Vector3.One * bodyRadius;
        FallingBody.Position = Vector3.Up * (bodyRadius + FallHeight);
    }

    public override void _PhysicsProcess(double delta)
    {
        _elapsed += (float)delta;
        float progress = Mathf.Min(_elapsed / _delay, 1f);

        FallingBody.Position = Vector3.Up * (_radius * BodyToBlastRatio + FallHeight * (1f - progress));
        Marker.Transparency = Mathf.Lerp(MarkerStartTransparency, MarkerEndTransparency, progress);
        SetMarkerSize(progress);

        if (progress >= 1f)
        {
            Land();
        }
    }

    private void SetMarkerSize(float progress)
    {
        float share = Mathf.Lerp(MarkerStartShare, 1f, progress);
        Marker.Scale = new Vector3(_radius * share, 1f, _radius * share);
    }

    // Every machine shows its own explosion off the same countdown, so nothing more is sent.
    private void Land()
    {
        if (ExplosionScene != null)
        {
            ExplosionVisual explosion = ExplosionScene.Instantiate<ExplosionVisual>();
            GetParent().AddChild(explosion);
            explosion.Show(GlobalPosition, _radius);
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

        QueueFree();
    }
}
