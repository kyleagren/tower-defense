using Godot;
using System;

public partial class EconomyManager : Node
{
    public static EconomyManager Instance { get; private set; }

    [Export] public int StartingBudget = 60;
    [Export] public int BudgetPerRune = 5;
    [Export] public int MaxCapacity = 5;

    public int Budget { get; private set; }
    public int CapacityUsed { get; private set; }

    public event Action BudgetChanged;

    public override void _Ready()
    {
        Instance = this;
        Budget = StartingBudget;
        EventBus.Instance.EnemyKilled += OnEnemyKilled;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.EnemyKilled -= OnEnemyKilled;
    }

    private void OnEnemyKilled(int reward)
    {
        Budget += reward;
        BudgetChanged?.Invoke();
    }

    public int GetTowerCost(Spell spell, int runeCount)
    {
        return spell.BudgetCost + BudgetPerRune * runeCount;
    }

    public bool CanAfford(Spell spell, int runeCount)
    {
        return CapacityUsed < MaxCapacity && Budget >= GetTowerCost(spell, runeCount);
    }

    public int SpendOnTower(Spell spell, int runeCount)
    {
        int cost = GetTowerCost(spell, runeCount);
        Budget -= cost;
        CapacityUsed++;
        BudgetChanged?.Invoke();
        return cost;
    }

    public void RefundTower(int budgetSpent)
    {
        Budget += budgetSpent;
        CapacityUsed--;
        BudgetChanged?.Invoke();
    }
}
