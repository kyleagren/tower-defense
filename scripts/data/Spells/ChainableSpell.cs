public abstract partial class ChainableSpell : Spell
{
    public virtual int BaseChainCount { get; protected set; } = 0;
    public virtual float BaseChainRange { get; protected set; } = 6f;
    public virtual float MoreDamagePerRemainingChain { get; protected set; } = 0f;
}
