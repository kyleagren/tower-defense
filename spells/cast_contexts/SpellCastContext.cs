using Godot;
using System.Collections.Generic;

public class SpellCastContext
{
    // Every ScaledStat is initialized here: default(ScaledStat) has More = 0, which would resolve
    // to 0. Assigning a float goes through ScaledStat's implicit conversion (Increased 0, More 1).
    public ScaledStat Range = 0f;
    public ScaledStat CastTime = 0f;
    public ScaledStat CastSpeed = 1f;
    public ScaledStat Cooldown = 0f;
    public ScaledStat CooldownRecoveryRate = 1f;
    public ScaledStat RagdollForce = 0f;
    public ScaledStat ManaCost = 0f;
    public ScaledStat CapacityCost = 0f;

    // Cost added by what was picked at placement (an Ice Wall's extra segments), kept apart from
    // ManaCost/CapacityCost so the Ritual's own cost - what repair is priced from - stays just the
    // spell's. Scaled by the same multipliers when the total resolves.
    public float PlacementManaCost;
    public float PlacementCapacityCost;

    // Damage can't itself be a ScaledStat — it's a packet of per-element floats — so the scale
    // sits beside it and Damage exposes the resolved packet.
    public DamagePacket UnscaledDamage;
    public ScaledStat DamageScale = 1f;

    // Equipment-only per-element bonus, on top of DamageScale's uniform Increased/More. Private
    // setter: only ApplyEquipmentBonuses (below) can populate it, so a Rune's Apply() has no path
    // to touch it - enforced by the compiler, not by convention.
    public ElementalIncrease ElementalIncrease { get; private set; }

    public DamagePacket Damage
    {
        get
        {
            float more = DamageScale.More;
            return new DamagePacket
            {
                PhysicalDamage = UnscaledDamage.PhysicalDamage * (1f + DamageScale.Increased + ElementalIncrease.Physical) * more,
                FireDamage = UnscaledDamage.FireDamage * (1f + DamageScale.Increased + ElementalIncrease.Fire) * more,
                WaterDamage = UnscaledDamage.WaterDamage * (1f + DamageScale.Increased + ElementalIncrease.Water) * more,
                ColdDamage = UnscaledDamage.ColdDamage * (1f + DamageScale.Increased + ElementalIncrease.Cold) * more,
                LightningDamage = UnscaledDamage.LightningDamage * (1f + DamageScale.Increased + ElementalIncrease.Lightning) * more,
                WindDamage = UnscaledDamage.WindDamage * (1f + DamageScale.Increased + ElementalIncrease.Wind) * more
            };
        }
    }

    // Cast speed reduces cast time rather than scaling it, so the two resolve together.
    public float ResolvedCastTime => CastTime.Value / CastSpeed.Value;

    // Cooldown recovery rate works the same way cast speed does (and the same way PoE's own
    // "increased cooldown recovery rate" stat works) - it divides into cooldown time rather than
    // subtracting from it, so stacking it can never push the cooldown to zero or negative.
    // A trigger rune's added cooldown sits after the rest of cooldown resolution and before recovery
    // gear, which then speeds the added time up like any other.
    public float ResolvedCooldown
    {
        get
        {
            float cooldown = Cooldown.Value;

            if (Trigger != null)
            {
                cooldown = Mathf.Max(cooldown * Trigger.CooldownMultiplier, cooldown + Trigger.CooldownAdded);
            }

            return cooldown / CooldownRecoveryRate.Value;
        }
    }

    // How far a Resonant Ritual hears others cast: its range, gear and runes included, less the bonus
    // Far Resonance gives only the spell.
    public float ResonanceRadius => Range.Base * (1f + Range.Increased - (Trigger?.RangeNotResonating ?? 0f)) * Range.More;

    // The radius of the circle a spell that lands on a spot covers, Area included - whichever of its
    // components holds it. 0 for a spell that doesn't land on a spot.
    public float SpotRadius =>
        (Bombardment?.Radius ?? Impact?.Radius ?? StatusStrike?.Radius ?? GroundEffect?.Radius ?? 0f) * (Area?.Multiplier.Value ?? 1f);

    // Mana/capacity cost reduction is ordinary linear "Increased" (unlike cooldown), so it needs
    // an explicit floor instead - PoE's own rule that nothing can ever cost less than 1.
    public int ResolvedManaCost => Mathf.Max(1, Mathf.CeilToInt((ManaCost.Base + PlacementManaCost) * ManaCost.Multiplier));
    public int ResolvedCapacityCost => Mathf.Max(1, Mathf.CeilToInt((CapacityCost.Base + PlacementCapacityCost) * CapacityCost.Multiplier));

    public TargetPriority TargetPriority;

    // Which Status a HasStatus priority looks for; null takes any.
    public Tag? TargetStatus;

    // A Resonant Ritual with one picks its own target when pulsed, rather than reaching for the enemy
    // the pulse came from.
    public TargetingRune TargetingRune;

    // The enemy this cast was aimed at, set per cast by whatever picked it.
    public Enemy ChosenTarget;

    // Read by the Ritual itself rather than by the cast pipeline: the one flag here that changes how
    // the Ritual is treated instead of what its spell does.
    public bool Shrouded;

    public ProjectileCastData Projectile;
    public ChainCastData Chain;
    public AreaCastData Area;
    public InstantHitCastData InstantHit;
    public List<OnHitRune> OnHitRunes = new();
    public WallCastData Wall;
    public BombardmentCastData Bombardment;
    public BeamCastData Beam;
    public SweepCastData Sweep;
    public SlashCastData Slash;
    public PulseCastData Pulse;
    public FixedAimCastData FixedAim;
    public StatusStrikeCastData StatusStrike;
    public ImpactCastData Impact;
    public FixedSpotCastData FixedSpot;
    public TriggerCastData Trigger;
    public GroundEffectCastData GroundEffect;
    public CastTrigger CastTrigger;
    public CastEffect CastEffect;

    // The casting spell's own tags, stamped when the spell creates the context so a rune reading them at hit time
    // doesn't need a path back to the Spell resource.
    public IReadOnlyList<Tag> SpellTags = System.Array.Empty<Tag>();

    // Whose cast this is - stamped by the Ritual that resolved it, and carried on every hit it deals.
    public Attribution Attribution;

    // Stamped fresh by the Ritual for each cast.
    public ResonancePulse ResonancePulse;

    // Bundled so a damage site passes one value instead of threading the runes, the tags and the
    // attribution separately through Projectile, ChainResolver and every cast effect.
    public SpellHit SpellHit => new SpellHit
    {
        OnHitRunes = OnHitRunes,
        SpellTags = SpellTags,
        Attribution = Attribution,
        ResonancePulse = ResonancePulse,
        ChosenTarget = ChosenTarget,
        TargetingRune = TargetingRune
    };

    // Folds an owner's equipment bonuses into this context, once, during resolve. Simple stats go
    // into the same ScaledStat.Increased fields Runes use, which equipment and Runes legitimately
    // share; the elemental bonus goes into the private-setter field above
    // instead, and cooldown recovery rate feeds CooldownRecoveryRate rather than Cooldown itself.
    // Area and Duration only reach components the spell (or a rune) already created - equipment
    // never grants a capability, it only scales one.
    public void ApplyEquipmentBonuses(CharacterSheet sheet)
    {
        Range.Increased += sheet.GetBonus(StatId.RitualRange);
        CastSpeed.Increased += sheet.GetBonus(StatId.RitualCastSpeed);
        CooldownRecoveryRate.Increased += sheet.GetBonus(StatId.RitualCooldownRecoveryRate);
        ManaCost.Increased -= sheet.GetBonus(StatId.RitualManaCostReduction);
        CapacityCost.Increased -= sheet.GetBonus(StatId.RitualCapacityCostReduction);
        DamageScale.Increased += sheet.GetBonus(StatId.RitualDamage);

        if (Area != null)
        {
            Area.Multiplier.Increased += sheet.GetBonus(StatId.RitualArea);
        }

        float durationBonus = sheet.GetBonus(StatId.RitualDuration);

        if (Bombardment != null)
        {
            Bombardment.Duration.Increased += durationBonus;
        }

        if (GroundEffect != null)
        {
            GroundEffect.Duration.Increased += durationBonus;
        }

        if (Wall != null)
        {
            Wall.WallDuration.Increased += durationBonus;
        }

        if (Sweep != null)
        {
            Sweep.Duration.Increased += durationBonus;
        }

        ElementalIncrease = new ElementalIncrease
        {
            Fire = sheet.GetBonus(StatId.RitualFireDamage),
            Water = sheet.GetBonus(StatId.RitualWaterDamage),
            Cold = sheet.GetBonus(StatId.RitualColdDamage),
            Lightning = sheet.GetBonus(StatId.RitualLightningDamage),
            Wind = sheet.GetBonus(StatId.RitualWindDamage)
        };
    }
}
