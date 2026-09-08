using Godot;
using System.Linq;

public abstract partial class Armor : Item
{
    [Export] public BaseType BaseType { get; private set; }
    [Export] public Godot.Collections.Array<RolledBaseStat> BaseStats { get; private set; } = new();

    // Chest/Legplates carry double the base total of the other three slots.
    protected virtual float SlotWeight => 1f;

    public void RollBaseStats(Rarity rarity, System.Random random)
    {
        BaseType = ArmorBaseTypeTable.Random(random);
        StatId[] grantedStats = ArmorBaseTypeTable.GetGrantedStats(BaseType);
        float total = ArmorBaseStats.GetTotal(rarity, SlotWeight);
        float perStat = total / grantedStats.Length;

        BaseStats = new Godot.Collections.Array<RolledBaseStat>();
        foreach (StatId stat in grantedStats)
        {
            // Shield is worth half as much per point as Armor - a pure Shield item gets half its
            // slot's total, and a Hybrid item's Shield share is halved a second time on top of the
            // even split it already takes with Armor (total/2 -> total/4).
            float value = stat == StatId.SentinelShield ? perStat * 0.5f : perStat;
            BaseStats.Add(new RolledBaseStat { Stat = stat, Value = value });
        }
    }

    // This item's own base value for a stat, times its own rolled local "+X%" affixes for that
    // same stat - each item resolves fully on its own, no character-level multiplier layer.
    public float ResolvedValue(StatId stat)
    {
        float baseValue = BaseStats.Where(entry => entry.Stat == stat).Sum(entry => entry.Value);
        if (baseValue == 0f)
        {
            return 0f;
        }

        float localPercent = Affixes
            .Where(roll => roll.Stat == stat || roll.Stat == StatId.SentinelArmorAndShield)
            .Sum(roll => roll.Value);

        return baseValue * (1f + localPercent);
    }
}
