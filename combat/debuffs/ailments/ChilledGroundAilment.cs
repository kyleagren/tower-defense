// Applied by ChilledGround to anything standing on it. Its own type rather than a ChillAilment, so
// frozen terrain and a chilling hit stack instead of competing for one slot.
public class ChilledGroundAilment : SlowDebuff
{
    private static readonly Tag[] _elements = { Tag.Cold };

    public ChilledGroundAilment(float slowPercent, float duration) : base(slowPercent, duration)
    {
    }

    public override Tag[] Elements => _elements;

    public override DebuffType Type => DebuffType.ChilledGround;
}
