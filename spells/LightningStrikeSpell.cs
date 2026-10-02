using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class LightningStrikeSpell : Spell
{
    [Export] public PackedScene ArcVisualScene { get; private set; }
    [Export] public float BaseStrikeRadius { get; private set; } = 4f;
    [Export] public float StrikeHeight { get; private set; } = 15f;

    // To each enemy struck.
    [Export] public override float BaseLightningDamage { get; protected set; } = 60f;
    [Export] public override float BaseRange { get; protected set; } = 10f;
    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 4f;
    [Export] public override int BaseManaCost { get; protected set; } = 15;
    public override TargetPriority BaseTargetPriority { get; protected set; } = TargetPriority.HasStatus;

    public override string Name => "Lightning Strike";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Lightning, Tag.AoE };
    public override string Description => "Calls lightning down on every enemy near its target that already carries a Lightning Status.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.TargetStatus = Tag.Lightning;
        context.CastEffect = new StatusStrikeCastEffect();
        context.CastTrigger = new StatusInRangeCastTrigger { Status = Tag.Lightning };
        context.Area = new AreaCastData();
        context.StatusStrike = new StatusStrikeCastData
        {
            ArcVisualScene = ArcVisualScene,
            Status = Tag.Lightning,
            Radius = BaseStrikeRadius,
            StrikeHeight = StrikeHeight
        };
    }
}
