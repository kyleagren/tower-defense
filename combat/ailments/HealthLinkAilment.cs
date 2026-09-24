using Godot;

// Half of a Health Link. Both enemies in a linked pair carry one pointing at the other, created
// together by Link(). Two independent halves rather than one shared object: the pair is symmetric
// by construction, and neither half has to outlive the other's death to be cleaned up.
public class HealthLinkAilment : Ailment
{
    public Enemy Partner { get; }
    public float MirrorPercent { get; }

    private float _remainingDuration;

    private HealthLinkAilment(Enemy partner, float duration, float mirrorPercent)
    {
        Partner = partner;
        _remainingDuration = duration;
        MirrorPercent = mirrorPercent;
    }

    // One link per partner rather than one per enemy - an enemy linked to two others carries two,
    // and only a second link to the same partner is a conflict.
    public override bool OccupiesSameSlotAs(Ailment other) =>
        other is HealthLinkAilment link && link.Partner == Partner;

    protected override float Magnitude => MirrorPercent;

    // False when this pair already carries a link the incoming one can't beat. An equal-strength
    // re-link refreshes the duration, but a weaker one is refused outright - otherwise two players
    // could hold a strong link open indefinitely by taking turns re-casting a weak one. That
    // comparison is Ailment.ResolveAgainst's default, reached through Magnitude above.
    public static bool Link(Enemy a, Enemy b, float duration, float mirrorPercent)
    {
        if (a == null || b == null || a == b || a.IsDead || b.IsDead)
        {
            return false;
        }

        // Symmetric, so the two halves stand or fall together: if the first is refused by a stronger
        // link already on this pair, the second would be too.
        if (!a.ApplyAilment(new HealthLinkAilment(b, duration, mirrorPercent)))
        {
            return false;
        }

        b.ApplyAilment(new HealthLinkAilment(a, duration, mirrorPercent));
        return true;
    }

    public override bool Tick(Enemy enemy, float delta)
    {
        if (!GodotObject.IsInstanceValid(Partner) || Partner.IsDead)
        {
            return false;
        }

        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }

    public override void OnHostDamaged(Enemy host, Hit hit)
    {
        if (!GodotObject.IsInstanceValid(Partner) || Partner.IsDead)
        {
            return;
        }

        DamagePacket mirrored = hit.Damage * MirrorPercent;
        mirrored.IsCrit = false;
        mirrored.IsTrueDamage = true;

        // A Mirrored hit never reaches ailments, which stops it bouncing back through the partner's
        // own half and is also why links never chain - a third enemy linked to the partner is not
        // reached. It sets no Status either, so the partner doesn't inherit an element from a hit
        // that landed somewhere else. The credit stays with whoever dealt the original hit.
        Partner.TakeDamage(new Hit { Damage = mirrored, Attribution = hit.Attribution, Type = HitType.Mirrored });
    }
}
