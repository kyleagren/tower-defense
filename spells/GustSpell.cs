using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class GustSpell : Spell
{
    [Export] public PackedScene SweepScene { get; private set; }
    [Export] public float BaseSweepRadius { get; private set; } = 0.6f;
    [Export] public float BaseSweepSpeed { get; private set; } = 6f;
    [Export] public float BaseSweepDuration { get; private set; } = 2f;
    // Of its Wind, moved into the first Status it picks up - 60/40 Wind to that element.
    [Export] public float BaseInfusionShare { get; private set; } = 0.4f;

    // Per enemy it passes through.
    [Export] public override float BaseWindDamage { get; protected set; } = 12f;
    [Export] public override float BaseCastTime { get; protected set; } = 1.2f;
    [Export] public override float BaseCooldown { get; protected set; } = 3f;
    [Export] public override int BaseManaCost { get; protected set; } = 12;

    public override string Name => "Gust";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Wind, Tag.AoE, Tag.Duration, Tag.Traveling };
    public override string Description => "Sends a small twister along the ground through every enemy in its path, swirling any Status it finds.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new SweepCastEffect();
        context.Area = new AreaCastData();
        context.Sweep = new SweepCastData
        {
            Scene = SweepScene,
            Radius = BaseSweepRadius,
            Speed = BaseSweepSpeed,
            Duration = BaseSweepDuration,
            InfusionShare = BaseInfusionShare
        };
    }
}
