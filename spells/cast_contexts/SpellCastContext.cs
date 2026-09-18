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
    public float ResolvedCooldown => Cooldown.Value / CooldownRecoveryRate.Value;

    // Mana/capacity cost reduction is ordinary linear "Increased" (unlike cooldown), so it needs
    // an explicit floor instead - PoE's own rule that nothing can ever cost less than 1.
    public int ResolvedManaCost => Mathf.Max(1, Mathf.CeilToInt(ManaCost.Value));
    public int ResolvedCapacityCost => Mathf.Max(1, Mathf.CeilToInt(CapacityCost.Value));

    public TargetPriority TargetPriority;

    public ProjectileCastData Projectile;
    public ChainCastData Chain;
    public AreaCastData Area;
    public InstantHitCastData InstantHit;
    public List<OnHitRune> OnHitRunes = new();
    public WallCastData Wall;
    public BombardmentCastData Bombardment;
    public GroundEffectCastData GroundEffect;
    public CastTrigger CastTrigger;

    // The casting spell's own tags, stamped during resolve so a rune reading them at hit time
    // doesn't need a path back to the Spell resource.
    public IReadOnlyList<Tag> SpellTags = System.Array.Empty<Tag>();

    // Bundled so a damage site passes one value instead of threading the runes and the tags
    // separately through Projectile, ChainResolver and every cast effect.
    public SpellHit SpellHit => new SpellHit { OnHitRunes = OnHitRunes, SpellTags = SpellTags };

    // Folds an owner's equipment bonuses into this context, once, during resolve. Simple stats go
    // into the same ScaledStat.Increased fields Runes use, which equipment and Runes legitimately
    // share; the elemental bonus goes into the private-setter field above
    // instead, and cooldown recovery rate feeds CooldownRecoveryRate rather than Cooldown itself.
    public void ApplyEquipmentBonuses(CharacterSheet sheet)
    {
        Range.Increased += sheet.GetBonus(StatId.RitualRange);
        CastSpeed.Increased += sheet.GetBonus(StatId.RitualCastSpeed);
        CooldownRecoveryRate.Increased += sheet.GetBonus(StatId.RitualCooldownRecoveryRate);
        ManaCost.Increased -= sheet.GetBonus(StatId.RitualManaCostReduction);
        CapacityCost.Increased -= sheet.GetBonus(StatId.RitualCapacityCostReduction);
        DamageScale.Increased += sheet.GetBonus(StatId.RitualDamage);

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
