using Godot;

public partial class EconomyManager : Node
{
    [Export] public int StartingMana = 120;
    [Export] public int MaxCapacity = 1000;
    [Export] public bool Unlimited = false;

    public int Mana { get; private set; }
    public int CapacityUsed { get; private set; }
    public int GoldEarned { get; private set; }

    // Mana, capacity, and gold only ever move together (a kill, a spend, a refund, a reset), and
    // every listener refreshes on all of them, so one signal covers it. A Godot signal rather than
    // a C# event so every cross-system notification in the project goes through the same
    // mechanism — and so the multiplayer layer (README: Architecture) has an RPC-able hook.
    [Signal] public delegate void EconomyChangedEventHandler();

    // Registered in _EnterTree rather than _Ready: _EnterTree runs top-down across the whole tree
    // before any node's _Ready, so consumers reading it in their own _Ready are safe regardless of
    // where this node sits in the scene.
    public override void _EnterTree()
    {
        LevelContext.Current.Economy = this;
    }

    public override void _Ready()
    {
        ResetForMatch();
        EventBus.Instance.EnemyKilled += OnEnemyKilled;

        if (!NetworkSession.Instance.IsHost)
        {
            RpcId(1, MethodName.RequestState);
        }
    }

    // Only the host changes the economy; every other machine mirrors it.
    private void Changed()
    {
        EmitSignal(SignalName.EconomyChanged);
        Rpc(MethodName.ReceiveState, Mana, CapacityUsed, GoldEarned);
    }

    // For a machine whose copy of the level loaded after the host's.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestState()
    {
        RpcId(Multiplayer.GetRemoteSenderId(), MethodName.ReceiveState, Mana, CapacityUsed, GoldEarned);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveState(int mana, int capacityUsed, int goldEarned)
    {
        Mana = mana;
        CapacityUsed = capacityUsed;
        GoldEarned = goldEarned;
        EmitSignal(SignalName.EconomyChanged);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.EnemyKilled -= OnEnemyKilled;
    }

    // Restores starting match state. Called on load, before any other machine has this level - they
    // ask for the state once theirs loads. Also the hook a "try again" button can call to restart a
    // failed match without reloading the scene.
    public void ResetForMatch()
    {
        Mana = StartingMana;
        CapacityUsed = 0;
        GoldEarned = 0;
        EmitSignal(SignalName.EconomyChanged);
    }

    private void OnEnemyKilled(int reward, int goldReward, Vector3 position, float dropChance, int lootRolls)
    {
        Mana += reward;
        GoldEarned += goldReward;
        Changed();
    }

    public bool CanAfford(SpellCastContext context)
    {
        if (Unlimited)
        {
            return true;
        }

        return CapacityUsed + context.ResolvedCapacityCost <= MaxCapacity && Mana >= context.ResolvedManaCost;
    }

    public (int ManaCost, int CapacityCost) SpendOnRitual(SpellCastContext context)
    {
        int manaCost = context.ResolvedManaCost;
        int capacityCost = context.ResolvedCapacityCost;

        if (!Unlimited)
        {
            Mana -= manaCost;
            CapacityUsed += capacityCost;
        }

        Changed();
        return (manaCost, capacityCost);
    }

    public void RefundRitual(int manaSpent, int capacityCost)
    {
        if (!Unlimited)
        {
            Mana += manaSpent;
            CapacityUsed -= capacityCost;
        }

        Changed();
    }

    public bool CanAffordMana(int amount) => Unlimited || Mana >= amount;

    public bool TrySpendMana(int amount)
    {
        if (Unlimited)
        {
            return true;
        }

        if (Mana < amount)
        {
            return false;
        }

        Mana -= amount;
        Changed();
        return true;
    }
}
