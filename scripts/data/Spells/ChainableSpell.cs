using Godot;

public abstract partial class ChainableSpell : Spell
{
    [Export] public int BaseChainCount { get; protected set; } = 0;
    [Export] public float BaseChainRange { get; protected set; } = 6f;
}
