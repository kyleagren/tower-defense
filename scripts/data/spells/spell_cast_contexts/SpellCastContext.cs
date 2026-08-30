public class SpellCastContext
{
    // Every ScaledStat is initialized here: default(ScaledStat) has More = 0, which would resolve
    // to 0. Assigning a float goes through ScaledStat's implicit conversion (Increased 0, More 1).
    public ScaledStat Range = 0f;
    public ScaledStat CastTime = 0f;
    public ScaledStat CastSpeed = 1f;
    public ScaledStat Cooldown = 0f;
    public ScaledStat RagdollForce = 0f;
    public ScaledStat ManaCost = 0f;
    public ScaledStat CapacityCost = 0f;

    // Damage can't itself be a ScaledStat — it's a packet of per-element floats — so the scale
    // sits beside it and Damage exposes the resolved packet. Readers use Damage as before.
    public DamagePacket UnscaledDamage;
    public ScaledStat DamageScale = 1f;
    public DamagePacket Damage => UnscaledDamage * DamageScale.Value;

    // Cast speed reduces cast time rather than scaling it, so the two resolve together.
    public float ResolvedCastTime => CastTime.Value / CastSpeed.Value;

    public ProjectileCastData Projectile;
    public ChainCastData Chain;
    public AreaCastData Area;
    public InstantHitCastData InstantHit;
    public ReactionRune Reaction;
    public WallCastData Wall;
    public CastTrigger CastTrigger;
}
