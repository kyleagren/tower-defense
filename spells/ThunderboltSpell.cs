using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ThunderboltSpell : Spell
{
    [Export] public override float BaseLightningDamage { get; protected set; } = 200f;
    [Export] public override float BaseRange { get; protected set; } = 10f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 5f;
    [Export] public override int BaseManaCost { get; protected set; } = 15;
    [Export] public float StrikeHeight { get; private set; } = 15f;
    [Export] public PackedScene ArcVisualScene { get; private set; }

    public override string Name => "Thunderbolt";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Lightning };
    public override string Glyph => "ᛉ";
    public override string Description => "Calls a heavy bolt of lightning down onto a single enemy.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new InstantHitCastEffect();
        context.InstantHit = new InstantHitCastData
        {
            ArcVisualScene = ArcVisualScene,
            SkyStrikeHeight = StrikeHeight
        };
    }
}
