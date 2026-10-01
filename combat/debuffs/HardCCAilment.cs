// A crowd-control ailment that locks an enemy down for its active duration, then stays in its slot,
// recovering, for its recovery duration - the per-enemy cooldown that stops towers taking turns to
// keep it locked. Keyed by class like any debuff, so every source of the same CC shares the one
// cooldown. A new one is always discarded while one is here, active or recovering.
public abstract class HardCCAilment : Debuff
{
    private float _activeRemaining;
    private float _recoveryRemaining;

    protected HardCCAilment(float activeDuration, float recoveryDuration)
    {
        _activeRemaining = activeDuration;
        _recoveryRemaining = recoveryDuration;
    }

    public override bool IsRecovering => _activeRemaining <= 0f;

    public override DebuffApplication ResolveAgainst(Debuff existing) => DebuffApplication.Discard;

    public override bool Tick(Enemy enemy, float delta)
    {
        if (_activeRemaining > 0f)
        {
            _activeRemaining -= delta;
            return true;
        }

        _recoveryRemaining -= delta;
        return _recoveryRemaining > 0f;
    }
}
