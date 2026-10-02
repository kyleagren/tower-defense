using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class LightningRailSpell : Spell
{
    [Export] public PackedScene BeamScene { get; private set; }
    [Export] public float BaseBeamHalfWidth { get; private set; } = 0.35f;

    [Export] public override float BaseLightningDamage { get; protected set; } = 70f;
    [Export] public override float BaseRange { get; protected set; } = 12f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 3f;
    [Export] public override int BaseManaCost { get; protected set; } = 15;

    public override string Name => "Lightning Rail";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Lightning, Tag.AoE, Tag.Line };
    public override string Description => "Fires a bar of lightning that strikes every enemy along its path.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new BeamCastEffect();
        context.Area = new AreaCastData();
        context.Beam = new BeamCastData
        {
            Scene = BeamScene,
            HalfWidth = BaseBeamHalfWidth,
            TickCount = 1
        };
    }
}
