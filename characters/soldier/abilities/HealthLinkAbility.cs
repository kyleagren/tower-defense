using Godot;

// README's Health Link (Characters). Arming captures the attack binding for exactly two shots, each
// firing a spike bolt that sticks in whatever it hits - an enemy's shoulder or the terrain. Two
// spikes in two different living enemies link them: each then mirrors MirrorPercent of the damage it
// takes onto the other as true damage, for Duration, under a green beam. Anything else is a dud - a
// thin red beam between whatever spikes landed, for DudDuration. The second shot is fired whether or
// not the first landed. Backing out before the first bolt is fired is free; after that the cast is
// committed and pays the cooldown however it ends.
public partial class HealthLinkAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 12f;
    [Export] public float Duration { get; private set; } = 7f;
    [Export] public float ArmingTimeout { get; private set; } = 5f;
    [Export] public float DudDuration { get; private set; } = 1.5f;
    [Export] public float SpikeRange { get; private set; } = 30f;
    [Export] public float MirrorPercent { get; private set; } = 1f;
    // Of the rifle's per-shot damage, so spending the second shot on a dud still hits for something.
    [Export] public float SpikeDamageMultiplier { get; private set; } = 1.3f;
    // A backstop - the cast clears its terrain spikes when it ends, well inside this.
    [Export] public float TerrainSpikeLifetime { get; private set; } = 10f;
    [Export] public PackedScene SpikeScene { get; private set; }
    [Export] public PackedScene TetherScene { get; private set; }
    [Export] public PackedScene BoltScene { get; private set; }
    // How far the spike's base sits below its anchor, so it reads as driven into the shoulder rather
    // than balanced on top of it.
    [Export] public float SpikeDepth { get; private set; } = 0.05f;
    // How much of a terrain spike is buried, tip first. Out of SpikeLength, which matches the mesh in
    // HealthLinkSpike.tscn.
    [Export] public float TerrainSpikeDepth { get; private set; } = 0.08f;
    [Export] public float SpikeLength { get; private set; } = 0.28f;
    // Where bolts launch and the armed beam starts. A plain Node3D export, which is safe against the
    // inherited-base hazard that bites custom-typed ones - Soldier.MuzzlePoint is exported the same way.
    [Export] public Node3D BeamOrigin { get; private set; }

    private const int ShotsPerCast = 2;

    public override string DisplayName => "Health Link";
    public override string Description => "Fire two spikes; two enemies spiked mirror the damage each other takes.";

    public override bool CanToggleOff => _phase == Phase.Arming;
    public override bool CapturesAttackInput => _phase == Phase.Arming;

    // Placeholder for an armed-state cue - the crosshair is the right home for that, not the ability bar.
    public override float ActiveFraction => _phase switch
    {
        Phase.Arming => ArmingTimeout <= 0f ? 0f : _armingRemaining / ArmingTimeout,
        Phase.Linked => Duration <= 0f ? 0f : _linkRemaining / Duration,
        Phase.Dud => DudDuration <= 0f ? 0f : _dudRemaining / DudDuration,
        _ => 0f
    };

    private enum Phase { Idle, Arming, Linked, Dud }

    private Phase _phase = Phase.Idle;
    private float _armingRemaining;
    private float _linkRemaining;
    private float _dudRemaining;
    private int _shotsFired;
    private HealthLinkBolt _boltInFlight;
    // Null for a spike in the terrain, or for one that was never planted.
    private Enemy _firstTarget;
    private Enemy _secondTarget;
    private Node3D _firstSpike;
    private Node3D _secondSpike;
    private HealthLinkTether _tether;

    // Committed once the first bolt leaves the barrel, hit or miss.
    private bool IsCommitted => _shotsFired > 0;

    private Soldier Wielder => (Soldier)Character;

    public override bool TryActivate()
    {
        if (_phase == Phase.Arming)
        {
            EndCast(IsCommitted);
            return true;
        }

        // A live link or a fizzling dud is left alone - there's never a reason to want either gone early.
        if (_phase != Phase.Idle)
        {
            return false;
        }

        return base.TryActivate();
    }

    protected override void Activate()
    {
        IsActive = true;
        _phase = Phase.Arming;
        _armingRemaining = ArmingTimeout;
        _shotsFired = 0;
    }

    public override void CancelCapture()
    {
        if (_phase == Phase.Arming)
        {
            EndCast(IsCommitted);
        }
    }

    public override void _ExitTree()
    {
        // The spikes, beam and any bolt in flight are parented into the scene, not under this node,
        // so a Sentinel torn down mid-cast (hub character swap) would otherwise leave them behind.
        ClearVisuals();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_phase != Phase.Arming || !Character.CanUseAbilityInput || !@event.IsActionPressed("attack"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();

        if (_boltInFlight == null && _shotsFired < ShotsPerCast)
        {
            FireBolt();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        switch (_phase)
        {
            case Phase.Arming:
                TickArming((float)delta);
                break;
            case Phase.Linked:
                TickLink((float)delta);
                break;
            case Phase.Dud:
                TickDud((float)delta);
                break;
        }
    }

    private void TickArming(float delta)
    {
        DropSpikesInDeadEnemies();

        _armingRemaining -= delta;

        if (_armingRemaining <= 0f)
        {
            EndCast(IsCommitted);
        }
    }

    private void TickLink(float delta)
    {
        if (IsGone(_firstTarget) || IsGone(_secondTarget))
        {
            EndCast(payCooldown: true);
            return;
        }

        _linkRemaining -= delta;

        if (_linkRemaining <= 0f)
        {
            EndCast(payCooldown: true);
        }
    }

    private void TickDud(float delta)
    {
        DropSpikesInDeadEnemies();

        _dudRemaining -= delta;

        if (_dudRemaining <= 0f)
        {
            EndCast(payCooldown: true);
        }
    }

    // Launches from the muzzle toward whatever the crosshair is on, so the bolt lands where the
    // player aimed even though it doesn't start at the camera.
    private void FireBolt()
    {
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 end = origin - Character.Camera.GlobalTransform.Basis.Z * SpikeRange;

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Enemies | PhysicsLayers.World | PhysicsLayers.Obstacles;
        Godot.Collections.Dictionary result = Character.GetWorld3D().DirectSpaceState.IntersectRay(query);
        Vector3 aimPoint = result.Count > 0 ? (Vector3)result["position"] : end;

        _shotsFired++;
        _boltInFlight = BoltScene.Instantiate<HealthLinkBolt>();
        GetTree().CurrentScene.AddChild(_boltInFlight);
        _boltInFlight.Launch(BeamOrigin.GlobalPosition, aimPoint, SpikeRange, OnBoltResolved);
    }

    private void OnBoltResolved(HealthLinkBolt.Impact impact)
    {
        _boltInFlight = null;

        if (impact.Enemy != null || impact.HitSurface)
        {
            Wielder.ShowImpact(impact.Position);
        }

        Node3D spike = PlantSpike(impact);

        if (_shotsFired == 1)
        {
            _firstSpike = spike;
            _firstTarget = impact.Enemy;

            if (spike != null)
            {
                EnsureTether().Arm(BeamOrigin, spike);
            }

            DealSpikeDamage(impact);
            return;
        }

        _secondSpike = spike;
        _secondTarget = impact.Enemy;

        bool linked = _firstTarget != null && _secondTarget != null && _secondTarget != _firstTarget
            && HealthLinkAilment.Link(_firstTarget, _secondTarget, Duration, MirrorPercent);

        // After the link forms, so the second spike's own damage mirrors onto the first enemy.
        DealSpikeDamage(impact);

        if (linked)
        {
            _phase = Phase.Linked;
            _linkRemaining = Duration;
            EnsureTether().Link(_firstSpike, _secondSpike);
            return;
        }

        BeginDud();
    }

    // Nothing to draw a beam between means the dud has nothing to show, so it ends at once.
    private void BeginDud()
    {
        if (_firstSpike == null || _secondSpike == null)
        {
            EndCast(payCooldown: true);
            return;
        }

        _phase = Phase.Dud;
        _dudRemaining = DudDuration;
        EnsureTether().Fail(_firstSpike, _secondSpike);
    }

    private void DealSpikeDamage(HealthLinkBolt.Impact impact)
    {
        if (impact.Enemy == null)
        {
            return;
        }

        float baseDamage = Character.AttackDamage * SpikeDamageMultiplier;
        DamagePacket damage = Wielder.RollShotDamage(impact.Enemy, impact.SegmentFrom, impact.SegmentTo, baseDamage);
        impact.Enemy.TakeDamage(new Hit { Damage = damage, Attribution = Character.HitAttribution });
    }

    // A spike in an enemy that has died just disappears; the beam hides itself once an end is gone.
    private void DropSpikesInDeadEnemies()
    {
        if (IsGone(_firstTarget))
        {
            FreeIfValid(_firstSpike);
            _firstSpike = null;
            _firstTarget = null;
        }

        if (IsGone(_secondTarget))
        {
            FreeIfValid(_secondSpike);
            _secondSpike = null;
            _secondTarget = null;
        }
    }

    private static bool IsGone(Enemy enemy) => enemy != null && (!IsInstanceValid(enemy) || enemy.IsDead);

    private HealthLinkTether EnsureTether()
    {
        if (_tether == null)
        {
            _tether = TetherScene.Instantiate<HealthLinkTether>();
            GetTree().CurrentScene.AddChild(_tether);
        }

        return _tether;
    }

    // Null for a bolt that ran out of range with nothing to stick in.
    private Node3D PlantSpike(HealthLinkBolt.Impact impact)
    {
        if (impact.Enemy != null)
        {
            return SpawnSpikeInEnemy(impact.Enemy);
        }

        return impact.HitSurface ? SpawnSpikeInTerrain(impact) : null;
    }

    // Stands upright in a shoulder rather than following the shot that planted it: a spike aligned
    // to the bullet points back at whoever fired it, which foreshortens to nothing from the one
    // angle the shooter is guaranteed to be looking from.
    //
    // Parented to the shoulder attachment so it rides the walk cycle, and upright because enemies
    // only ever yaw. An enemy with no skeleton (the dev-only TrainingDummy) gets it at centre mass
    // instead, untuned - it only has to be visible there.
    private Node3D SpawnSpikeInEnemy(Enemy target)
    {
        Node3D spike = SpikeScene.Instantiate<Node3D>();
        Node3D anchor = target.ShoulderAttachment ?? target;
        anchor.AddChild(spike);

        spike.Position = target.ShoulderAttachment != null
            ? new Vector3(0f, -SpikeDepth, 0f)
            : new Vector3(0f, target.CenterMassHeight, 0f);

        return spike;
    }

    // Tip first along the bolt's flight, so it reads as having struck there. The spike's own +Y runs
    // base to tip. Frees itself after TerrainSpikeLifetime even if the cast somehow never clears it.
    private Node3D SpawnSpikeInTerrain(HealthLinkBolt.Impact impact)
    {
        Node3D spike = SpikeScene.Instantiate<Node3D>();
        GetTree().CurrentScene.AddChild(spike);

        // The arc constructor can't pick an axis for a half-turn, so straight down is built by hand.
        spike.GlobalBasis = impact.Direction.Dot(Vector3.Down) > 0.999f
            ? new Basis(Vector3.Right, Mathf.Pi)
            : new Basis(new Quaternion(Vector3.Up, impact.Direction));
        spike.GlobalPosition = impact.Position - impact.Direction * (SpikeLength - TerrainSpikeDepth);

        GetTree().CreateTimer(TerrainSpikeLifetime).Timeout += () => FreeIfValid(spike);
        return spike;
    }

    // One exit for every way a cast can end, so the commitment rule lives in exactly one place and
    // no path can leave a spike, beam or bolt behind.
    private void EndCast(bool payCooldown)
    {
        ClearVisuals();

        _firstTarget = null;
        _secondTarget = null;
        _armingRemaining = 0f;
        _linkRemaining = 0f;
        _dudRemaining = 0f;
        _shotsFired = 0;
        _phase = Phase.Idle;
        IsActive = false;

        if (payCooldown)
        {
            StartCooldown();
        }
    }

    private void ClearVisuals()
    {
        FreeIfValid(_tether);
        FreeIfValid(_firstSpike);
        FreeIfValid(_secondSpike);
        FreeIfValid(_boltInFlight);

        _tether = null;
        _firstSpike = null;
        _secondSpike = null;
        _boltInFlight = null;
    }

    private static void FreeIfValid(Node node)
    {
        if (node != null && IsInstanceValid(node))
        {
            node.QueueFree();
        }
    }
}
