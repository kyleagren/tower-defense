using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class IceStormSpell : Spell
{
    [Export] public PackedScene BombardmentScene { get; private set; }
    [Export] public PackedScene GroundEffectScene { get; private set; }
    [Export] public float BaseStormRadius { get; private set; } = 3f;
    [Export] public float BaseStormDuration { get; private set; } = 0.75f;
    [Export] public float BaseGroundRadius { get; private set; } = 3f;
    [Export] public float BaseGroundDuration { get; private set; } = 6f;
    [Export] public float BaseGroundSlowPercent { get; private set; } = 0.25f;

    // Per second of storm, not per cast - Bombardment slices it into ticks.
    [Export] public override float BaseColdDamage { get; protected set; } = 120f;
    [Export] public override float BaseCastTime { get; protected set; } = 1.5f;
    [Export] public override float BaseCooldown { get; protected set; } = 8f;
    [Export] public override int BaseManaCost { get; protected set; } = 20;

    public override string Name => "Ice Storm";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Cold, Tag.AoE, Tag.Duration };
    public override string Description => "Calls down a hail of ice, leaving frozen ground that slows anything crossing it.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new BombardmentCastEffect();
        context.Area = new AreaCastData();
        context.Bombardment = new BombardmentCastData
        {
            Scene = BombardmentScene,
            Radius = BaseStormRadius,
            Duration = BaseStormDuration
        };
        context.GroundEffect = new GroundEffectCastData
        {
            Scene = GroundEffectScene,
            Radius = BaseGroundRadius,
            Duration = BaseGroundDuration,
            Magnitude = BaseGroundSlowPercent
        };
    }
}
