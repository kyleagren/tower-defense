using Godot;

public partial class Soldier : PlayerCharacter
{
    [Export] public PlayerCharacter Architect { get; private set; }
    [Export] public float AttackDamage = 8f;
    [Export] public float AttackRange = 30f;
    [Export] public float FireRate = 6f;
    [Export] public PackedScene ImpactScene { get; private set; }
    [Export] public float NormalFov = 75f;
    [Export] public float AdsFov = 40f;
    [Export] public float NormalSpringLength = 5f;
    [Export] public float AdsSpringLength = 0.5f;
    [Export] public float AdsTransitionSpeed = 10f;

    private float _fireCooldown;

    public override void _Ready()
    {
        base._Ready();
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        SetActive(false);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        bool shouldBeActive = phase == WaveController.GamePhase.Combat;

        if (shouldBeActive)
        {
            HandoffFrom(Architect);
        }

        SetActive(shouldBeActive);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!active)
        {
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleLook(mouseMotion);
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            ToggleMouseCapture();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!active)
        {
            return;
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir);

        UpdateAim(delta);

        _fireCooldown -= (float)delta;

        if (Input.IsActionPressed("attack") && _fireCooldown <= 0f)
        {
            _fireCooldown = 1f / FireRate;
            FireShot();
        }
    }

    private void UpdateAim(double delta)
    {
        bool isAiming = Input.IsActionPressed("aim");
        float targetFov = isAiming ? AdsFov : NormalFov;
        float targetSpringLength = isAiming ? AdsSpringLength : NormalSpringLength;
        float weight = Mathf.Min(1f, (float)delta * AdsTransitionSpeed);

        Camera.Fov = Mathf.Lerp(Camera.Fov, targetFov, weight);
        SpringArm.SpringLength = Mathf.Lerp(SpringArm.SpringLength, targetSpringLength, weight);
    }

    private void FireShot()
    {
        Vector3 origin = Camera.GlobalPosition;
        Vector3 forward = -Camera.GlobalTransform.Basis.Z;
        Vector3 end = origin + forward * AttackRange;

        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = 2; // Enemies layer only

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count == 0)
        {
            return;
        }

        if (result["collider"].As<Node3D>() is Enemy enemy)
        {
            enemy.TakeDamage(new DamagePacket { PhysicalDamage = AttackDamage });
        }

        if (ImpactScene != null)
        {
            ImpactFlash impact = ImpactScene.Instantiate<ImpactFlash>();
            GetTree().CurrentScene.AddChild(impact);
            impact.Show((Vector3)result["position"]);
        }
    }
}
