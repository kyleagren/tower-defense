using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class IceWallSpell : Spell
{
    [Export] public PackedScene SegmentScene { get; private set; }
    [Export] public float BaseSegmentWidth { get; private set; } = 1.5f;
    [Export] public float BaseSegmentHealth { get; private set; } = 150f;
    [Export] public float BaseWallDuration { get; private set; } = 7f;
    [Export] public float BaseDetectionRange { get; private set; } = 6f;
    [Export] public float BaseManaCostPerSegment { get; private set; } = 10f;
    [Export] public float BaseCapacityCostPerSegment { get; private set; } = 10f;

    [Export] public override float BaseCastTime { get; protected set; } = 1f;
    [Export] public override float BaseCooldown { get; protected set; } = 7f;
    [Export] public override int BaseManaCost { get; protected set; } = 15;
    [Export] public override int BaseCapacityCost { get; protected set; } = 15;

    public override string Name => "Ice Wall";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Cold, Tag.Duration, Tag.Wall };
    public override string Description => "Summons a destructible ice blockade near enemies, forcing them to stop and break through.";
    public override bool RequiresTarget => false;

    public override SpellCastContext CreateBaseContext()
    {
        return new SpellCastContext
        {
            Range = BaseRange,
            CastTime = BaseCastTime,
            Cooldown = BaseCooldown,
            ManaCost = BaseManaCost,
            CapacityCost = BaseCapacityCost,
            CastEffect = new WallCastEffect(),
            Wall = new WallCastData
            {
                SegmentScene = SegmentScene,
                SegmentWidth = BaseSegmentWidth,
                SegmentHealth = BaseSegmentHealth,
                WallDuration = BaseWallDuration,
                ManaCostPerSegment = BaseManaCostPerSegment,
                CapacityCostPerSegment = BaseCapacityCostPerSegment
            },
            CastTrigger = new WallProximityCastTrigger { Range = BaseDetectionRange }
        };
    }

    public override PlacementFlow CreatePlacementFlow(RitualPlacer placer, SpellCastContext context) => new WallPlacementFlow(placer, context);
}
