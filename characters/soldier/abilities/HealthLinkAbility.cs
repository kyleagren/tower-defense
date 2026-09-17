using Godot;

// README's Health Link (Characters). Arming captures the attack binding for the next two shots, each
// planting a spike in an enemy; the two spiked enemies then mirror MirrorPercent of the damage
// either one takes onto the other as true damage, for Duration. Backing out before the first spike
// lands is free - after that the cast is committed and pays the cooldown however it ends, including
// a miss, a second spike in the same enemy, or the first target dying before the pair is closed.
public partial class HealthLinkAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 12f;
    [Export] public float Duration { get; private set; } = 7f;
    [Export] public float ArmingTimeout { get; private set; } = 5f;
    [Export] public float SpikeRange { get; private set; } = 30f;
    [Export] public float MirrorPercent { get; private set; } = 1f;
    [Export] public PackedScene SpikeScene { get; private set; }
    [Export] public PackedScene TetherScene { get; private set; }
    // Placed off the enemy's own CenterMassHeight rather than a fixed world height, so it lands at
    // roughly shoulder height on anything regardless of how tall that enemy is.
    [Export] public float SpikeRise { get; private set; } = 0.25f;
    [Export] public float SpikeShoulderOffset { get; private set; } = 0.2f;
    // Where the armed beam starts. A plain Node3D export, which is safe against the inherited-base
    // hazard that bites custom-typed ones - Soldier.MuzzlePoint is exported the same way.
    [Export] public Node3D BeamOrigin { get; private set; }

    public override string DisplayName => "Health Link";
    public override string Description => "Spike two enemies so each mirrors the damage the other takes.";

    public override bool CanToggleOff => _phase == Phase.Arming;
    public override bool CapturesAttackInput => _phase == Phase.Arming;

    // Sweeps twice: once as the arming window closes, then again over the link itself. Placeholder
    // for an armed-state cue - the crosshair is the right home for that, not the ability bar.
    public override float ActiveFraction => _phase == Phase.Arming
        ? (ArmingTimeout <= 0f ? 0f : _armingRemaining / ArmingTimeout)
        : (Duration <= 0f ? 0f : _linkRemaining / Duration);

    private enum Phase { Idle, Arming, Linked }

    private Phase _phase = Phase.Idle;
    private float _armingRemaining;
    private float _linkRemaining;
    private Enemy _firstTarget;
    private Enemy _secondTarget;
    private Node3D _firstSpike;
    private Node3D _secondSpike;
    private HealthLinkTether _tether;

    public override bool TryActivate()
    {
        if (_phase == Phase.Arming)
        {
            EndCast(_firstTarget != null);
            return true;
        }

        // A live link is left alone - there's never a reason to want it gone early.
        if (_phase == Phase.Linked)
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
    }

    public override void CancelCapture()
    {
        if (_phase == Phase.Arming)
        {
            EndCast(_firstTarget != null);
        }
    }

    public override void _ExitTree()
    {
        // The spikes and beam are parented into the scene, not under this node, so a Sentinel torn
        // down mid-cast (hub character swap) would otherwise leave them behind.
        ClearVisuals();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_phase != Phase.Arming || !Character.CanUseAbilityInput || !@event.IsActionPressed("attack"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        FireSpike();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (_phase == Phase.Arming)
        {
            TickArming((float)delta);
        }
        else if (_phase == Phase.Linked)
        {
            TickLink((float)delta);
        }
    }

    private void TickArming(float delta)
    {
        // Losing the first target before the pair closes ends the cast immediately rather than
        // waiting for a second shot with nothing left to link to.
        if (_firstTarget != null && (!IsInstanceValid(_firstTarget) || _firstTarget.IsDead))
        {
            EndCast(payCooldown: true);
            return;
        }

        _armingRemaining -= delta;

        if (_armingRemaining <= 0f)
        {
            EndCast(_firstTarget != null);
        }
    }

    private void TickLink(float delta)
    {
        if (!IsInstanceValid(_firstTarget) || _firstTarget.IsDead
            || !IsInstanceValid(_secondTarget) || _secondTarget.IsDead)
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

    private void FireSpike()
    {
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 forward = -Character.Camera.GlobalTransform.Basis.Z;
        Vector3 end = origin + forward * SpikeRange;

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Enemies;

        Godot.Collections.Dictionary result = Character.GetWorld3D().DirectSpaceState.IntersectRay(query);
        Enemy hit = result.Count > 0 ? result["collider"].As<Node3D>() as Enemy : null;

        if (hit == null || hit.IsDead)
        {
            EndCast(payCooldown: true);
            return;
        }

        if (_firstTarget == null)
        {
            _firstTarget = hit;
            _firstSpike = SpawnSpike(hit);

            _tether = TetherScene.Instantiate<HealthLinkTether>();
            GetTree().CurrentScene.AddChild(_tether);
            _tether.Arm(BeamOrigin, _firstSpike);
            return;
        }

        // A second spike in the enemy already carrying the first links nothing, and spends the cast
        // the same as a miss.
        if (hit == _firstTarget)
        {
            EndCast(payCooldown: true);
            return;
        }

        Node3D secondSpike = SpawnSpike(hit);

        if (!HealthLinkAilment.Link(_firstTarget, hit, Duration, MirrorPercent))
        {
            secondSpike.QueueFree();
            EndCast(payCooldown: true);
            return;
        }

        _secondTarget = hit;
        _secondSpike = secondSpike;
        _phase = Phase.Linked;
        _linkRemaining = Duration;
        _tether.Link(_firstSpike, _secondSpike);
    }

    // Stands upright in a shoulder rather than following the shot that planted it: a spike aligned
    // to the bullet points back at whoever fired it, which foreshortens to nothing from the one
    // angle the shooter is guaranteed to be looking from. Parented to the enemy root with an
    // identity rotation, which stays upright on its own because enemies only ever yaw.
    private Node3D SpawnSpike(Enemy target)
    {
        Node3D spike = SpikeScene.Instantiate<Node3D>();
        target.AddChild(spike);
        spike.Position = new Vector3(SpikeShoulderOffset, target.CenterMassHeight + SpikeRise, 0f);
        return spike;
    }

    // One exit for every way a cast can end, so the "committed after the first spike" rule lives in
    // exactly one place and no path can leave a spike or beam behind.
    private void EndCast(bool payCooldown)
    {
        ClearVisuals();

        _firstTarget = null;
        _secondTarget = null;
        _armingRemaining = 0f;
        _linkRemaining = 0f;
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

        _tether = null;
        _firstSpike = null;
        _secondSpike = null;
    }

    private static void FreeIfValid(Node node)
    {
        if (node != null && IsInstanceValid(node))
        {
            node.QueueFree();
        }
    }
}
