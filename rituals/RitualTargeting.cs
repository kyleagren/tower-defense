using Godot;

// Who a Ritual aims at, host only: searching again on a timer, the three ways of searching - anything
// in range, an Anchor's spot, a fixed lane - and turning the spawn point to face the aim.
public partial class RitualTargeting : Node
{
    [Export] public float SearchInterval { get; private set; } = 0.25f;

    private Ritual _ritual;
    private float _searchTimer;

    public Enemy Current { get; set; }

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
    }

    // Drops a dead target, then searches again on the timer or as soon as the target is lost.
    public void Refresh(SpellCastContext context, float delta)
    {
        _searchTimer -= delta;

        bool hadTarget = Current != null;
        DropIfDead();
        bool justLostTarget = hadTarget && Current == null;

        if (_searchTimer <= 0f || justLostTarget)
        {
            _searchTimer = SearchInterval;
            Current = Search(context);
        }
    }

    public void DropIfDead()
    {
        if (Current != null && (!IsInstanceValid(Current) || Current.IsDead))
        {
            Current = null;
        }
    }

    public Enemy Search(SpellCastContext context) =>
        context.FixedSpot != null ? FindInSpot(context)
        : context.FixedAim != null ? FindInLane(context)
        : EnemyTargeting.FindTarget(_ritual.GlobalPosition, context, sightFrom: _ritual.SpawnPoint.GlobalPosition);

    // An Anchor Ritual only answers to an enemy inside its spot's area. No sight needed - it isn't
    // aiming at anyone.
    private Enemy FindInSpot(SpellCastContext context) =>
        EnemyTargeting.FindNearest(context.FixedSpot.Point, context.SpotRadius);

    // A Ritual that fires in a fixed direction only answers to an enemy it can see in its lane:
    // within the lane's half-width of the line from its base out to its range.
    private Enemy FindInLane(SpellCastContext context)
    {
        Vector3 end = _ritual.GlobalPosition + context.FixedAim.Direction * context.Range.Value;
        return EnemyTargeting.FindNearestToSegment(_ritual.GlobalPosition, end, context.FixedAim.LaneHalfWidth, sightFrom: _ritual.SpawnPoint.GlobalPosition);
    }

    // Straight at the target's center mass, so a projectile arrives where it's meant to hit. A
    // target almost directly above or below keeps the previous aim, since LookAt can't resolve a
    // direction parallel to its up vector - an enemy walking through a Shrouded Ritual can get there.
    public void AimAt(Vector3 target)
    {
        Node3D spawnPoint = _ritual.SpawnPoint;
        Vector3 toTarget = (target - spawnPoint.GlobalPosition).Normalized();

        if (Mathf.Abs(toTarget.Dot(Vector3.Up)) > 0.999f)
        {
            return;
        }

        spawnPoint.LookAt(target, Vector3.Up);
    }
}
