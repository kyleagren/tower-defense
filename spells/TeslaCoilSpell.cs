using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class TeslaCoilSpell : Spell
{
    [Export] public PackedScene ArcVisualScene { get; private set; }
    [Export] public int BaseTargetCount { get; private set; } = 4;
    [Export] public int BasePulseCount { get; private set; } = 4;
    [Export] public float BasePulseInterval { get; private set; } = 0.3f;

    // Per pulse, to each target.
    [Export] public override float BaseLightningDamage { get; protected set; } = 14f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 8f;
    [Export] public override int BaseManaCost { get; protected set; } = 20;
    public override TargetPriority BaseTargetPriority { get; protected set; } = TargetPriority.Random;

    public override string Name => "Tesla Coil";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Lightning };
    public override string Description => "Arcs lightning out to several nearby enemies in a rapid series of pulses.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new PulseCastEffect();
        context.Pulse = new PulseCastData
        {
            ArcVisualScene = ArcVisualScene,
            TargetCount = BaseTargetCount,
            PulseCount = BasePulseCount,
            PulseInterval = BasePulseInterval
        };
    }
}
