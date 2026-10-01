using Godot;
using System.Collections.Generic;

// README's Overdrive (Characters). Spends a full Harvest gauge on OverdriveShots empowered shots,
// which take over the attack binding until they're fired or expire. Each shot pierces, splits its
// damage across elements in the proportions the gauge was filled with, and applies the default
// ailment of whichever element fed it most.
public partial class OverdriveAbility : Ability
{
    // Shares this slot and owns the gauge. A node local to soldier.tscn, so a typed export is safe.
    [Export] public HarvestAbility Harvest { get; private set; }
    [Export] public override float BaseCooldown { get; protected set; } = 0f;
    [Export] public int OverdriveShots { get; private set; } = 10;
    // Fixed, deliberately ignoring attack speed.
    [Export] public float ShotInterval { get; private set; } = 0.5f;
    [Export] public float Expiry { get; private set; } = 20f;
    [Export] public int PierceCount { get; private set; } = 3;
    // Overdrive has no area of its own, so Area of Effect buys extra pierce instead - one per this
    // much of it, rounded down.
    [Export] public float AreaPerExtraPierce { get; private set; } = 0.33f;
    // Seconds of weapon DPS each shot deals, so a fast gun and a slow heavy one land equal shots.
    [Export] public float DpsSecondsPerShot { get; private set; } = 1f;
    // A share of the shot's own Fire damage.
    [Export] public float BurnDamageFraction { get; private set; } = 0.5f;
    [Export] public float BurnDuration { get; private set; } = 3f;
    [Export] public float ChillPercent { get; private set; } = 0.3f;
    [Export] public float ChillDuration { get; private set; } = 3f;

    public override string DisplayName => "Overdrive";
    public override string Description => "Spend a full Harvest gauge on piercing elemental shots.";

    public override bool CapturesAttackInput => IsActive;
    public override bool CanCancelCapture => false;
    // Only while spending - the gauge itself is Harvest's.
    public override float? GaugeFraction => IsActive ? ShotsFraction : null;
    public override Tag? GaugeElement => _ailmentElement;

    public override CrosshairShots? CrosshairShots => IsActive
        ? new CrosshairShots(CrosshairShotStyle.Row, _shotsRemaining, OverdriveShots)
        : null;

    public override bool HasCrosshairTimer => true;
    public override float? CrosshairTimerFraction => IsActive && _expiryDuration > 0f ? _expiryRemaining / _expiryDuration : null;
    public override Color CrosshairCueColor => DamageTypeColors.Get(_ailmentElement);

    private float ShotsFraction => OverdriveShots <= 0 ? 0f : (float)_shotsRemaining / OverdriveShots;

    private int _shotsRemaining;
    private float _expiryDuration;
    private float _expiryRemaining;
    private float _shotCooldown;
    private int _pierceCount;
    private Dictionary<Tag, float> _shares;
    private Tag? _ailmentElement;

    private Soldier Wielder => (Soldier)Character;

    protected override bool CanActivate()
    {
        return Harvest.IsGaugeFull;
    }

    protected override void Activate()
    {
        Harvest.TryConsumeFullGauge(out _shares, out _ailmentElement);

        IsActive = true;
        _shotsRemaining = OverdriveShots;
        _expiryDuration = ScaledDuration(Expiry);
        _expiryRemaining = _expiryDuration;
        _pierceCount = PierceCount + Mathf.FloorToInt(EffectiveBonus(AbilityStatKind.Area) / AreaPerExtraPierce);
        _shotCooldown = 0f;
        Wielder.SetWeaponTint(DamageTypeColors.Get(_ailmentElement));
    }

    public override void OnCharacterDied()
    {
        if (IsActive)
        {
            EndOverdrive();
        }
    }

    public override void OnCharacterDeactivated()
    {
        if (IsActive)
        {
            EndOverdrive();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Health Link's spikes, or any other capture layered on top, take the click first.
        if (!IsActive || !Character.CanUseAbilityInput || Character.IsAnotherAbilityCapturing(this)
            || !InputPress.Began(@event, "attack"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();

        if (_shotCooldown <= 0f)
        {
            FireShot();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!IsActive)
        {
            return;
        }

        _shotCooldown -= (float)delta;
        _expiryRemaining -= (float)delta;

        if (_expiryRemaining <= 0f)
        {
            EndOverdrive();
        }
    }

    // Re-casts the ray with each enemy hit excluded, so every enemy along the line gets its own
    // headshot, crit and weak-point check at full damage.
    private void FireShot()
    {
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 forward = -Character.Camera.GlobalTransform.Basis.Z;
        Vector3 end = origin + forward * Wielder.AttackRange;
        float baseDamage = ScaledDamage(Character.AttackDamage * Character.AttackSpeed * DpsSecondsPerShot);

        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Enemies;

        Godot.Collections.Array<Rid> pierced = new Godot.Collections.Array<Rid>();
        Vector3 tracerEnd = end;

        for (int i = 0; i < _pierceCount; i++)
        {
            Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
            if (result.Count == 0 || result["collider"].As<Node3D>() is not Enemy enemy)
            {
                break;
            }

            Vector3 hitPosition = (Vector3)result["position"];
            Wielder.ShowImpact(hitPosition);
            HitEnemy(enemy, origin, end, baseDamage);

            // The last enemy it can pierce stops the tracer; fewer than that and it runs full range.
            if (i == _pierceCount - 1)
            {
                tracerEnd = hitPosition;
            }

            pierced.Add(enemy.GetRid());
            query.Exclude = pierced;
        }

        Wielder.ShowTracer(tracerEnd);

        _shotsRemaining--;
        _shotCooldown = ShotInterval;

        if (_shotsRemaining <= 0)
        {
            EndOverdrive();
        }
    }

    private void HitEnemy(Enemy enemy, Vector3 rayOrigin, Vector3 rayEnd, float baseDamage)
    {
        DamagePacket damage = SplitAcrossElements(Wielder.RollShotDamage(enemy, rayOrigin, rayEnd, baseDamage));

        enemy.TakeDamage(new Hit { Damage = damage, Attribution = Character.HitAttribution, Type = HitType.Unmarked });

        if (!enemy.IsDead)
        {
            ApplyAilment(enemy, damage);
        }
    }

    private DamagePacket SplitAcrossElements(DamagePacket rolled)
    {
        DamagePacket split = new DamagePacket { Source = rolled.Source, IsCrit = rolled.IsCrit };
        float total = rolled.Total;

        foreach (KeyValuePair<Tag, float> share in _shares)
        {
            split.AddElement(share.Key, total * share.Value);
        }

        return split;
    }

    // Lightning's Shock and Water's Drenched aren't built, so shots led by those apply nothing yet.
    private void ApplyAilment(Enemy enemy, DamagePacket damage)
    {
        switch (_ailmentElement)
        {
            case Tag.Fire:
                enemy.ApplyDebuff(new BurnAilment(damage.FireDamage * BurnDamageFraction, BurnDuration, Character.HitAttribution));
                break;
            case Tag.Cold:
                enemy.ApplyDebuff(new ChillAilment(ChillPercent, ChillDuration));
                break;
        }
    }

    private void EndOverdrive()
    {
        Wielder.SetWeaponTint(null);
        IsActive = false;
        _shotsRemaining = 0;
        _shares = null;
    }
}
