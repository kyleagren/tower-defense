using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class BlackIceSpell : Spell
{
    [Export] public PackedScene FieldScene { get; private set; }
    [Export] public float BaseRadius { get; private set; } = 3.5f;
    // Matches cast time plus cooldown, so each patch lands as the last one ends.
    [Export] public float BaseDuration { get; private set; } = 8f;
    // Per enemy on the patch, each tick.
    [Export] public float BaseSlipChance { get; private set; } = 0.25f;

    [Export] public override float BaseRange { get; protected set; } = 12f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 7f;
    [Export] public override int BaseManaCost { get; protected set; } = 15;

    public override string Name => "Black Ice";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Cold, Tag.AoE, Tag.Duration };
    public override string Description => "Freezes a patch of ground slick. Enemies crossing it may slip and fall.";
    public override bool DealsHits => false;

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new FieldCastEffect();
        context.Area = new AreaCastData();
        context.GroundEffect = new GroundEffectCastData
        {
            Scene = FieldScene,
            Radius = BaseRadius,
            Duration = BaseDuration,
            Magnitude = BaseSlipChance
        };
    }
}
