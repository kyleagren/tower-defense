using Godot;
using System.Collections.Generic;

public abstract partial class Spell : Resource
{
    public abstract IReadOnlyList<Tag> Tags { get; }
    public abstract string Description { get; }

    [Export] public float BaseRange { get; private set; } = 8f;
    [Export] public float BaseCastTime { get; protected set; } = 1f;
    [Export] public float BaseCooldown { get; protected set; } = 0f;
    [Export] public float BaseDamage { get; protected set; } = 0f;
    [Export] public float BaseKnockbackForce { get; protected set; } = 0f;
    [Export] public Color BaseColor { get; protected set; } = Colors.White;

    public abstract SpellCastContext CreateBaseContext();
}
