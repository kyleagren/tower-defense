using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class FireBurstSpell : Spell
{
    [Export] public PackedScene ProjectileScene { get; private set; }
    [Export] public int BaseColumns { get; private set; } = 4;
    [Export] public int BaseRows { get; private set; } = 4;
    // Between neighboring pellets, both across and up.
    [Export] public float BaseSpreadAngle { get; private set; } = 3f;
    [Export] public float BaseRadius { get; private set; } = 0.06f;
    [Export] public float BaseSpeed { get; private set; } = 25f;
    // How far pellets fly, as a multiple of range - room for a chain jump past the target.
    [Export] public float BaseExpiryRangeMultiplier { get; private set; } = 3f;

    // Per pellet.
    [Export] public override float BaseFireDamage { get; protected set; } = 8f;
    [Export] public override float BaseRange { get; protected set; } = 2f;
    [Export] public override float BaseCastTime { get; protected set; } = 1.5f;
    [Export] public override float BaseCooldown { get; protected set; } = 2f;
    [Export] public override float BaseRagdollForce { get; protected set; } = 5f;
    [Export] public override int BaseManaCost { get; protected set; } = 10;

    public override string Name => "Fire Burst";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Projectile, Tag.Fire };
    public override string Glyph => "ᛊ";
    public override string Description => "Blasts a tight spray of fire pellets at close range.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new ProjectileCastEffect();
        context.Projectile = new ProjectileCastData
        {
            ProjectileCount = BaseColumns,
            Rows = BaseRows,
            SpreadAngleDegrees = BaseSpreadAngle,
            Radius = BaseRadius,
            Speed = BaseSpeed,
            ExpiryRangeMultiplier = BaseExpiryRangeMultiplier,
            ProjectileScene = ProjectileScene
        };
    }
}
