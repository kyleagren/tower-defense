using Godot;

// Beckon's field: pulls every enemy inside toward its center like a spring and roots it there. The
// root is a short ailment each tick refreshes, so an enemy that leaves - or outlives the field -
// walks free a moment later without anything having to release it. A rooted enemy still attacks
// whatever comes within reach.
public partial class BeckonField : EffectVolume
{
    // How many ticks a root survives unrefreshed, so one late tick doesn't free an enemy.
    private const float RootTicks = 3f;
    // Inside this an enemy has arrived and stops being pulled, rather than nudged every tick.
    private const float ArrivalRadius = 0.5f;

    private float _pullStiffness;

    protected override bool AffectsOnEntry => true;

    public void Configure(float pullStiffness)
    {
        _pullStiffness = pullStiffness;
    }

    protected override void OnTick(Enemy enemy)
    {
        enemy.ApplyDebuff(new BeckonedDebuff(TickInterval * RootTicks));

        Vector3 offset = GlobalPosition - enemy.GlobalPosition;
        offset.Y = 0f;

        if (offset.Length() > ArrivalRadius)
        {
            enemy.ApplyKnockback(offset * _pullStiffness * TickInterval);
        }
    }
}
