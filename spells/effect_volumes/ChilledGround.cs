public partial class ChilledGround : GroundEffect
{
    protected override void OnTick(Enemy enemy)
    {
        enemy.ApplyDebuff(new ChilledGroundAilment(Magnitude, EffectDuration));
    }
}
