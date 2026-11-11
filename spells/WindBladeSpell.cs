using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class WindBladeSpell : Spell
{
    [Export] public PackedScene ProjectileScene { get; private set; }
    // The blade's thickness; BaseLength is how wide it spans across its flight.
    [Export] public float BaseRadius { get; private set; } = 0.2f;
    [Export] public float BaseLength { get; private set; } = 2.4f;
    [Export] public float BaseSpeed { get; private set; } = 22f;
    [Export] public int BasePierceCount { get; private set; } = 3;
    // Of its Wind, moved into the first Status it picks up - 60/40 Wind to that element.
    [Export] public float BaseInfusionShare { get; private set; } = 0.4f;

    // Per enemy it cuts through.
    [Export] public override float BaseWindDamage { get; protected set; } = 30f;
    [Export] public override float BaseRange { get; protected set; } = 10f;
    [Export] public override float BaseCastTime { get; protected set; } = 1.2f;
    [Export] public override float BaseCooldown { get; protected set; } = 1f;
    [Export] public override float BaseRagdollForce { get; protected set; } = 10f;
    [Export] public override int BaseManaCost { get; protected set; } = 12;

    public override string Name => "Wind Blade";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Projectile, Tag.Wind, Tag.AoE };
    public override string Glyph => "ᚨ";
    public override string Description => "Fires a wide blade of wind that cuts through several enemies, taking on the element of the first Status it finds.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new ProjectileCastEffect();
        context.Area = new AreaCastData();
        context.Projectile = new ProjectileCastData
        {
            Radius = BaseRadius,
            Length = BaseLength,
            Crosswise = true,
            Speed = BaseSpeed,
            PierceCount = BasePierceCount,
            ProjectileScene = ProjectileScene
        };
        context.Infusion = new InfusionCastData { Share = BaseInfusionShare };
    }
}
