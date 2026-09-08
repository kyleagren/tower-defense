using Godot;

[GlobalClass]
public partial class SwordAndShield : Weapon
{
    [Export] public override float BaseAttackSpeed { get; protected set; } = 1.2f;

    public override void RollBaseStats(Rarity rarity)
    {
        BaseAttackDamage = WeaponBaseStats.GetSwordDamage(rarity);
    }
}
