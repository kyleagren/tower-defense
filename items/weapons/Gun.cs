using Godot;

[GlobalClass]
[SaveTag("gun")]
public partial class Gun : Weapon
{
    [Export] public override float BaseAttackSpeed { get; protected set; } = 6f;
    [Export] public override float BaseCritDamageBonus { get; protected set; } = 0.25f;

    public override void RollBaseStats(Rarity rarity)
    {
        BaseAttackDamage = WeaponBaseStats.GetGunDamage(rarity);
    }
}
