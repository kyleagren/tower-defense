using Godot;
using System.Collections.Generic;

// README's Shield Slam (Characters). A short frontal cone: every enemy inside takes a hit of
// DamageMultiplier x the sword's attack damage - rolled like a swing, so it can crit and carries
// heat's Fire - and is thrown straight away from the Tank. Mass does the scaling: small enemies go
// flying, big ones barely stagger and stay packed where the rest of the kit wants them. Heat lands
// after the hits, so the slam's own Fire comes from the heat it started with.
public partial class ShieldSlamAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 8f;
    [Export] public float Range { get; private set; } = 4f;
    // Full width of the cone.
    [Export] public float ConeDegrees { get; private set; } = 120f;
    // How far above or below the Tank's feet an enemy can stand and still be hit.
    [Export] public float HeightReach { get; private set; } = 2f;
    [Export] public float DamageMultiplier { get; private set; } = 1f;
    // kg·m/s - an 80kg Grunt slides about 5m, a 300kg brute barely over one.
    [Export] public float KnockbackImpulse { get; private set; } = 2400f;
    // Launches anything the slam kills - half what a Fireball explosion gives its victims.
    [Export] public float KillLaunchImpulse { get; private set; } = 200f;
    [Export] public float HeatGain { get; private set; } = 20f;
    [Export] public PackedScene WaveScene { get; private set; }

    public override string DisplayName => "Shield Slam";
    public override string Description => "Slam the shield forward, battering and hurling back every enemy in front.";

    private Tank Wielder => (Tank)Character;

    protected override void Activate()
    {
        float range = ScaledArea(Range);
        Vector3 origin = Character.GlobalPosition;
        Vector3 forward = -Character.GlobalBasis.Z;
        forward = new Vector3(forward.X, 0f, forward.Z).Normalized();

        // Collected before anything is hit - a kill unregisters from the list being walked.
        List<(Enemy Enemy, Vector3 Away)> targets = FindTargets(origin, forward, range);
        float baseDamage = ScaledDamage(Character.AttackDamage * DamageMultiplier);

        foreach ((Enemy enemy, Vector3 away) in targets)
        {
            enemy.TakeDamage(new Hit
            {
                Damage = Character.RollAttackDamage(baseDamage),
                Attribution = Character.HitAttribution,
                RagdollImpulse = (away + Vector3.Up * 0.3f).Normalized() * KillLaunchImpulse
            });
            enemy.ApplyKnockback(away * KnockbackImpulse);
        }

        Wielder.AddHeat(HeatGain);
        Wielder.PlayShieldThrust();

        Rpc(MethodName.PlayWave, origin, forward, range);

        StartCooldown();
    }

    // On every machine, the Tank's own included.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PlayWave(Vector3 origin, Vector3 forward, float range)
    {
        ShieldSlamWave wave = WaveScene.Instantiate<ShieldSlamWave>();
        LevelContext.Current.LevelRoot.AddChild(wave);
        wave.Play(origin, forward, range, ConeDegrees);
    }

    // Measured flat within a height band, so a slope or a taller enemy doesn't change what counts as
    // in front, but an enemy on another floor - under a bridge the Tank stands on - isn't.
    private List<(Enemy, Vector3)> FindTargets(Vector3 origin, Vector3 forward, float range)
    {
        List<(Enemy, Vector3)> targets = new List<(Enemy, Vector3)>();
        float halfAngle = Mathf.DegToRad(ConeDegrees / 2f);

        foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(origin, range, enemy => Mathf.Abs(enemy.GlobalPosition.Y - origin.Y) <= HeightReach))
        {
            Vector3 offset = enemy.GlobalPosition - origin;
            offset.Y = 0f;

            // An enemy right on top of the Tank has no direction to be in front of, so it counts
            // and goes straight forward.
            if (offset == Vector3.Zero)
            {
                targets.Add((enemy, forward));
                continue;
            }

            if (forward.AngleTo(offset) <= halfAngle)
            {
                targets.Add((enemy, offset.Normalized()));
            }
        }

        return targets;
    }
}
