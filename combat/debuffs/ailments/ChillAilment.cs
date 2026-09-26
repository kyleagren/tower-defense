// Applied by ChillRune off any Cold damage.
public class ChillAilment : SlowDebuff
{
    private static readonly Tag[] _elements = { Tag.Cold };

    public ChillAilment(float slowPercent, float duration) : base(slowPercent, duration)
    {
    }

    public override Tag[] Elements => _elements;
}
