using Godot;
using System.Collections.Generic;

// README's Status Strip and Overdrive (Characters), sharing one slot.
//
// Tap pulls the Status off every enemy within Radius, converting each into Shield and Overdrive
// charge. Shield is granted as a share of the Soldier's own maximum, so with no Shield gear the
// strip still charges the gauge but grants nothing. The gauge remembers how much of each element
// fed it.
//
// Hold on a full gauge spends it on OverdriveShots empowered shots, which take over the attack
// binding until they're fired or expire. Each shot pierces, splits its damage across elements in
// the proportions the gauge was filled with, and applies the default ailment of whichever element
// fed it most.
public partial class StatusStripAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 8f;
    [Export] public float Radius { get; private set; } = 8f;
    [Export] public float ChargePerStatus { get; private set; } = 0.04f;
    [Export] public float MaxChargePerCast { get; private set; } = 0.2f;
    // Both fractions of the Soldier's maximum Shield.
    [Export] public float ShieldPerStatus { get; private set; } = 0.05f;
    [Export] public float MaxShieldPerCast { get; private set; } = 0.25f;

    [Export] public int OverdriveShots { get; private set; } = 10;
    // Fixed, deliberately ignoring attack speed.
    [Export] public float OverdriveShotInterval { get; private set; } = 0.5f;
    [Export] public float OverdriveExpiry { get; private set; } = 20f;
    [Export] public int OverdrivePierceCount { get; private set; } = 3;
    // Seconds of weapon DPS each shot deals, so a fast gun and a slow heavy one land equal shots.
    [Export] public float OverdriveDpsSecondsPerShot { get; private set; } = 1f;
    // A share of the shot's own Fire damage.
    [Export] public float BurnDamageFraction { get; private set; } = 0.5f;
    [Export] public float BurnDuration { get; private set; } = 3f;
    [Export] public float ChillPercent { get; private set; } = 0.3f;
    [Export] public float ChillDuration { get; private set; } = 3f;

    // Charge is built from floats, so a gauge that should be full can land a hair under.
    private const float FullChargeTolerance = 0.001f;

    public override string DisplayName => "Status Strip";
    public override string Description => "Tap: strip elemental Status off nearby enemies for Shield and Overdrive charge. Hold on a full gauge: Overdrive.";

    public override bool HasHoldVariant => true;
    public override bool CapturesAttackInput => IsActive;
    public override bool CanCancelCapture => false;
    public override float ActiveFraction => OverdriveShots <= 0 ? 0f : (float)_shotsRemaining / OverdriveShots;

    public override float? GaugeFraction => IsActive ? ActiveFraction : _charge;
    public override Tag? GaugeElement => DominantChargeElement();

    private float _charge;
    private readonly Dictionary<Tag, float> _chargeByElement = new Dictionary<Tag, float>();

    private int _shotsRemaining;
    private float _expiryRemaining;
    private float _shotCooldown;
    private readonly Dictionary<Tag, float> _overdriveShares = new Dictionary<Tag, float>();
    private Tag? _overdriveAilmentElement;

    private Soldier Wielder => (Soldier)Character;

    protected override void Activate()
    {
        Dictionary<Tag, int> stripped = StripNearbyStatuses(out int strippedCount);

        if (strippedCount > 0)
        {
            Character.GrantShield(Character.MaxShield * Mathf.Min(strippedCount * ShieldPerStatus, MaxShieldPerCast));
            AddCharge(stripped, strippedCount);
        }

        StartCooldown();
    }

    public override bool TryActivateHold()
    {
        if (IsActive || _charge < 1f - FullChargeTolerance)
        {
            return false;
        }

        _overdriveAilmentElement = DominantChargeElement();
        _overdriveShares.Clear();

        foreach (KeyValuePair<Tag, float> entry in _chargeByElement)
        {
            _overdriveShares[entry.Key] = entry.Value / _charge;
        }

        ResetGauge();

        IsActive = true;
        _shotsRemaining = OverdriveShots;
        _expiryRemaining = OverdriveExpiry;
        _shotCooldown = 0f;
        Wielder.SetWeaponTint(DamageTypeColors.Get(_overdriveAilmentElement));
        return true;
    }

    public override void OnCharacterDied()
    {
        ResetGauge();

        if (IsActive)
        {
            EndOverdrive();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Health Link's spikes, or any other capture layered on top, take the click first.
        if (!IsActive || !Character.CanUseAbilityInput || Character.IsAnotherAbilityCapturing(this)
            || !@event.IsActionPressed("attack"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();

        if (_shotCooldown <= 0f)
        {
            FireOverdriveShot();
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

    private Dictionary<Tag, int> StripNearbyStatuses(out int strippedCount)
    {
        Dictionary<Tag, int> stripped = new Dictionary<Tag, int>();
        strippedCount = 0;

        Vector3 origin = Character.GlobalPosition;
        float radiusSquared = Radius * Radius;

        foreach (Enemy enemy in LevelContext.Current.Enemies.Active)
        {
            if (enemy.GlobalPosition.DistanceSquaredTo(origin) > radiusSquared)
            {
                continue;
            }

            Tag? status = enemy.RemoveStatus();
            if (status.HasValue)
            {
                stripped[status.Value] = stripped.GetValueOrDefault(status.Value) + 1;
                strippedCount++;
            }
        }

        return stripped;
    }

    // A cast capped by the per-cast limit or the gauge's ceiling keeps its element proportions -
    // each element's share is scaled down together rather than whichever came first filling it.
    private void AddCharge(Dictionary<Tag, int> stripped, int strippedCount)
    {
        float castCharge = Mathf.Min(Mathf.Min(strippedCount * ChargePerStatus, MaxChargePerCast), 1f - _charge);
        if (castCharge <= 0f)
        {
            return;
        }

        foreach (KeyValuePair<Tag, int> entry in stripped)
        {
            float share = castCharge * entry.Value / strippedCount;
            _chargeByElement[entry.Key] = _chargeByElement.GetValueOrDefault(entry.Key) + share;
        }

        _charge += castCharge;
    }

    private void ResetGauge()
    {
        _charge = 0f;
        _chargeByElement.Clear();
    }

    private Tag? DominantChargeElement()
    {
        Tag? dominant = null;
        float best = 0f;

        foreach (KeyValuePair<Tag, float> entry in _chargeByElement)
        {
            if (entry.Value > best)
            {
                best = entry.Value;
                dominant = entry.Key;
            }
        }

        return dominant;
    }

    // Re-casts the ray with each enemy hit excluded, so every enemy along the line gets its own
    // headshot, crit and weak-point check at full damage.
    private void FireOverdriveShot()
    {
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 forward = -Character.Camera.GlobalTransform.Basis.Z;
        Vector3 end = origin + forward * Wielder.AttackRange;
        float baseDamage = Character.AttackDamage * Character.AttackSpeed * OverdriveDpsSecondsPerShot;

        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Enemies;

        Godot.Collections.Array<Rid> pierced = new Godot.Collections.Array<Rid>();
        Vector3 tracerEnd = end;

        for (int i = 0; i < OverdrivePierceCount; i++)
        {
            Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
            if (result.Count == 0 || result["collider"].As<Node3D>() is not Enemy enemy)
            {
                break;
            }

            Vector3 hitPosition = (Vector3)result["position"];
            Wielder.ShowImpact(hitPosition);
            HitWithOverdrive(enemy, origin, end, baseDamage);

            // The last enemy it can pierce stops the tracer; fewer than that and it runs full range.
            if (i == OverdrivePierceCount - 1)
            {
                tracerEnd = hitPosition;
            }

            pierced.Add(enemy.GetRid());
            query.Exclude = pierced;
        }

        Wielder.ShowTracer(tracerEnd);

        _shotsRemaining--;
        _shotCooldown = OverdriveShotInterval;

        if (_shotsRemaining <= 0)
        {
            EndOverdrive();
        }
    }

    private void HitWithOverdrive(Enemy enemy, Vector3 rayOrigin, Vector3 rayEnd, float baseDamage)
    {
        DamagePacket damage = SplitAcrossOverdriveElements(Wielder.RollShotDamage(enemy, rayOrigin, rayEnd, baseDamage));

        enemy.TakeDamage(new Hit { Damage = damage, Attribution = Character.HitAttribution, Type = HitType.Unmarked });

        if (!enemy.IsDead)
        {
            ApplyOverdriveAilment(enemy, damage);
        }
    }

    private DamagePacket SplitAcrossOverdriveElements(DamagePacket rolled)
    {
        DamagePacket split = new DamagePacket { Source = rolled.Source, IsCrit = rolled.IsCrit };
        float total = rolled.Total;

        foreach (KeyValuePair<Tag, float> share in _overdriveShares)
        {
            split.AddElement(share.Key, total * share.Value);
        }

        return split;
    }

    // Lightning's Shock and Water's Drenched aren't built, so shots led by those apply nothing yet.
    private void ApplyOverdriveAilment(Enemy enemy, DamagePacket damage)
    {
        switch (_overdriveAilmentElement)
        {
            case Tag.Fire:
                enemy.ApplyAilment(new BurnAilment(damage.FireDamage * BurnDamageFraction, BurnDuration, Character.HitAttribution));
                break;
            case Tag.Cold:
                enemy.ApplyAilment(new ChillAilment(ChillPercent, ChillDuration));
                break;
        }
    }

    private void EndOverdrive()
    {
        Wielder.SetWeaponTint(null);
        IsActive = false;
        _shotsRemaining = 0;
        _overdriveShares.Clear();
    }
}
