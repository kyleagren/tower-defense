using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Ritual : Node3D
{
    [Export] public Spell Spell { get; private set; }
    [Export] public Rune[] RuneSlots { get; private set; } = new Rune[GameRules.MaxRuneSlots];
    [Export] public Node3D SpawnPoint { get; private set; }
    [Export] public float TargetSearchInterval = 0.25f;

    private SpellInstance spellInstance;
    private Enemy currentTarget;
    private float searchTimer;
    private int budgetCost;
    private List<CastEffect> castEffects;

    public override void _Ready()
    {
        castEffects = GetChildren().OfType<CastEffect>().ToList();
        Configure(Spell, RuneSlots);
    }

    public void Repair()
    {
        GD.Print($"Repairing {Spell?.Name} tower...");
    }

    public void SetBudgetCost(int cost)
    {
        budgetCost = cost;
    }

    public void Destroy()
    {
        foreach (Rune rune in RuneSlots)
        {
            if (rune == null)
            {
                continue;
            }

            RuneInventoryEntry entry = SaveManager.Instance.Data.KnownRunes
                .FirstOrDefault(e => e.Rune == rune);

            if (entry != null && !entry.Unlimited)
            {
                entry.Quantity++;
            }
        }

        EconomyManager.Instance.RefundTower(budgetCost);

        QueueFree();
    }

    public void Configure(Spell spell, IReadOnlyList<Rune> runes)
    {
        Spell = spell;
        spellInstance = new SpellInstance(spell);

        foreach (Rune rune in runes)
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

        bool hadTarget = currentTarget != null;

        if (currentTarget != null && (!IsInstanceValid(currentTarget) || currentTarget.IsDead))
        {
            currentTarget = null;
        }

        bool justLostTarget = hadTarget && currentTarget == null;

        if (searchTimer <= 0f || justLostTarget)
        {
            searchTimer = TargetSearchInterval;
            currentTarget = EnemyTargeting.FindNearest(GlobalPosition, spellInstance.Resolve().Range);
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
        CastEffect effect = castEffects.FirstOrDefault(e => e.CanHandle(context));
        effect?.Cast(context, SpawnPoint);

        spellInstance.StartCast(context.CastTime, context.Cooldown);
    }
}
