using Godot;

// README's Weak-Point Visor (Characters). Activates a few of each enemy's pre-placed body-part
// weak-point markers (built into the enemy scenes) for Duration - Head is always included, the
// rest random. A shot through one stacks a flat more-damage multiplier (WeakPointMultiplier) on top
// of whatever the crit roll already gave it. Delegates the actual marking to LevelContext rather
// than looping its enemies itself, since that state is also what lets an enemy spawning
// mid-duration expose its own markers on _Ready. Runs for a duration, and a second press ends it
// early - the cooldown starts from whichever of those actually ended it.
public partial class WeakPointVisorAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 20f;
    [Export] public float Duration { get; private set; } = 8f;
    [Export] public int WeakPointCount { get; private set; } = 3;
    [Export] public float WeakPointDamageMultiplier { get; private set; } = 1.3f;
    [Export] public Color CueColor { get; private set; } = new Color(1f, 0.8f, 0.2f);

    public override string DisplayName => "Weak-Point Visor";
    public override string Description => "Exposes weak points on every enemy.";
    public override bool CanToggleOff => true;

    public override float ActiveFraction => _activeDuration <= 0f ? 0f : _durationRemaining / _activeDuration;
    public override bool HasCrosshairTimer => true;
    public override float? CrosshairTimerFraction => IsActive ? ActiveFraction : null;
    public override Color CrosshairCueColor => CueColor;

    // Ability damage scales only the bonus part - scaling the whole multiplier would compound far
    // too hard on top of headshot crits. Read per shot, so re-gearing mid-Visor applies at once.
    public float WeakPointMultiplier =>
        1f + (WeakPointDamageMultiplier - 1f) * (1f + EffectiveBonus(AbilityStatKind.Damage));

    private float _activeDuration;
    private float _durationRemaining;
    private LevelContext _level;

    public WeakPointVisorAbility()
    {
        DamageEffectiveness = 0.5f;
    }

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
        _activeDuration = ScaledDuration(Duration);
        _durationRemaining = _activeDuration;
        _level = LevelContext.Current;
        _level.ActivateWeakPointVisor(WeakPointCount);
    }

    public override void OnCharacterDeactivated()
    {
        if (IsActive)
        {
            End();
        }
    }

    // Freed mid-duration (a hub character swap) would otherwise leave every enemy's markers lit.
    public override void _ExitTree()
    {
        if (IsActive)
        {
            End();
        }
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
        _level.DeactivateWeakPointVisor();
    }
}
