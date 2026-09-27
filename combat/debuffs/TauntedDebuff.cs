using Godot;

// Retaliation's taunt: the host attacks the taunting Sentinel above everything else it could want -
// lane, leash, walls, Rituals, the objective. Lapses on its own at the end of the window, and a
// taunter that dies or leaves play stops being a target on the spot.
public class TauntedDebuff : Debuff
{
    public Sentinel Taunter { get; }

    private float _remainingDuration;

    public TauntedDebuff(Sentinel taunter, float duration)
    {
        Taunter = taunter;
        _remainingDuration = duration;
    }

    public override DebuffType Type => DebuffType.Taunted;

    public override Godot.Collections.Array ToNetwork() => new() { (int)Type, _remainingDuration };

    // The taunter is the sender's own Sentinel.
    public static TauntedDebuff Decode(Godot.Collections.Array data, Attribution attribution) =>
        attribution.Source is Sentinel taunter ? new TauntedDebuff(taunter, data[1].AsSingle()) : null;

    // Null once the taunter is gone or down, so a taunt on a dead Tank stops pulling at once.
    public Sentinel ActiveTaunter =>
        GodotObject.IsInstanceValid(Taunter) && Taunter.IsActive && !Taunter.IsDead ? Taunter : null;

    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }
}
