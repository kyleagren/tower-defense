public abstract class SpellCastContext
{
    public float Range;
    public float CastTime;
    public float Cooldown;
    public DamagePacket Damage;
    public float IncreasedDamagePercent;
    public float MoreDamageMultiplier = 1f;
    public float RagdollForce;
    public float AreaMultiplier = 1f;
}
