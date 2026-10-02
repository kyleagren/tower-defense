using Godot;

public abstract partial class OnHitRune : Rune
{
    public override void Apply(SpellCastContext context) => context.OnHitRunes.Add(this);

    // What the Ritual details panel calls one use of this rune's effect - null for a rune with
    // nothing to count.
    public virtual string EffectLabel => null;

    // Runs the moment a spell's damage lands on an enemy, before the hit resolves. Returns true only
    // if it consumed that enemy's Status, which is what stops the incoming hit's own element
    // replacing it. A rune that merely adds an effect of its own returns false.
    public abstract bool OnHit(OnHitContext hit);

    // Runs after the hit's damage has landed, Status and death included - for a rune that cares how
    // the hit turned out (an execute, an on-kill effect) rather than what it carries. Free to deal
    // new hits, since nothing is mid-resolution any more. Spending a Status belongs to OnHit, before
    // damage.
    public virtual void AfterHit(AfterHitContext hit)
    {
    }

    // Counts one use of this rune's effect against the Ritual whose hit it rode on. Host only, like
    // every on-hit.
    protected void CountEffect(Attribution attribution)
    {
        if (attribution.Source is Ritual ritual && GodotObject.IsInstanceValid(ritual))
        {
            ritual.Stats.RecordRuneEffect(this);
        }
    }
}
