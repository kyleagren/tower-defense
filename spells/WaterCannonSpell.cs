using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class WaterCannonSpell : Spell
{
    [Export] public PackedScene BeamScene { get; private set; }
    [Export] public float BaseBeamHalfWidth { get; private set; } = 0.75f;
    [Export] public int BaseTickCount { get; private set; } = 3;
    [Export] public float BaseTickInterval { get; private set; } = 0.3f;
    // kg·m/s per tick - about 0.3m on an 80kg Grunt.
    [Export] public float BasePushImpulse { get; private set; } = 250f;

    // Per tick.
    [Export] public override float BaseWaterDamage { get; protected set; } = 15f;
    [Export] public override float BaseRange { get; protected set; } = 10f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 3f;
    [Export] public override int BaseManaCost { get; protected set; } = 15;

    public override string Name => "Water Cannon";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Water, Tag.AoE };
    public override string Description => "Blasts a beam of water down a line of enemies, hitting each three times and pushing them back.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new BeamCastEffect();
        context.Area = new AreaCastData();
        context.Beam = new BeamCastData
        {
            Scene = BeamScene,
            HalfWidth = BaseBeamHalfWidth,
            TickCount = BaseTickCount,
            TickInterval = BaseTickInterval,
            PushImpulse = BasePushImpulse
        };
    }
}
