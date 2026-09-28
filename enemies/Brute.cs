using Godot;

// A huge, slow melee enemy whose club hits every Sentinel and Ritual in a wide arc in front of it,
// not just the one it was aiming at. The swing is telegraphed - damage lands when the club passes
// straight ahead - so a Sentinel that steps out of the arc during the wind-up dodges it.
public partial class Brute : WalkingEnemy
{
    [Export] public float SwingArcDegrees = 150f;
    [Export] public float SwingReach = 3.5f;
    [Export] public float WindUpDuration = 0.7f;
    [Export] public float StrikeDuration = 0.25f;
    [Export] public float RecoverDuration = 0.5f;
    // The club arm's yaw around the body: positive is out to the arm's own side, negative across
    // the front toward the other side.
    [Export] public float WindUpYawDegrees = 100f;
    [Export] public float FollowThroughYawDegrees = -70f;

    private const float RaisedArmPitchDegrees = 90f;

    private int _clubArmBoneIdx;
    private float _swingElapsed = -1f;
    private bool _impactResolved;
    private IDamageable _swingTarget;

    private bool IsSwinging => _swingElapsed >= 0f;

    // The strike phase is linear, so this is exactly when the club points straight ahead.
    private float ImpactTime => WindUpDuration + StrikeDuration * WindUpYawDegrees / (WindUpYawDegrees - FollowThroughYawDegrees);

    private float SwingDuration => WindUpDuration + StrikeDuration + RecoverDuration;

    public override void _Ready()
    {
        base._Ready();
        _clubArmBoneIdx = Skeleton.FindBone("Arm.R");
    }

    // After base, so the swing's arm pose overwrites the walk cycle's on the same frame.
    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (IsSwinging)
        {
            AdvanceSwing((float)delta);
        }
    }

    protected override void DealDamage(IDamageable target)
    {
        if (IsSwinging)
        {
            return;
        }

        _swingTarget = target;
        StartSwing();
        Rpc(MethodName.ReceiveSwing);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveSwing()
    {
        StartSwing();
    }

    private void StartSwing()
    {
        _swingElapsed = 0f;
        _impactResolved = false;
    }

    private void AdvanceSwing(float delta)
    {
        _swingElapsed += delta;

        if (!_impactResolved && _swingElapsed >= ImpactTime)
        {
            _impactResolved = true;

            if (IsSimulatedHere)
            {
                ResolveImpact();
            }
        }

        if (_swingElapsed >= SwingDuration)
        {
            _swingElapsed = -1f;
            _swingTarget = null;
            return;
        }

        Skeleton.SetBonePoseRotation(_clubArmBoneIdx, ClubArmPose(_swingElapsed));
    }

    // Anything the Brute was attacking that can't be in the arc sweep below (a wall, the objective)
    // is struck directly. Sentinels and Rituals are left to the sweep - a Sentinel so it can dodge,
    // a Ritual so it isn't struck twice.
    private void ResolveImpact()
    {
        if (_swingTarget is not Sentinel && _swingTarget is not Ritual && IsStillDamageable(_swingTarget))
        {
            base.DealDamage(_swingTarget);
        }

        foreach (Sentinel sentinel in Level.Sentinels.Values)
        {
            if (sentinel.IsActive && !sentinel.IsDead && IsInSwingArc(sentinel.GlobalPosition))
            {
                base.DealDamage(sentinel);
            }
        }

        foreach (Ritual ritual in Level.Rituals.Active)
        {
            if (!ritual.IsDisabled && IsInSwingArc(ritual.GlobalPosition))
            {
                base.DealDamage(ritual);
            }
        }
    }

    private static bool IsStillDamageable(IDamageable target) =>
        target is GodotObject node && IsInstanceValid(node) && !target.IsDestroyed;

    private bool IsInSwingArc(Vector3 position)
    {
        Vector3 offset = position - GlobalPosition;
        offset.Y = 0f;

        if (offset.Length() > SwingReach)
        {
            return false;
        }

        if (offset.LengthSquared() < 0.0001f)
        {
            return true;
        }

        Vector3 forward = -GlobalBasis.Z;
        forward.Y = 0f;

        return forward.AngleTo(offset) <= Mathf.DegToRad(SwingArcDegrees / 2f);
    }

    // Pitch raises the hanging arm to point forward; yaw then sweeps it around the body. Pitch is
    // applied first, so the sweep stays horizontal.
    private Quaternion ClubArmPose(float elapsed)
    {
        float pitch;
        float yaw;

        if (elapsed < WindUpDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / WindUpDuration);
            pitch = Mathf.Lerp(0f, RaisedArmPitchDegrees, t);
            yaw = Mathf.Lerp(0f, WindUpYawDegrees, t);
        }
        else if (elapsed < WindUpDuration + StrikeDuration)
        {
            float t = (elapsed - WindUpDuration) / StrikeDuration;
            pitch = RaisedArmPitchDegrees;
            yaw = Mathf.Lerp(WindUpYawDegrees, FollowThroughYawDegrees, t);
        }
        else
        {
            float t = Mathf.SmoothStep(0f, 1f, (elapsed - WindUpDuration - StrikeDuration) / RecoverDuration);
            pitch = Mathf.Lerp(RaisedArmPitchDegrees, 0f, t);
            yaw = Mathf.Lerp(FollowThroughYawDegrees, 0f, t);
        }

        return new Quaternion(Vector3.Up, Mathf.DegToRad(yaw)) * new Quaternion(Vector3.Right, Mathf.DegToRad(pitch));
    }
}
