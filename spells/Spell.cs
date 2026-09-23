using Godot;
using System.Collections.Generic;

public abstract partial class Spell : Resource
{
    public abstract string Name { get; }
    public abstract IReadOnlyList<Tag> Tags { get; }
    public abstract string Description { get; }

    public virtual float BaseRange { get; protected set; } = 8f;
    public virtual float BaseCastTime { get; protected set; } = 1f;
    public virtual float BaseCooldown { get; protected set; } = 0f;
    public virtual float BasePhysicalDamage { get; protected set; } = 0f;
    public virtual float BaseFireDamage { get; protected set; } = 0f;
    public virtual float BaseWaterDamage { get; protected set; } = 0f;
    public virtual float BaseColdDamage { get; protected set; } = 0f;
    public virtual float BaseLightningDamage { get; protected set; } = 0f;
    public virtual float BaseWindDamage { get; protected set; } = 0f;
    public virtual float BaseRagdollForce { get; protected set; } = 0f;
    public virtual int BaseManaCost { get; protected set; } = 10;
    public virtual int BaseCapacityCost { get; protected set; } = 10;
    public virtual bool RequiresTarget => true;
    public virtual TargetPriority BaseTargetPriority { get; protected set; } = TargetPriority.ClosestToGoal;

    public DamagePacket BaseDamage => new DamagePacket
    {
        PhysicalDamage = BasePhysicalDamage,
        FireDamage = BaseFireDamage,
        WaterDamage = BaseWaterDamage,
        ColdDamage = BaseColdDamage,
        LightningDamage = BaseLightningDamage,
        WindDamage = BaseWindDamage
    };

    public abstract SpellCastContext CreateBaseContext();

    // The plain flow by default: nothing to pick, the Ritual goes wherever it fits. A spell with
    // placement-time choices of its own (Ice Wall's endpoints) returns its own flow instead.
    public virtual PlacementFlow CreatePlacementFlow(RitualPlacer placer, SpellCastContext context) => new PlacementFlow();
}
