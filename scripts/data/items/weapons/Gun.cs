using Godot;

[GlobalClass]
public partial class Gun : Weapon
{
    [Export] public override float BaseAttackSpeed { get; protected set; } = 6f;

    public override void RollBaseStats(Rarity rarity)
    {
        BaseAttackDamage = WeaponBaseStats.GetGunDamage(rarity);
    }
}
