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

    public override void _Ready()
    {
        Instance = this;
        Mana = StartingMana;
        EventBus.Instance.EnemyKilled += OnEnemyKilled;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.EnemyKilled -= OnEnemyKilled;
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
