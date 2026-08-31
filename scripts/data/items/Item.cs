using Godot;

public abstract partial class Item : Resource
{
    [Export] public Rarity Rarity { get; private set; }
    [Export] public Modifier[] Modifiers { get; private set; } = System.Array.Empty<Modifier>();

    // No hand-authored flavor names yet - just rarity + slot, matching the "name corresponds
    // to slot + rarity for now" call. The slot itself comes from the concrete type, not a
    // separate tag that would have to be kept in sync with it.
    public string Name => $"{Rarity} {GetType().Name}";
}
