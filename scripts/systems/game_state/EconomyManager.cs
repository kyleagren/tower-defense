using Godot;
using System;

public partial class EconomyManager : Node
{
    public static EconomyManager Instance { get; private set; }

    [Export] public int StartingMana = 60;
    [Export] public int MaxCapacity = 1000;

    public int Mana { get; private set; }
    public int CapacityUsed { get; private set; }
    public int GoldEarned { get; private set; }

    public event Action ManaChanged;
    public event Action GoldEarnedChanged;

    // Assigned in _EnterTree rather than _Ready: _EnterTree runs top-down across the whole tree
    // before any node's _Ready, so consumers caching Instance in their own _Ready are safe
    // regardless of where this node sits in the scene.
    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        ResetForMatch();
        EventBus.Instance.EnemyKilled += OnEnemyKilled;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.EnemyKilled -= OnEnemyKilled;
    }

    // Restores starting match state. Called on load; also the hook a "try again" button can call
    // to restart a failed match without reloading the scene.
    public void ResetForMatch()
    {
        Mana = StartingMana;
        CapacityUsed = 0;
        GoldEarned = 0;
        ManaChanged?.Invoke();
        GoldEarnedChanged?.Invoke();
    }

    private void OnEnemyKilled(int reward, int goldReward)
    {
        Mana += reward;
        GoldEarned += goldReward;
        ManaChanged?.Invoke();
        GoldEarnedChanged?.Invoke();
    }

    public bool CanAfford(SpellCastContext context)
    {
        int manaCost = Mathf.CeilToInt(context.ManaCost);
        int capacityCost = Mathf.CeilToInt(context.CapacityCost);
        return CapacityUsed + capacityCost <= MaxCapacity && Mana >= manaCost;
    }

    public (int ManaCost, int CapacityCost) SpendOnTower(SpellCastContext context)
    {
        int manaCost = Mathf.CeilToInt(context.ManaCost);
        int capacityCost = Mathf.CeilToInt(context.CapacityCost);
        Mana -= manaCost;
        CapacityUsed += capacityCost;
        ManaChanged?.Invoke();
        return (manaCost, capacityCost);
    }

    public void RefundTower(int manaSpent, int capacityCost)
    {
        Mana += manaSpent;
        CapacityUsed -= capacityCost;
        ManaChanged?.Invoke();
    }
}
