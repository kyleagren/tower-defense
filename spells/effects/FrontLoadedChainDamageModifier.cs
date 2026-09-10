using Godot;

[GlobalClass]
public partial class FrontLoadedChainDamageModifier : ChainDamageModifier
{
    [Export] public float MoreDamagePerRemainingChain = 0.15f;

    public override float GetMultiplier(int remainingChains)
    {
        return Mathf.Pow(1f + MoreDamagePerRemainingChain, remainingChains);
    }
}
