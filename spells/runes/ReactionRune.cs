public abstract partial class ReactionRune : Rune
{
    public override void Apply(SpellCastContext context)
    {
        context.Reaction = this;
    }

    public abstract bool OnHit(Enemy target, DamagePacket incomingDamage);
}
