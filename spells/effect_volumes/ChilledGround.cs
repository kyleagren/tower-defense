public partial class ChilledGround : GroundEffect
{
    protected override void OnTick(Enemy enemy)
    {
        enemy.ApplyAilment(new ChilledGroundAilment(Magnitude, EffectDuration));
    }
}
