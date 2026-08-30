using Godot;
using System;

public partial class EconomyManager : Node
{
    public static EconomyManager Instance { get; private set; }

    [Export] public int StartingMana = 60;
    [Export] public int ManaPerRune = 5;
    [Export] public int MaxCapacity = 5;

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

    public int GetTowerCost(Spell spell, int runeCount)
    {
        return spell.ManaCost + ManaPerRune * runeCount;
    }

    public bool CanAfford(Spell spell, int runeCount)
    {
        return CapacityUsed < MaxCapacity && Mana >= GetTowerCost(spell, runeCount);
    }

    public int SpendOnTower(Spell spell, int runeCount)
    {
        int cost = GetTowerCost(spell, runeCount);
        Mana -= cost;
        CapacityUsed++;
        ManaChanged?.Invoke();
        return cost;
    }

    public void RefundTower(int manaSpent)
    {
        Mana += manaSpent;
        CapacityUsed--;
        ManaChanged?.Invoke();
    }
}
