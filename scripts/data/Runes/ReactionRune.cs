public abstract partial class ReactionRune : Rune
{
    public override void Apply(SpellCastContext context)
    {
        context.Reaction = this;
    }

    public abstract void OnHit(Enemy target, DamagePacket incomingDamage);
}
