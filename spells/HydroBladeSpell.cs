using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HydroBladeSpell : Spell
{
    [Export] public PackedScene SlashScene { get; private set; }
    [Export] public int BaseHitCount { get; private set; } = 2;
    [Export] public float BaseHitInterval { get; private set; } = 0.3f;

    // Per hit.
    [Export] public override float BaseWaterDamage { get; protected set; } = 50f;
    [Export] public override float BaseRange { get; protected set; } = 9f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 2f;
    [Export] public override int BaseManaCost { get; protected set; } = 10;

    public override string Name => "Hydro Blade";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Water };
    public override string Glyph => "ᚷ";
    public override string Description => "Cuts a single enemy with two quick slashes of water.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new SlashCastEffect();
        context.Slash = new SlashCastData
        {
            VisualScene = SlashScene,
            HitCount = BaseHitCount,
            HitInterval = BaseHitInterval
        };
    }
}
