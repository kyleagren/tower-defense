using Godot;

// README's Weak-Point Visor (Characters). Activates a few of each enemy's pre-placed body-part
// weak-point markers (built into the enemy scenes) for Duration - Head is always included, the
// rest random. A shot through one stacks a flat more-damage multiplier
// (Soldier.WeakPointDamageMultiplier) on top of whatever the crit roll already gave it. Delegates
// the actual marking to Enemy's static Activate/DeactivateWeakPointVisor rather than looping
// EnemyRegistry itself, since that flag is also what lets an enemy spawning mid-duration expose
// its own markers on _Ready. Runs for a duration, and a second press ends it early - the cooldown
// starts from whichever of those actually ended it.
public partial class WeakPointVisorAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 20f;
    [Export] public float Duration { get; private set; } = 8f;
    [Export] public int WeakPointCount { get; private set; } = 3;

    public override string DisplayName => "Weak-Point Visor";
    public override string Description => "Exposes weak points on every enemy.";
    public override bool CanToggleOff => true;

    public override float ActiveFraction => Duration <= 0f ? 0f : _durationRemaining / Duration;

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
        Enemy.ActivateWeakPointVisor(WeakPointCount);
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
        Enemy.DeactivateWeakPointVisor();
    }
}
