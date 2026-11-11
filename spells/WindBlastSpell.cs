using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class WindBlastSpell : Spell
{
    [Export] public PackedScene BeamScene { get; private set; }
    [Export] public float BaseBlastHalfWidth { get; private set; } = 2f;
    // kg·m/s, flat along the blast - about 4m on an 80kg Grunt, a little over 1m on a 300kg Brute.
    [Export] public float BasePushImpulse { get; private set; } = 2000f;

    // Per enemy in the blast.
    [Export] public override float BaseWindDamage { get; protected set; } = 15f;
    [Export] public override float BaseRange { get; protected set; } = 3.5f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 3f;
    [Export] public override int BaseManaCost { get; protected set; } = 12;

    public override string Name => "Wind Blast";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Wind, Tag.AoE, Tag.Line };
    public override string Glyph => "ᚹ";
    public override string Description => "Blasts a short, wide gust out from the Ritual, shoving every enemy in it away.";

    // One instant tick, so the interval never comes into it.
    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new BeamCastEffect();
        context.Area = new AreaCastData();
        context.Beam = new BeamCastData
        {
            Scene = BeamScene,
            HalfWidth = BaseBlastHalfWidth,
            TickCount = 1,
            TickInterval = 0f,
            PushImpulse = BasePushImpulse
        };
    }
}
