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

    // HUD affordance only. The shared handler calls TryActivate either way; an ability that ends
    // early on a second press implements that itself.
    public virtual bool CanToggleOff => false;

    // Node.Owner is Godot's own scene-ownership reference, so the wielding Sentinel can't reuse
    // that name - Character matches what RitualPlacer/TokenDeployer already call the same thing.
    protected Sentinel Character { get; private set; }

    private float _cooldownRemaining;
    private float _cooldownDuration;

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
        float recoveryRate = 1f + CharacterSheetCache.GetSheet(Character.Id).GetBonus(StatId.SentinelCooldownRecoveryRate);
        _cooldownDuration = BaseCooldown / recoveryRate;
        _cooldownRemaining = _cooldownDuration;
    }
}
