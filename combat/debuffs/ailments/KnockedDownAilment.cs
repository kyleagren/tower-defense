// Laid out flat on Black Ice: can't move or attack until it gets back up. No cooldown afterward -
// Black Ice's variance is the point - and a fresh one is turned away while one is here, so being down
// is never extended.
public class KnockedDownAilment : Debuff
{
    private static readonly Tag[] _elements = { Tag.Cold };

    private float _remaining;

    public KnockedDownAilment(float duration)
    {
        _remaining = duration;
    }

    public override Tag[] Elements => _elements;

    public override DebuffType Type => DebuffType.KnockedDown;

    public override DebuffApplication ResolveAgainst(Debuff existing) => DebuffApplication.Discard;

    public override bool Tick(Enemy enemy, float delta)
    {
        _remaining -= delta;
        return _remaining > 0f;
    }
}
