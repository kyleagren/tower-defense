using Godot;

// README's Weak-Point Visor (Characters). Runs for a duration, and a second press ends it early -
// the cooldown starts from whichever of those actually ended it.
// The weak-point exposure itself isn't built yet (Enemy has no such concept); Activate/End are
// the timing shell only.
public partial class WeakPointVisorAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 20f;
    [Export] public float Duration { get; private set; } = 8f;

    public override string DisplayName => "Weak-Point Visor";
    public override string Description => "Exposes a weak point on every enemy.";
    public override bool CanToggleOff => true;

    private float _durationRemaining;

    public override bool TryActivate()
    {
        if (IsActive)
        {
            End();
            return true;
        }

        return base.TryActivate();
    }

    protected override void Activate()
    {
        IsActive = true;
        _durationRemaining = Duration;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!IsActive)
        {
            return;
        }

        _durationRemaining -= (float)delta;

        if (_durationRemaining <= 0f)
        {
            End();
        }
    }

    private void End()
    {
        IsActive = false;
        StartCooldown();
    }
}
