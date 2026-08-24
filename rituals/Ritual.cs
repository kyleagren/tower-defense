using Godot;

public partial class Ritual : Node3D
{
    [Export] public Spell Spell { get; private set; }
    [Export] public Rune[] RuneSlots { get; private set; } = new Rune[SpellInstance.MaxRuneSlots];
    [Export] public CastEffect CastEffect { get; private set; }
    [Export] public Node3D SpawnPoint { get; private set; }
    [Export] public float TargetSearchInterval = 0.25f;

    private SpellInstance spellInstance;
    private Enemy currentTarget;
    private float searchTimer;

    public override void _Ready()
    {
        spellInstance = new SpellInstance(Spell);

        foreach (Rune rune in RuneSlots)
        {
            if (rune != null)
            {
                spellInstance.TryAttachRune(rune);
            }
        }
    }

    public override void _Process(double delta)
    {
        spellInstance.Tick((float)delta);

        searchTimer -= (float)delta;
        if (searchTimer <= 0f)
        {
            searchTimer = TargetSearchInterval;
            currentTarget = EnemyTargeting.FindNearest(GlobalPosition, Spell.BaseRange);
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

    private void CastAtTarget()
    {
        Vector3 targetPosition = currentTarget.GlobalPosition;
        Vector3 levelAimPosition = new Vector3(targetPosition.X, SpawnPoint.GlobalPosition.Y, targetPosition.Z);
        SpawnPoint.LookAt(levelAimPosition, Vector3.Up);

        SpellCastContext context = spellInstance.Resolve();
        CastEffect.Cast(context, SpawnPoint);

        spellInstance.StartCast(context.CastTime, context.Cooldown);
    }
}
