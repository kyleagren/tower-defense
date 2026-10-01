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

    // Another ability that goes on cooldown whenever this one does - Converge and Repel, which share a
    // slot and one field between them. Wired both ways in the scene.
    [Export] public Ability CooldownPartner { get; private set; }

    // How strongly this ability responds to each kind of ability stat from gear - a lever for keeping
    // one ability's scaling from running away without touching the mods themselves. Applies to the
    // generic and per-slot bonus together. A subclass wanting a different default sets it in its
    // constructor, so the Inspector still shows the real default.
    [Export] public float CooldownRecoveryEffectiveness { get; protected set; } = 1f;
    [Export] public float AreaEffectiveness { get; protected set; } = 1f;
    [Export] public float DurationEffectiveness { get; protected set; } = 1f;
    [Export] public float DamageEffectiveness { get; protected set; } = 1f;

    // Which slot this ability sits in - what its per-slot gear stats are rolled against. A hold
    // ability shares its slot's index. -1 until the Sentinel assigns it.
    public int SlotIndex { get; private set; } = -1;

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

    // While this is active, no other ability can be started - Retaliation's stance. One already
    // running carries on.
    public virtual bool LocksOutOtherAbilities => false;

    // Null for an ability with no gauge. AbilityGauge shows the first slot that has one.
    public virtual float? GaugeFraction => null;

    // What the gauge's fill is tinted by - null for no element.
    public virtual Tag? GaugeElement => null;

    // What the crosshair shows for this ability, polled each frame by CrosshairCue: the shots left
    // while it holds the attack binding, and the fraction left on a timer. Null shows nothing.
    public virtual CrosshairShots? CrosshairShots => null;
    public virtual float? CrosshairTimerFraction => null;

    // An ability that ever shows a crosshair timer keeps its own row for it, so a bar's position
    // says which ability it belongs to even while the others are idle.
    public virtual bool HasCrosshairTimer => false;
    public virtual Color CrosshairCueColor => Colors.White;

    // Called when the wielding Sentinel dies, after any capture has been cancelled.
    public virtual void OnCharacterDied()
    {
    }

    // Called when the wielding Sentinel hands control back to the Architect at the end of a wave,
    // after any capture has been cancelled. Something the ability left out in the world is its own
    // call to keep or clear.
    public virtual void OnCharacterDeactivated()
    {
    }

    // Node.Owner is Godot's own scene-ownership reference, so the wielding Sentinel can't reuse
    // that name - Character matches what RitualPlacer/TokenDeployer already call the same thing.
    protected Sentinel Character { get; private set; }

    private float _cooldownRemaining;
    private float _cooldownDuration;

    public override void _Ready()
    {
        Character = GetParent<Sentinel>();
    }

    public void AssignSlot(int slot)
    {
        SlotIndex = slot;
    }

    // Read by each ability when it activates, so re-gearing mid-cast doesn't reshape a cast already
    // running.
    protected float ScaledArea(float baseValue) => baseValue * (1f + EffectiveBonus(AbilityStatKind.Area));
    protected float ScaledDuration(float baseValue) => baseValue * (1f + EffectiveBonus(AbilityStatKind.Duration));
    protected float ScaledDamage(float baseValue) => baseValue * (1f + EffectiveBonus(AbilityStatKind.Damage));

    // The gear bonus for one kind of ability stat - the generic one and this slot's own, added
    // together - scaled by this ability's effectiveness for that kind.
    protected float EffectiveBonus(AbilityStatKind kind)
    {
        CharacterSheet sheet = Character.Sheet;
        float bonus = sheet.GetBonus(AbilityStats.Generic(kind));

        if (SlotIndex >= 0)
        {
            bonus += sheet.GetBonus(AbilityStats.For(SlotIndex, kind));
        }

        return bonus * Effectiveness(kind);
    }

    private float Effectiveness(AbilityStatKind kind) => kind switch
    {
        AbilityStatKind.CooldownRecoveryRate => CooldownRecoveryEffectiveness,
        AbilityStatKind.Area => AreaEffectiveness,
        AbilityStatKind.Duration => DurationEffectiveness,
        _ => DamageEffectiveness
    };

    // A subclass that needs per-frame work of its own must call base._PhysicsProcess(delta) - the
    // cooldown timer runs here.
    public override void _PhysicsProcess(double delta)
    {
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - (float)delta);
        }
    }

    public virtual bool TryActivate()
    {
        if (IsActive || !IsReady || !CanActivate())
        {
            return false;
        }

        Activate();
        return true;
    }

    // A last check before activating, for an ability with its own precondition - somewhere to place
    // a field, a full gauge to spend.
    protected virtual bool CanActivate()
    {
        return true;
    }

    protected abstract void Activate();

    // Resolved when the cooldown actually starts rather than when the ability began, so one that
    // runs for a duration and can be cut short recovers from the point it really ended. Recovery
    // rate divides in, matching SpellCastContext.ResolvedCooldown's shape.
    protected void StartCooldown()
    {
        float recoveryRate = 1f + EffectiveBonus(AbilityStatKind.CooldownRecoveryRate);
        SetCooldown(BaseCooldown / recoveryRate);
        CooldownPartner?.SetCooldown(_cooldownDuration);
    }

    private void SetCooldown(float duration)
    {
        _cooldownDuration = duration;
        _cooldownRemaining = duration;
    }
}
