using Godot;

public abstract partial class ChainableSpell : Spell
{
    [Export] public int BaseChainCount { get; protected set; } = 0;
}
