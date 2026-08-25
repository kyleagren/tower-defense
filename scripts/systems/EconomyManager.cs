using Godot;

public partial class EconomyManager : Node
{
    [Export] public int StartingBudget = 60;
    [Export] public int BudgetPerRune = 5;
    [Export] public int MaxCapacity = 5;

    public int Budget { get; private set; }
    public int CapacityUsed { get; private set; }

    public override void _Ready()
    {
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
        return cost;
    }

    public void RefundTower(int budgetSpent)
    {
        Budget += budgetSpent;
        CapacityUsed--;
    }
}
