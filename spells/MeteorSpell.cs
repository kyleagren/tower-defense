using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MeteorSpell : Spell
{
    [Export] public PackedScene ImpactScene { get; private set; }
    [Export] public float BaseRadius { get; private set; } = 2.5f;
    // From cast to landing.
    [Export] public float BaseDelay { get; private set; } = 1.5f;

    [Export] public override float BaseFireDamage { get; protected set; } = 250f;
    [Export] public override float BaseRange { get; protected set; } = 14f;
    [Export] public override float BaseCastTime { get; protected set; } = 1.5f;
    [Export] public override float BaseCooldown { get; protected set; } = 6f;
    [Export] public override float BaseRagdollForce { get; protected set; } = 400f;
    [Export] public override int BaseManaCost { get; protected set; } = 25;

    public override string Name => "Meteor";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Fire, Tag.AoE };
    public override string Description => "Calls a meteor down on a spot, crushing whatever is still standing there when it lands.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new ImpactCastEffect();
        context.Area = new AreaCastData();
        context.Impact = new ImpactCastData
        {
            Scene = ImpactScene,
            Radius = BaseRadius,
            Delay = BaseDelay
        };
    }
}
