using Godot;
using System.Collections.Generic;

public abstract partial class Spell : Resource
{
    public abstract IReadOnlyList<Tag> Tags { get; }
    
    [Export] public float BaseRange { get; private set; } = 8f;
    [Export] public float BaseCooldown { get; private set; } = 0f;
    public abstract SpellCastContext CreateBaseContext();
}