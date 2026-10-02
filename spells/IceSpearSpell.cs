using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class IceSpearSpell : Spell
{
    [Export] public PackedScene ProjectileScene { get; private set; }
    [Export] public float BaseRadius { get; private set; } = 0.12f;
    [Export] public float BaseLength { get; private set; } = 1.2f;
    [Export] public float BaseSpeed { get; private set; } = 30f;
    [Export] public int BasePierceCount { get; private set; } = 2;

    [Export] public override float BaseColdDamage { get; protected set; } = 40f;
    [Export] public override float BaseCastTime { get; protected set; } = 1.2f;
    [Export] public override float BaseRagdollForce { get; protected set; } = 15f;
    [Export] public override int BaseManaCost { get; protected set; } = 10;

    public override string Name => "Ice Spear";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Projectile, Tag.Cold };
    public override string Description => "Hurls a spear of ice that pierces through several enemies.";

    protected override void AddComponents(SpellCastContext context)
    {
        context.CastEffect = new ProjectileCastEffect();
        context.Projectile = new ProjectileCastData
        {
            Radius = BaseRadius,
            Length = BaseLength,
            Speed = BaseSpeed,
            PierceCount = BasePierceCount,
            ProjectileScene = ProjectileScene
        };
    }
}
