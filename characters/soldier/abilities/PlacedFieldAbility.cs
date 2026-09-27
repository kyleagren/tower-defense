using Godot;

// An ability that drops a field on the ground under the crosshair for Duration - Converge and Repel.
// Active while the field stands, with the cooldown starting once it's gone. A subclass only decides
// how to configure the field it spawns.
public abstract partial class PlacedFieldAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 8f;
    [Export] public float PlacementRange { get; private set; } = 25f;
    [Export] public float Radius { get; private set; } = 5f;
    [Export] public float Duration { get; private set; } = 5f;
    [Export] public PackedScene FieldScene { get; private set; }

    // How far below the aimed point to look for ground to settle on.
    private const float GroundSearchDepth = 50f;
    // Backs off the aimed surface before dropping, so a shot into a wall settles in front of it.
    private const float SurfaceBackoff = 0.1f;

    public override float ActiveFraction => _activeDuration <= 0f ? 0f : _fieldRemaining / _activeDuration;

    private Vector3 _placement;
    private float _activeDuration;
    private float _fieldRemaining;

    // Nowhere to put the field means no cast and no cooldown.
    protected override bool CanActivate()
    {
        return TryFindPlacement(out _placement);
    }

    protected override void Activate()
    {
        _activeDuration = ScaledDuration(Duration);

        EffectVolume field = FieldScene.Instantiate<EffectVolume>();
        LevelContext.Current.LevelRoot.AddChild(field);
        field.Initialize(_placement, ScaledArea(Radius), _activeDuration);
        ConfigureField(field);

        IsActive = true;
        _fieldRemaining = _activeDuration;
    }

    protected abstract void ConfigureField(EffectVolume field);

    // Timed here rather than off the field's own exit, since the field frees itself on the same
    // duration and a tracked reference would only add a way for the two to disagree.
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!IsActive)
        {
            return;
        }

        _fieldRemaining -= (float)delta;

        if (_fieldRemaining <= 0f)
        {
            IsActive = false;
            StartCooldown();
        }
    }

    private bool TryFindPlacement(out Vector3 point)
    {
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 aimEnd = Character.GetCameraAimPoint(PlacementRange);
        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;

        PhysicsRayQueryParameters3D aim = PhysicsRayQueryParameters3D.Create(origin, aimEnd);
        aim.CollisionMask = PhysicsLayers.World;
        Godot.Collections.Dictionary aimHit = spaceState.IntersectRay(aim);

        Vector3 dropFrom = aimHit.Count > 0
            ? (Vector3)aimHit["position"] - (aimEnd - origin).Normalized() * SurfaceBackoff
            : aimEnd;

        PhysicsRayQueryParameters3D drop = PhysicsRayQueryParameters3D.Create(dropFrom, dropFrom + Vector3.Down * GroundSearchDepth);
        drop.CollisionMask = PhysicsLayers.World;
        Godot.Collections.Dictionary groundHit = spaceState.IntersectRay(drop);

        point = groundHit.Count > 0 ? (Vector3)groundHit["position"] : Vector3.Zero;
        return groundHit.Count > 0;
    }
}
