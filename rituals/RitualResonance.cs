using Godot;

// The Resonant trigger, host only. The first hit a cast lands reaches every other Resonant Ritual
// within its own resonance radius of where the cast came from, and each casts a short beat later,
// aimed at the enemy that was hit.
public partial class RitualResonance : Node
{
    // A short beat between a hit and the Resonant casts it sets off, so a chain reads as a ripple.
    private const float Stagger = 0.1f;

    // How far above and below a pulled-in point the ground is looked for.
    private const float GroundProbeHeight = 5f;
    private const float GroundProbeDepth = 10f;

    private Ritual _ritual;
    // Seconds until a received pulse casts, below 0 for none waiting; and the enemy whose hit sent it.
    private float _pending = -1f;
    private Enemy _source;
    // Where the source's target was when a pulse arrived, for aiming at it if it has since died.
    private Vector3 _sourcePoint;

    // Set only for a pulsed cast with nothing in range to hit: what the spawn point aims at.
    public Vector3? Aim { get; private set; }

    // Set only for the same cast: where a ground spell lands.
    public Vector3? CastPoint { get; private set; }

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
    }

    public static void PulseNeighbours(ResonancePulse pulse, Enemy hitEnemy)
    {
        if (pulse.Sent)
        {
            return;
        }

        pulse.Sent = true;

        foreach (Ritual ritual in LevelContext.Current.Rituals.Active)
        {
            if (ritual != pulse.Caster && ritual.Resonance.Hears(pulse.Origin))
            {
                ritual.Resonance.Receive(hitEnemy);
            }
        }
    }

    private bool Hears(Vector3 position)
    {
        if (!_ritual.IsConfigured || _ritual.Health.IsBroken)
        {
            return false;
        }

        SpellCastContext context = _ritual.ResolveWithOwner();
        return context.Trigger?.Resonant == true && _ritual.GlobalPosition.DistanceTo(position) <= context.ResonanceRadius;
    }

    // Dropped while the Ritual can't cast or already has one waiting - never queued.
    private void Receive(Enemy hitEnemy)
    {
        if (_ritual.Health.IsBroken || !_ritual.CanCast || _pending >= 0f)
        {
            return;
        }

        _pending = Stagger;
        _source = hitEnemy;
        _sourcePoint = hitEnemy.AimPoint;
    }

    // The Ritual calls this every physics step while it has a trigger rune.
    public void Tick(SpellCastContext context, float delta)
    {
        if (_pending < 0f)
        {
            return;
        }

        _pending -= delta;

        if (_pending >= 0f)
        {
            return;
        }

        if (_ritual.CanCast)
        {
            _ritual.Targeting.Current = PulseTarget(context);

            // Nothing in range to hit: still reach toward the enemy that was hit - a projectile or
            // beam along that line, a ground spell onto the nearest ground in range of it.
            if (_ritual.Targeting.Current == null && _source != null)
            {
                Vector3 toward = IsInstanceValid(_source) && !_source.IsDead ? _source.AimPoint : _sourcePoint;
                Aim = toward;
                CastPoint = GroundPointToward(toward, context.Range.Value);
            }

            _ritual.CastAtTarget(context);
            Aim = null;
            CastPoint = null;
        }

        _source = null;
    }

    // The point pulled in to the edge of this Ritual's range if it's beyond it, then dropped onto the
    // terrain beneath. Null where there's no ground under it.
    private Vector3? GroundPointToward(Vector3 point, float range)
    {
        Vector3 origin = _ritual.GlobalPosition;
        Vector3 offset = point - origin;
        offset.Y = 0f;

        if (offset.Length() > range)
        {
            offset = offset.Normalized() * range;
        }

        Vector3 above = new Vector3(origin.X + offset.X, point.Y + GroundProbeHeight, origin.Z + offset.Z);
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(above, above + Vector3.Down * (GroundProbeHeight + GroundProbeDepth), PhysicsLayers.World);
        Godot.Collections.Dictionary hit = _ritual.GetWorld3D().DirectSpaceState.IntersectRay(query);

        return hit.Count == 0 ? null : (Vector3)hit["position"];
    }

    // The enemy that was hit if this Ritual can reach and see it, otherwise the enemy in its own range
    // nearest it - unless a targeting rune says what to aim at. With none, the cast still goes off -
    // and is wasted.
    private Enemy PulseTarget(SpellCastContext context)
    {
        if (context.FixedSpot != null || context.FixedAim != null || context.TargetingRune != null)
        {
            return _ritual.Targeting.Search(context);
        }

        Vector3 origin = _ritual.GlobalPosition;
        Vector3 sightFrom = _ritual.SpawnPoint.GlobalPosition;
        Enemy source = _source != null && IsInstanceValid(_source) && !_source.IsDead ? _source : null;

        if (source != null
            && GeometryUtils.RangeDistanceSquared(origin, source.GlobalPosition) <= context.Range.Value * context.Range.Value
            && LineOfSight.IsClear(_ritual, sightFrom, source.AimPoint))
        {
            return source;
        }

        Vector3 near = source?.GlobalPosition ?? origin;
        return EnemyTargeting.FindNearestTo(near, origin, context.Range.Value, sightFrom);
    }
}
