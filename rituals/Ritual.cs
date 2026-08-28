using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Ritual : Node3D
{
    [Export] public Spell Spell { get; private set; }
    [Export] public Rune[] RuneSlots { get; private set; } = new Rune[GameRules.MaxRuneSlots];
    [Export] public Node3D SpawnPoint { get; private set; }
    [Export] public float TargetSearchInterval = 0.25f;

    private SpellInstance _spellInstance;
    private Enemy _currentTarget;
    private float _searchTimer;
    private int _budgetCost;
    private List<CastEffect> _castEffects;

    public override void _Ready()
    {
        _castEffects = GetChildren().OfType<CastEffect>().ToList();
        Configure(Spell, RuneSlots);
    }

    public void Repair()
    {
        GD.Print($"Repairing {Spell?.Name} tower...");
    }

    public void SetBudgetCost(int cost)
    {
        _budgetCost = cost;
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

        EconomyManager.Instance.RefundTower(_budgetCost);

        QueueFree();
    }

    public void Configure(Spell spell, IReadOnlyList<Rune> runes)
    {
        Spell = spell;
        _spellInstance = new SpellInstance(spell);

        foreach (Rune rune in runes)
        {
            if (rune != null)
            {
                _spellInstance.TryAttachRune(rune);
            }
        }
    }

    public override void _Process(double delta)
    {
        _spellInstance.Tick((float)delta);

        _searchTimer -= (float)delta;

        bool hadTarget = _currentTarget != null;

        if (_currentTarget != null && (!IsInstanceValid(_currentTarget) || _currentTarget.IsDead))
        {
            _currentTarget = null;
        }

        bool justLostTarget = hadTarget && _currentTarget == null;

        if (_searchTimer <= 0f || justLostTarget)
        {
            _searchTimer = TargetSearchInterval;
            _currentTarget = EnemyTargeting.FindNearest(GlobalPosition, _spellInstance.Resolve().Range);
        }

        if (_currentTarget != null && _spellInstance.CanCast)
        {
            CastAtTarget();
        }
    }

    private void CastAtTarget()
    {
        Vector3 targetPosition = _currentTarget.GlobalPosition;
        Vector3 levelAimPosition = new Vector3(targetPosition.X, SpawnPoint.GlobalPosition.Y, targetPosition.Z);
        SpawnPoint.LookAt(levelAimPosition, Vector3.Up);

        SpellCastContext context = _spellInstance.Resolve();
        CastEffect effect = _castEffects.FirstOrDefault(e => e.CanHandle(context));
        effect?.Cast(context, SpawnPoint);

        _spellInstance.StartCast(context.CastTime, context.Cooldown);
    }
}
