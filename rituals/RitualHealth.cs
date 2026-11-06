using Godot;

// A Ritual's health, changed only on the host and mirrored on every copy: taking damage, breaking at
// zero, repair and tear-down. A broken Ritual stops casting and being targeted until it's repaired.
public partial class RitualHealth : Node
{
    [Export] public float MaxHealth { get; private set; } = 100f;
    [Export] public HealthBar3D HealthBar { get; private set; }

    // On every machine, whenever the Ritual breaks or is repaired back from broken.
    [Signal] public delegate void BrokenChangedEventHandler(bool broken);

    private Ritual _ritual;
    private LevelContext _level;
    private float _current;

    public bool IsBroken { get; private set; }

    private float MissingFraction => 1f - (_current / MaxHealth);

    public override void _Ready()
    {
        _ritual = GetParent<Ritual>();
        _level = LevelContext.Current;
        _current = MaxHealth;
        HealthBar.SetFraction(1f);
        HealthBar.Visible = false;
    }

    public void TakeDamage(Hit hit)
    {
        if (IsBroken || !IsMultiplayerAuthority())
        {
            return;
        }

        SetHealth(Mathf.Max(0f, _current - hit.Damage.Total));
    }

    // Host only. Breaks it outright, whatever its health - a Sapper's finished channel.
    public void Break()
    {
        if (IsBroken || !IsMultiplayerAuthority())
        {
            return;
        }

        SetHealth(0f);
    }

    // Half the Ritual's own cost, scaled by missing health. Zero at full health. The spell's own
    // cost only - placement extras like an Ice Wall's segments are left out, since the Ritual's own
    // health doesn't scale with them.
    public int RepairCost
    {
        get
        {
            float missing = MissingFraction;

            if (missing <= 0f)
            {
                return 0;
            }

            return Mathf.CeilToInt(_ritual.ResolveWithOwner().ManaCost.Value * GameRules.RepairCostFraction * missing);
        }
    }

    // Free during build time; otherwise RepairCost is paid.
    public void Repair()
    {
        if (!IsMultiplayerAuthority())
        {
            RpcId(1, MethodName.RequestRepair);
            return;
        }

        if (MissingFraction <= 0f)
        {
            return;
        }

        if (!_level.IsBuildTime)
        {
            int cost = RepairCost;

            if (!_level.Economy.TrySpendMana(cost))
            {
                return;
            }

            _ritual.Stats.RecordRepair(cost);
        }

        SetHealth(MaxHealth);
    }

    // Mana back shrinks with damage by the same fraction repair charges, so a Sentinel tearing a
    // broken Ritual down and rebuilding it is never a way around repairing it. Repairs are free
    // during build time, so tearing one down then gets everything back. Capacity always returns in
    // full: it's a ceiling on what can be built, not a currency, so shorting it would lose it for the
    // rest of the match.
    public void Destroy()
    {
        if (!IsMultiplayerAuthority())
        {
            RpcId(1, MethodName.RequestDestroy);
            return;
        }

        float kept = _level.IsBuildTime ? 1f : 1f - GameRules.RepairCostFraction * MissingFraction;
        _level.Economy.RefundRitual(Mathf.FloorToInt(_ritual.ManaCost * kept), _ritual.CapacityCost);
        _ritual.QueueFree();
    }

    // Any player can repair or tear down any Ritual. The host goes by who sent it, not by anything
    // the sender claims.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestRepair()
    {
        if (NetworkSession.Instance.SenderSeat != default)
        {
            Repair();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestDestroy()
    {
        if (NetworkSession.Instance.SenderSeat != default)
        {
            Destroy();
        }
    }

    // Only the host changes a Ritual's health; copies follow.
    private void SetHealth(float health)
    {
        ShowHealth(health);
        Rpc(MethodName.ReceiveHealth, health);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveHealth(float health)
    {
        ShowHealth(health);
    }

    private void ShowHealth(float health)
    {
        _current = health;
        HealthBar.SetFraction(health / MaxHealth);
        HealthBar.Visible = health < MaxHealth;

        bool broken = health <= 0f;

        if (broken != IsBroken)
        {
            IsBroken = broken;
            EmitSignal(SignalName.BrokenChanged, broken);
        }
    }
}
