using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RainSpell : Spell
{
    [Export] public PackedScene FieldScene { get; private set; }
    [Export] public float BaseRadius { get; private set; } = 3.5f;
    // Matches cast time plus cooldown, so each field lands as the last one ends.
    [Export] public float BaseDuration { get; private set; } = 6f;

    [Export] public override float BaseRange { get; protected set; } = 12f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 5f;
    [Export] public override int BaseManaCost { get; protected set; } = 6;

    public override string Name => "Rain";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Water, Tag.AoE, Tag.Duration };
    public override string Description => "Rains on an area, soaking everything in it with Water.";
    public override bool DealsHits => false;

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new FieldCastEffect();
        context.Area = new AreaCastData();
        context.GroundEffect = new GroundEffectCastData
        {
            Scene = FieldScene,
            Radius = BaseRadius,
            Duration = BaseDuration
        };
    }
}
