using Godot;
using System.Linq;

// Gun and SwordAndShield both occupy the same equipment slot, unlike Armor's subclasses (which
// each map to a different one) - Slot lives here on the shared base instead of being repeated
// identically on both leaves.
public abstract partial class Weapon : Item
{
    [Export] public float BaseAttackDamage { get; protected set; }
    [Export] public virtual float BaseAttackSpeed { get; protected set; }
    [Export] public float BaseCritChance { get; protected set; } = 0.05f;
    [Export] public float BaseCritDamageBonus { get; protected set; } = 0.5f;

    public override EquipmentSlot Slot => EquipmentSlot.Weapon;

    // Sets this weapon's rarity-scaled base damage - each concrete weapon type has its own starting
    // value and per-rarity increment (WeaponBaseStats), the way Armor.RollBaseStats scales off
    // ArmorBaseStats.
    public abstract void RollBaseStats(Rarity rarity);

    public float ResolvedBaseCritChance =>
        BaseCritChance + Affixes.Where(roll => roll.Stat == StatId.SentinelBaseCritChance).Sum(roll => roll.Value);

    public float CritDamageBonusMoreMultiplier =>
        1f + Affixes.Where(roll => roll.Stat == StatId.SentinelMoreCritDamageBonus).Sum(roll => roll.Value);

    public float ResolvedBaseAttackSpeed =>
        BaseAttackSpeed * (1f + Affixes.Where(roll => roll.Stat == StatId.SentinelBaseAttackSpeed).Sum(roll => roll.Value));
}
