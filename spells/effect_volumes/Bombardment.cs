using Godot;

// A cast in progress: damage delivered into a fixed area across a short window rather than all at
// once. Applies no ailment of its own, so the same class serves a meteor shower or a poison cloud
// unchanged - what differs between those is data and the scene it's instantiated from.
public partial class Bombardment : EffectVolume
{
    private DamagePacket _damagePerTick;
    private SpellHit _spellHit;

    // Damage is a rate, so a longer window deals more in total, and the scene's tick cadence changes
    // only how that rate is sliced, never how much it adds up to per second.
    public void Initialize(Vector3 position, float radius, float duration, DamagePacket damagePerSecond, SpellHit spellHit)
    {
        base.Initialize(position, radius, duration);
        _damagePerTick = damagePerSecond * TickInterval;
        _spellHit = spellHit;
    }

    protected override void OnTick(Enemy enemy)
    {
        enemy.TakeDamage(_damagePerTick, spellHit: _spellHit);
    }
}
