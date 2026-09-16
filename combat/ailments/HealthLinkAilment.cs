using Godot;

// Half of a Health Link. Both enemies in a linked pair carry one pointing at the other, created
// together by Link(). Two independent halves rather than one shared object: the pair is symmetric
// by construction, and neither half has to outlive the other's death to be cleaned up.
public class HealthLinkAilment : Ailment
{
    public Enemy Partner { get; }
    public float MirrorPercent { get; private set; }

    private float _remainingDuration;

    private HealthLinkAilment(Enemy partner, float duration, float mirrorPercent)
    {
        Partner = partner;
        _remainingDuration = duration;
        MirrorPercent = mirrorPercent;
    }

    // False when this pair already carries a link the incoming one can't beat. An equal-strength
    // re-link refreshes the duration, but a weaker one is refused outright - otherwise two players
    // could hold a strong link open indefinitely by taking turns re-casting a weak one.
    public static bool Link(Enemy a, Enemy b, float duration, float mirrorPercent)
    {
        if (a == null || b == null || a == b || a.IsDead || b.IsDead)
        {
            return false;
        }

        HealthLinkAilment existing = FindLink(a, b);

        if (existing == null)
        {
            a.ApplyAilment(new HealthLinkAilment(b, duration, mirrorPercent));
            b.ApplyAilment(new HealthLinkAilment(a, duration, mirrorPercent));
            return true;
        }

        if (mirrorPercent < existing.MirrorPercent)
        {
            return false;
        }

        Refresh(existing, duration, mirrorPercent);
        Refresh(FindLink(b, a), duration, mirrorPercent);
        return true;
    }

    public static HealthLinkAilment FindLink(Enemy host, Enemy partner)
    {
        foreach (Ailment ailment in host.Ailments)
        {
            if (ailment is HealthLinkAilment link && link.Partner == partner)
            {
                return link;
            }
        }

        return null;
    }

    private static void Refresh(HealthLinkAilment link, float duration, float mirrorPercent)
    {
        if (link == null)
        {
            return;
        }

        link._remainingDuration = duration;
        link.MirrorPercent = mirrorPercent;
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

    public override void OnHostDamaged(Enemy host, DamagePacket damage)
    {
        if (!GodotObject.IsInstanceValid(Partner) || Partner.IsDead)
        {
            return;
        }

        DamagePacket mirrored = damage * MirrorPercent;
        mirrored.IsCrit = false;
        mirrored.IsTrueDamage = true;

        // notifyAilments stops this bouncing back through the partner's own half, and is also why
        // links never chain - a third enemy linked to the partner is not reached. updateStatus stops
        // the partner inheriting an element from a hit that landed somewhere else.
        Partner.TakeDamage(mirrored, updateStatus: false, notifyAilments: false);
    }
}
