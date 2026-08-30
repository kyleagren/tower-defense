using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Enemy : CharacterBody3D
{
    [Export] public float Speed = 3.5f;
    [Export] public float RotationSpeed = 10f;
    [Export] public NavigationAgent3D NavAgent { get; private set; }
    [Export] public Skeleton3D Skeleton { get; private set; }
    [Export] public CollisionShape3D BodyCollision { get; private set; }
    [Export] public PhysicalBoneSimulator3D BoneSimulator { get; private set; }
    [Export] public PhysicalBone3D RootPhysicalBone { get; private set; }

    [Export] public float WalkCycleFrequency = 6.0f;
    [Export] public float LegSwingAmplitude = 0.5f;
    [Export] public float ArmSwingAmplitude = 0.2f;
    [Export] public float RagdollCleanupDelay = 2f;
    [Export] public float MaxHealth = 100f;
    [Export] public float CenterMassHeight = 1f;
    [Export] public int ManaReward = 1;
    [Export] public int GoldReward = 1;
    [Export] public HealthBar3D HealthBar { get; private set; }

    // TODO placeholder: remove once there's a real way to display an enemy's element/ailments (Section 15 iconography).
    private static readonly Dictionary<Tag, StandardMaterial3D> _statusMaterials = new Dictionary<Tag, StandardMaterial3D>
    {
        { Tag.Fire, CreateOverlayMaterial(new Color(1f, 0.5f, 0f)) },
        { Tag.Water, CreateOverlayMaterial(new Color(0.05f, 0.15f, 0.55f)) },
        { Tag.Cold, CreateOverlayMaterial(new Color(0.75f, 0.95f, 1f)) },
        { Tag.Lightning, CreateOverlayMaterial(new Color(1f, 0.9f, 0.1f)) }
    };

    private static readonly StandardMaterial3D _burnFlashMaterial = CreateOverlayMaterial(new Color(0.5f, 0f, 0f));
    private const float BurnFlashDuration = 0.1f;

    private static StandardMaterial3D CreateOverlayMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
    }

    private int _legLBoneIdx;
    private int _legRBoneIdx;
    private int _armLBoneIdx;
    private int _armRBoneIdx;
    private float _walkTime;
    private bool _isDead;
    private bool _hasFacedInitialDirection;
    private float _currentHealth;
    private List<Ailment> _ailments = new List<Ailment>();
    // TODO placeholder: remove alongside StatusMaterials/BurnFlashMaterial above.
    private List<MeshInstance3D> _meshInstances = new List<MeshInstance3D>();
    private float _burnFlashTimer;

    public bool IsDead => _isDead;
    public Tag? Status { get; private set; }
    public Vector3 CenterMass => GlobalPosition + Vector3.Up * CenterMassHeight;

    public Vector3 GetJitteredCenterMass(float jitterAmount)
    {
        Vector3 jitter = new Vector3(
            (float)GD.RandRange(-jitterAmount, jitterAmount),
            (float)GD.RandRange(-jitterAmount, jitterAmount),
            (float)GD.RandRange(-jitterAmount, jitterAmount)
        );

        return CenterMass + jitter;
    }

    public override void _Ready()
    {
        _legLBoneIdx = Skeleton.FindBone("Leg.L");
        _legRBoneIdx = Skeleton.FindBone("Leg.R");
        _armLBoneIdx = Skeleton.FindBone("Arm.L");
        _armRBoneIdx = Skeleton.FindBone("Arm.R");

        _currentHealth = MaxHealth;
        AddToGroup("enemies");
        HealthBar.SetFraction(1f);
        HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;

        SetPhysicalBonesCollision(0, 0);
        CollectMeshInstances(Skeleton);
    }

    public void SetTarget(Vector3 targetPosition)
    {
        NavAgent.TargetPosition = targetPosition;
    }

    public void TakeDamage(DamagePacket amount, Vector3 ragdollImpulse = default, bool updateStatus = true, ReactionRune reaction = null)
    {
        if (_isDead)
        {
            return;
        }

        bool reactionTriggered = reaction?.OnHit(this, amount) ?? false;

        _currentHealth -= amount.Total;
        HealthBar.SetFraction(_currentHealth / MaxHealth);

        if (updateStatus)
        {
            if (reactionTriggered)
            {
                Status = null;
            }
            else
            {
                Tag? dominantElement = amount.DominantElement;
                if (dominantElement.HasValue)
                {
                    Status = dominantElement;
                }
            }
        }

        if (_currentHealth <= 0f)
        {
            Die(ragdollImpulse);
        }
    }

    public void ApplyAilment(Ailment ailment)
    {
        if (ailment is BurnAilment newBurn)
        {
            BurnAilment existingBurn = _ailments.Find(a => a is BurnAilment) as BurnAilment;
            if (existingBurn != null)
            {
                newBurn.MergeFrom(existingBurn);
                _ailments.Remove(existingBurn);
            }
        }

        _ailments.Add(ailment);
    }

    public void Die(Vector3 ragdollImpulse)
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        HealthBar.Visible = false;
        HealthBar.SetProcess(false);
        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, ManaReward, GoldReward);

        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        SetPhysicalBonesCollision(PhysicsLayers.Ragdoll, PhysicsLayers.World);

        BoneSimulator.PhysicalBonesStartSimulation();
        _ = ApplyRagdollImpulseNextPhysicsFrame(ragdollImpulse);

        GetTree().CreateTimer(RagdollCleanupDelay).Timeout += QueueFree;
    }

    private void SetPhysicalBonesCollision(uint layer, uint mask)
    {
        foreach (Node child in BoneSimulator.GetChildren())
        {
            if (child is PhysicalBone3D physicalBone)
            {
                physicalBone.CollisionLayer = layer;
                physicalBone.CollisionMask = mask;
            }
        }
    }

    private async Task ApplyRagdollImpulseNextPhysicsFrame(Vector3 impulse)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        RootPhysicalBone.ApplyCentralImpulse(impulse);
    }

    public void ApplyExtraRagdollImpulse(Vector3 impulse)
    {
        if (!_isDead)
        {
            return;
        }

        _ = ApplyRagdollImpulseNextPhysicsFrame(impulse);
    }

    public override void _PhysicsProcess(double delta)
    {
        TickAilments((float)delta);
        UpdateElementalVisual((float)delta); // TODO placeholder: remove once there's a real way to display this.

        if (NavAgent.IsNavigationFinished())
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            AnimateWalkCycle(0.0, delta);
            return;
        }

        Vector3 nextPathPosition = NavAgent.GetNextPathPosition();
        Vector3 direction = (nextPathPosition - GlobalPosition).Normalized();

        Velocity = direction * Speed;
        MoveAndSlide();

        RotateTowardsMovement(direction, delta);
        AnimateWalkCycle(1.0, delta);
    }

    private void TickAilments(float delta)
    {
        for (int i = _ailments.Count - 1; i >= 0; i--)
        {
            if (!_ailments[i].Tick(this, delta))
            {
                _ailments.RemoveAt(i);
            }
        }
    }

    // TODO placeholder block: remove this method and CollectMeshInstances once there's a real way to display an enemy's element/ailments.
    private void UpdateElementalVisual(float delta)
    {
        bool isBurning = _ailments.Exists(ailment => ailment is BurnAilment);

        if (isBurning)
        {
            _burnFlashTimer += delta;
            if (_burnFlashTimer >= 1f)
            {
                _burnFlashTimer -= 1f;
            }
        }
        else
        {
            _burnFlashTimer = 0f;
        }

        bool isFlashing = isBurning && _burnFlashTimer < BurnFlashDuration;

        Material overlay = null;
        if (isFlashing)
        {
            overlay = _burnFlashMaterial;
        }
        else if (Status.HasValue && _statusMaterials.TryGetValue(Status.Value, out StandardMaterial3D statusMaterial))
        {
            overlay = statusMaterial;
        }

        foreach (MeshInstance3D meshInstance in _meshInstances)
        {
            meshInstance.MaterialOverlay = overlay;
        }
    }

    private void CollectMeshInstances(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D meshInstance)
            {
                _meshInstances.Add(meshInstance);
            }

            CollectMeshInstances(child);
        }
    }

    private static Basis? ComputeFacingBasis(Vector3 direction)
    {
        Vector3 flatDirection = new Vector3(direction.X, 0f, direction.Z);
        if (flatDirection.LengthSquared() < 0.0001f)
        {
            return null;
        }

        return Basis.LookingAt(flatDirection, Vector3.Up);
    }

    public void SetInitialFacing(Vector3 direction)
    {
        Basis? facingBasis = ComputeFacingBasis(direction);
        if (facingBasis.HasValue)
        {
            Basis = facingBasis.Value;
            _hasFacedInitialDirection = true;
        }
    }

    private void RotateTowardsMovement(Vector3 direction, double delta)
    {
        Basis? targetBasis = ComputeFacingBasis(direction);
        if (!targetBasis.HasValue)
        {
            return;
        }

        if (!_hasFacedInitialDirection)
        {
            _hasFacedInitialDirection = true;
            Basis = targetBasis.Value;
            return;
        }

        Basis = Basis.Slerp(targetBasis.Value, Mathf.Min(1f, (float)delta * RotationSpeed));
    }

    private void AnimateWalkCycle(double moveAmount, double delta)
    {
        _walkTime += (float)(moveAmount * delta * WalkCycleFrequency);

        float legAngle = LegSwingAmplitude * Mathf.Sin(_walkTime) * (float)moveAmount;
        float armAngle = ArmSwingAmplitude * Mathf.Sin(_walkTime) * (float)moveAmount;

        Skeleton.SetBonePoseRotation(_legLBoneIdx, new Quaternion(Vector3.Right, legAngle));
        Skeleton.SetBonePoseRotation(_legRBoneIdx, new Quaternion(Vector3.Right, -legAngle));
        Skeleton.SetBonePoseRotation(_armLBoneIdx, new Quaternion(Vector3.Right, -armAngle));
        Skeleton.SetBonePoseRotation(_armRBoneIdx, new Quaternion(Vector3.Right, armAngle));
    }
}
