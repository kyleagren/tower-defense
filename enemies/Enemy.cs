using Godot;
using System.Threading.Tasks;

public partial class Enemy : CharacterBody3D
{
    [Export] public float Speed = 3.5f;
    [Export] public NavigationAgent3D NavAgent { get; private set; }
    [Export] public Skeleton3D Skeleton { get; private set; }
    [Export] public CollisionShape3D BodyCollision { get; private set; }
    [Export] public PhysicalBoneSimulator3D BoneSimulator { get; private set; }
    [Export] public PhysicalBone3D RootPhysicalBone { get; private set; }

    [Export] public float WalkCycleFrequency = 6.0f;
    [Export] public float LegSwingAmplitude = 0.5f;
    [Export] public float ArmSwingAmplitude = 0.2f;
    [Export] public float RagdollCleanupDelay = 2f;
    [Export] public float MaxHealth = 50f;
    [Export] public float CenterMassHeight = 1f;
    [Export] public int Reward = 2;
    [Export] public HealthBar3D HealthBar { get; private set; }

    private int legLBoneIdx;
    private int legRBoneIdx;
    private int armLBoneIdx;
    private int armRBoneIdx;
    private float walkTime;
    private bool isDead;
    private float currentHealth;

    public bool IsDead => isDead;
    public Tag? Aura { get; private set; }
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
        legLBoneIdx = Skeleton.FindBone("Leg.L");
        legRBoneIdx = Skeleton.FindBone("Leg.R");
        armLBoneIdx = Skeleton.FindBone("Arm.L");
        armRBoneIdx = Skeleton.FindBone("Arm.R");

        currentHealth = MaxHealth;
        AddToGroup("enemies");
        HealthBar.SetFraction(1f);
        HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;

        SetPhysicalBonesCollision(0, 0);
    }

    public void SetTarget(Vector3 targetPosition)
    {
        NavAgent.TargetPosition = targetPosition;
    }

    public void TakeDamage(DamagePacket amount, Vector3 ragdollImpulse = default)
    {
        if (isDead)
        {
            return;
        }

        currentHealth -= amount.Total;
        HealthBar.SetFraction(currentHealth / MaxHealth);

        Tag? dominantElement = amount.DominantElement;
        if (dominantElement.HasValue)
        {
            Aura = dominantElement;
        }

        if (currentHealth <= 0f)
        {
            Die(ragdollImpulse);
        }
    }

    public void Die(Vector3 ragdollImpulse)
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        HealthBar.Visible = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, Reward);

        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        SetPhysicalBonesCollision(4, 1);

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
        if (!isDead)
        {
            return;
        }

        _ = ApplyRagdollImpulseNextPhysicsFrame(impulse);
    }

    public override void _PhysicsProcess(double delta)
    {
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

        AnimateWalkCycle(1.0, delta);
    }

    private void AnimateWalkCycle(double moveAmount, double delta)
    {
        walkTime += (float)(moveAmount * delta * WalkCycleFrequency);

        float legAngle = LegSwingAmplitude * Mathf.Sin(walkTime) * (float)moveAmount;
        float armAngle = ArmSwingAmplitude * Mathf.Sin(walkTime) * (float)moveAmount;

        Skeleton.SetBonePoseRotation(legLBoneIdx, new Quaternion(Vector3.Right, legAngle));
        Skeleton.SetBonePoseRotation(legRBoneIdx, new Quaternion(Vector3.Right, -legAngle));
        Skeleton.SetBonePoseRotation(armLBoneIdx, new Quaternion(Vector3.Right, -armAngle));
        Skeleton.SetBonePoseRotation(armRBoneIdx, new Quaternion(Vector3.Right, armAngle));
    }
}
