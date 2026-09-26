using Godot;
using System.Collections.Generic;

// README's Harvest (Characters). Pulls the Status off every enemy within Radius, converting each into
// Shield and gauge charge. Shield is granted as a share of the Soldier's own maximum, so with no
// Shield gear it still charges the gauge but grants nothing. The gauge remembers how much of each
// element fed it, since Overdrive - which shares this slot - splits its damage in those proportions.
public partial class HarvestAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 8f;
    [Export] public float Radius { get; private set; } = 8f;
    [Export] public float ChargePerStatus { get; private set; } = 0.04f;
    [Export] public float MaxChargePerCast { get; private set; } = 0.2f;
    // Both fractions of the Soldier's maximum Shield.
    [Export] public float ShieldPerStatus { get; private set; } = 0.05f;
    [Export] public float MaxShieldPerCast { get; private set; } = 0.25f;

    // Charge is built from floats, so a gauge that should be full can land a hair under.
    private const float FullChargeTolerance = 0.001f;

    public override string DisplayName => "Harvest";
    public override string Description => "Strips elemental Status off nearby enemies for Shield and Overdrive charge.";

    public override float? GaugeFraction => _charge;
    public override Tag? GaugeElement => DominantChargeElement();

    public bool IsGaugeFull => _charge >= 1f - FullChargeTolerance;

    private float _charge;
    private readonly Dictionary<Tag, float> _chargeByElement = new Dictionary<Tag, float>();

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

    public override void OnCharacterDied()
    {
        ResetGauge();
    }

    // Empties a full gauge, handing back each element's share of it and whichever fed it most.
    public bool TryConsumeFullGauge(out Dictionary<Tag, float> shares, out Tag? dominantElement)
    {
        shares = null;
        dominantElement = null;

        if (!IsGaugeFull)
        {
            return false;
        }

        dominantElement = DominantChargeElement();
        shares = new Dictionary<Tag, float>();

        foreach (KeyValuePair<Tag, float> entry in _chargeByElement)
        {
            shares[entry.Key] = entry.Value / _charge;
        }

        ResetGauge();
        return true;
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
}
