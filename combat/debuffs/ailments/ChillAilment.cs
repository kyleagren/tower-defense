// Applied by ChillRune off any Cold damage.
public class ChillAilment : SlowDebuff
{
    private static readonly Tag[] _elements = { Tag.Cold };

    public ChillAilment(float slowPercent, float duration) : base(slowPercent, duration)
    {
    }

    public override Tag[] Elements => _elements;

    public override DebuffType Type => DebuffType.Chill;

    public override Godot.Collections.Array ToNetwork() => new() { (int)Type, SlowPercent, RemainingDuration };

    public static ChillAilment Decode(Godot.Collections.Array data) => new(data[1].AsSingle(), data[2].AsSingle());
}
