using Godot;

public partial class Enemy : CharacterBody3D
{
    [Export] public float Speed = 3.5f;
    [Export] public NavigationAgent3D NavAgent { get; private set; }

    public void SetTarget(Vector3 targetPosition)
    {
        NavAgent.TargetPosition = targetPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (NavAgent.IsNavigationFinished())
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        Vector3 nextPathPosition = NavAgent.GetNextPathPosition();
        Vector3 direction = (nextPathPosition - GlobalPosition).Normalized();

        Velocity = direction * Speed;
        MoveAndSlide();
    }
}
