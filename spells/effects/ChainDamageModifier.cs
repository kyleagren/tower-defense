using Godot;

public abstract partial class ChainDamageModifier : Resource
{
    public abstract float GetMultiplier(int remainingChains);
}
