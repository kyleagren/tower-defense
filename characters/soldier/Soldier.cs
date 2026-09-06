using Godot;

public partial class Soldier : Sentinel
{
    [Export] public float AttackDamage = 8f;
    [Export] public float AttackRange = 30f;
    [Export] public float FireRate = 6f;
    [Export] public PackedScene ImpactScene { get; private set; }
    [Export] public Node3D MuzzlePoint { get; private set; }
    [Export] public PackedScene TracerScene { get; private set; }
    [Export] public float NormalFov = 75f;
    [Export] public float AdsFov = 40f;
    [Export] public float NormalSpringLength = 5f;
    [Export] public float AdsSpringLength = 0.5f;
    [Export] public float AdsTransitionSpeed = 10f;
    [Export] public float AdsSensitivityMultiplier = 0.5f;

    public override CharacterId Id => CharacterId.Soldier;

    private float _fireCooldown;

    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir);

        UpdateAim(delta);

        _fireCooldown -= (float)delta;

        if (CanUseCombatInput && Input.IsActionPressed("attack") && _fireCooldown <= 0f)
        {
            _fireCooldown = 1f / FireRate;
            FireShot();
        }
    }

    private void UpdateAim(double delta)
    {
        bool isAiming = CanUseCombatInput && Input.IsActionPressed("alt_action");
        float targetFov = isAiming ? AdsFov : NormalFov;
        float targetSpringLength = isAiming ? AdsSpringLength : NormalSpringLength;
        float targetSensitivityMultiplier = isAiming ? AdsSensitivityMultiplier : 1f;
        float weight = Mathf.Min(1f, (float)delta * AdsTransitionSpeed);

        Camera.Fov = Mathf.Lerp(Camera.Fov, targetFov, weight);
        SpringArm.SpringLength = Mathf.Lerp(SpringArm.SpringLength, targetSpringLength, weight);
        LookSensitivity = Mathf.Lerp(LookSensitivity, targetSensitivityMultiplier, weight);
    }

    private void FireShot()
    {
        Vector3 origin = Camera.GlobalPosition;
        Vector3 forward = -Camera.GlobalTransform.Basis.Z;
        Vector3 end = origin + forward * AttackRange;

        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Enemies;

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
        Vector3 tracerEnd = end;

        if (result.Count > 0)
        {
            tracerEnd = (Vector3)result["position"];

            if (result["collider"].As<Node3D>() is Enemy enemy)
            {
                enemy.TakeDamage(new DamagePacket { PhysicalDamage = AttackDamage });
            }

            if (ImpactScene != null)
            {
                ImpactFlash impact = ImpactScene.Instantiate<ImpactFlash>();
                GetTree().CurrentScene.AddChild(impact);
                impact.Show(tracerEnd);
            }
        }

        if (TracerScene != null)
        {
            ArcVisual tracer = TracerScene.Instantiate<ArcVisual>();
            GetTree().CurrentScene.AddChild(tracer);
            tracer.Show(MuzzlePoint.GlobalPosition, tracerEnd);
        }
    }
}
