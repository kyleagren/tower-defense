using Godot;

// README's Vent (Characters). Drains heat at a steady rate for as long as it runs - past equilibrium
// and on down to the armor side's floor unless cancelled - while every enemy within Radius takes a
// fixed Burn each application. Burns merge, so a steady stream of small ones settles at a constant
// strength however much heat is left. Can only be started with heat above 0.
public partial class VentAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 4f;
    [Export] public float HeatDrainPerSecond { get; private set; } = 10f;
    [Export] public float Radius { get; private set; } = 5f;
    // Scaled by Duration, so a Duration bonus spaces applications out and weakens the Burn.
    [Export] public float BurnInterval { get; private set; } = 0.5f;
    // Each application's total damage, as a fraction of the sword's attack damage.
    [Export] public float BurnDamagePerApplication { get; private set; } = 0.125f;
    [Export] public float BurnDuration { get; private set; } = 1.5f;
    [Export] public PackedScene AuraScene { get; private set; }

    public override string DisplayName => "Vent";
    public override string Description => "Bleed off heat to regain armor, burning every enemy nearby while it drains.";

    public override bool CanToggleOff => true;

    // How much of the way down to the armor floor is still left to go.
    public override float ActiveFraction =>
        Mathf.Clamp((Wielder.Heat + Tank.HeatLimit) / (_startHeat + Tank.HeatLimit), 0f, 1f);

    private float _startHeat;
    private float _radius;
    private float _burnInterval;
    private float _burnDamage;
    private float _burnTimer;
    private VentAura _aura;

    private Tank Wielder => (Tank)Character;

    public override bool TryActivate()
    {
        if (IsActive)
        {
            End();
            return true;
        }

        return base.TryActivate();
    }

    protected override bool CanActivate()
    {
        return Wielder.Heat > 0f;
    }

    protected override void Activate()
    {
        IsActive = true;
        _startHeat = Wielder.Heat;
        _radius = ScaledArea(Radius);
        _burnInterval = ScaledDuration(BurnInterval);
        _burnDamage = ScaledDamage(Character.AttackDamage * BurnDamagePerApplication);

        Rpc(MethodName.BeginAura, _radius);

        ApplyBurns();
        _burnTimer = _burnInterval;
    }

    public override void OnCharacterDied()
    {
        if (IsActive)
        {
            End();
        }
    }

    public override void OnBuildStarted()
    {
        if (IsActive)
        {
            End();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!IsActive)
        {
            return;
        }

        Wielder.AddHeat(-HeatDrainPerSecond * (float)delta);

        if (Wielder.Heat <= -Tank.HeatLimit)
        {
            End();
            return;
        }

        _burnTimer -= (float)delta;

        if (_burnTimer <= 0f)
        {
            _burnTimer += _burnInterval;
            ApplyBurns();
        }
    }

    // Measured flat, like the rest of the kit.
    private void ApplyBurns()
    {
        foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(Character.GlobalPosition, _radius))
        {
            enemy.ApplyDebuff(new BurnAilment(_burnDamage, BurnDuration, Character.HitAttribution));
        }
    }

    private void End()
    {
        IsActive = false;
        Rpc(MethodName.EndAura);
        StartCooldown();
    }

    // On every machine, the Tank's own included. A child of the Tank, so it follows him and goes with
    // him if he's torn down mid-vent.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BeginAura(float radius)
    {
        _aura = AuraScene.Instantiate<VentAura>();
        Character.AddChild(_aura);
        _aura.Begin(radius);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EndAura()
    {
        if (_aura != null && IsInstanceValid(_aura))
        {
            _aura.QueueFree();
        }

        _aura = null;
    }
}
