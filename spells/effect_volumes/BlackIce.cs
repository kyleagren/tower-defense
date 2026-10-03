using Godot;

// Every tick, each enemy on it that isn't already down rolls Magnitude to slip and be knocked down.
// Nothing stops the same enemy slipping again the moment it's up - one may go down repeatedly while
// another crosses clean.
public partial class BlackIce : GroundEffect
{
    [Export] public float KnockdownDuration { get; private set; } = 2f;

    // A slip is a roll on the interval, not on stepping on.
    protected override bool AffectsOnEntry => false;

    protected override void OnTick(Enemy enemy)
    {
        if (enemy.HasDebuff(DebuffType.KnockedDown) || GD.Randf() >= Magnitude)
        {
            return;
        }

        enemy.ApplyDebuff(new KnockedDownAilment(KnockdownDuration));
    }
}
