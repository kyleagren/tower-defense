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
    [Export] public float RagdollCleanupDelay = 5.0f;

    private int legLBoneIdx;
    private int legRBoneIdx;
    private int armLBoneIdx;
    private int armRBoneIdx;
    private float walkTime;
    private bool isDead;

    public override void _Ready()
    {
        legLBoneIdx = Skeleton.FindBone("Leg.L");
        legRBoneIdx = Skeleton.FindBone("Leg.R");
        armLBoneIdx = Skeleton.FindBone("Arm.L");
        armRBoneIdx = Skeleton.FindBone("Arm.R");

        SetPhysicalBonesCollision(0, 0);
    }

    public void SetTarget(Vector3 targetPosition)
    {
        NavAgent.TargetPosition = targetPosition;
    }

    public void Die(Vector3 knockbackImpulse)
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        SetPhysicalBonesCollision(4, 1);

        BoneSimulator.PhysicalBonesStartSimulation();
        _ = ApplyKnockbackNextPhysicsFrame(knockbackImpulse);

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

    private async Task ApplyKnockbackNextPhysicsFrame(Vector3 impulse)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        RootPhysicalBone.ApplyCentralImpulse(impulse);
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
