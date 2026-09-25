using Godot;

// One of a Sentinel's four active ability slots. A Node rather than a Resource so each ability owns
// its own state, tweens and scene content directly, with no separate instance object holding the
// mutable half - the same shape RitualPlacer/TokenDeployer/LookInteractor already use for a
// Sentinel's other behaviors. This base carries only what the shared input handler and the HUD
// need; what an ability actually does is entirely bespoke to its subclass.
public abstract partial class Ability : Node
{
    public abstract string DisplayName { get; }
    public abstract string Description { get; }

    public virtual float BaseCooldown { get; protected set; } = 10f;

    public bool IsReady => _cooldownRemaining <= 0f;
    public bool IsActive { get; protected set; }

    // 1 the instant a cooldown starts, falling to 0 as it recovers - the HUD reads this rather
    // than raw seconds so a radial sweep needs no per-ability knowledge.
    public float CooldownFraction => _cooldownDuration <= 0f ? 0f : _cooldownRemaining / _cooldownDuration;

    // 1 the instant an active duration begins, falling to 0 as it runs out - lets the HUD drive a
    // depleting radial while IsActive the same way CooldownFraction drives a filling one once it
    // ends. An ability with no meaningful duration (an instant effect) just stays at 1 while active.
    public virtual float ActiveFraction => 1f;

    // HUD affordance only. The shared handler calls TryActivate either way; an ability that ends
    // early on a second press implements that itself.
    public virtual bool CanToggleOff => false;

    // True while the ability is consuming the attack binding for its own targeting. The character
    // suppresses its weapon rather than firing both off one click, and a click the ability took
    // never becomes weapon fire even once the capture ends - see Sentinel.CanUseWeaponInput.
    public virtual bool CapturesAttackInput => false;

    // Backing out with ui_cancel while capturing. What that costs is the ability's own call.
    public virtual void CancelCapture()
    {
    }

    // False for a capture that Escape shouldn't back out of - Escape then falls through to its
    // ordinary behavior instead of being swallowed.
    public virtual bool CanCancelCapture => true;

    // An ability with a hold variant defers its press: releasing before GameRules.AbilityHoldThreshold
    // is a tap (TryActivate), and holding to the threshold fires TryActivateHold the moment it's
    // reached. Without one, a press fires TryActivate straight away.
    public virtual bool HasHoldVariant => false;

    public virtual bool TryActivateHold()
    {
        return false;
    }

    // Null for an ability with no gauge. AbilityGauge shows the first slot that has one.
    public virtual float? GaugeFraction => null;

    // What the gauge's fill is tinted by - null for no element.
    public virtual Tag? GaugeElement => null;

    // Called when the wielding Sentinel dies, after any capture has been cancelled.
    public virtual void OnCharacterDied()
    {
    }

    // Node.Owner is Godot's own scene-ownership reference, so the wielding Sentinel can't reuse
    // that name - Character matches what RitualPlacer/TokenDeployer already call the same thing.
    protected Sentinel Character { get; private set; }

    private float _cooldownRemaining;
    private float _cooldownDuration;
    private bool _isHeld;
    private float _heldTime;

    public override void _Ready()
    {
        Character = GetParent<Sentinel>();
    }

    // A subclass that needs per-frame work of its own must call base._PhysicsProcess(delta) - the
    // cooldown timer runs here.
    public override void _PhysicsProcess(double delta)
    {
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - (float)delta);
        }

        if (_isHeld)
        {
            UpdateHold((float)delta);
        }
    }

    public void Press()
    {
        if (!HasHoldVariant)
        {
            TryActivate();
            return;
        }

        _isHeld = true;
        _heldTime = 0f;
    }

    public void Release()
    {
        if (!_isHeld)
        {
            return;
        }

        _isHeld = false;
        TryActivate();
    }

    // Losing ability input mid-hold (death, token pick mode) drops the press rather than letting it
    // fire later - and a release arriving while input is gated would never reach Release anyway.
    private void UpdateHold(float delta)
    {
        if (!Character.CanUseAbilityInput)
        {
            _isHeld = false;
            return;
        }

        _heldTime += delta;

        if (_heldTime >= GameRules.AbilityHoldThreshold)
        {
            _isHeld = false;
            TryActivateHold();
        }
    }

    public virtual bool TryActivate()
    {
        if (IsActive || !IsReady)
        {
            return false;
        }

        Activate();
        return true;
    }

    protected abstract void Activate();

    // Resolved when the cooldown actually starts rather than when the ability began, so one that
    // runs for a duration and can be cut short recovers from the point it really ended. Recovery
    // rate divides in, matching SpellCastContext.ResolvedCooldown's shape.
    protected void StartCooldown()
    {
        float recoveryRate = 1f + Character.Sheet.GetBonus(StatId.SentinelCooldownRecoveryRate);
        _cooldownDuration = BaseCooldown / recoveryRate;
        _cooldownRemaining = _cooldownDuration;
    }
}
