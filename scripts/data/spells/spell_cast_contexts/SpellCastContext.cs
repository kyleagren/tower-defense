public class SpellCastContext
{
    public float Range;
    public float CastTime;
    public float Cooldown;
    public DamagePacket Damage;
    public float IncreasedDamagePercent;
    public float MoreDamageMultiplier = 1f;
    public float RagdollForce;
    public float IncreasedRangePercent;
    public float MoreRangeMultiplier = 1f;
    public float CastSpeedMultiplier = 1f;
    public float ManaCost;
    public float IncreasedManaCostPercent;
    public float MoreManaCostMultiplier = 1f;
    public float CapacityCost;
    public float IncreasedCapacityCostPercent;
    public float MoreCapacityCostMultiplier = 1f;

    public ProjectileCastData Projectile;
    public ChainCastData Chain;
    public AreaCastData Area;
    public InstantHitCastData InstantHit;
    public ReactionRune Reaction;
}
