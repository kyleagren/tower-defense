using Godot;

public partial class Ritual : Node3D
{
    [Export] public Spell Spell { get; private set; }
    [Export] public Rune RuneSlotA { get; private set; }
    [Export] public Rune RuneSlotB { get; private set; }
    [Export] public ProjectileCastEffect CastEffect { get; private set; }
    [Export] public Node3D SpawnPoint { get; private set; }
    [Export] public float TargetSearchInterval = 0.25f;

    private SpellInstance spellInstance;
    private Enemy currentTarget;
    private float searchTimer;

    public override void _Ready()
    {
        spellInstance = new SpellInstance(Spell);

        if (RuneSlotA != null)
        {
            spellInstance.TryAttachRune(RuneSlotA);
        }

        if (RuneSlotB != null)
        {
            spellInstance.TryAttachRune(RuneSlotB);
        }
    }

    public override void _Process(double delta)
    {
        spellInstance.Tick((float)delta);

        searchTimer -= (float)delta;
        if (searchTimer <= 0f)
        {
            searchTimer = TargetSearchInterval;
            currentTarget = FindNearestEnemyInRange();
        }

        if (currentTarget != null && (!IsInstanceValid(currentTarget) || currentTarget.IsDead))
        {
            currentTarget = null;
        }

        if (currentTarget != null && spellInstance.CanCast)
        {
            CastAtTarget();
        }
    }

    private Enemy FindNearestEnemyInRange()
    {
        float rangeSquared = Spell.BaseRange * Spell.BaseRange;

        Enemy nearest = null;
        float nearestDistSq = rangeSquared;

        foreach (Node node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is Enemy enemy && !enemy.IsDead)
            {
                float distSq = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
                if (distSq <= nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = enemy;
                }
            }
        }

        return nearest;
    }

    private void CastAtTarget()
    {
        Vector3 targetPosition = currentTarget.GlobalPosition;
        Vector3 levelAimPosition = new Vector3(targetPosition.X, SpawnPoint.GlobalPosition.Y, targetPosition.Z);
        SpawnPoint.LookAt(levelAimPosition, Vector3.Up);

        SpellCastContext context = spellInstance.Resolve();

        if (context is ProjectileCastContext projectileContext)
        {
            CastEffect.Cast(projectileContext, SpawnPoint);
        }

        spellInstance.StartCast(context.CastTime, context.Cooldown);
    }
}
