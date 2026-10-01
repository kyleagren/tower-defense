// Applied by FreezeRune: frozen solid, unable to move or attack.
public class FrozenAilment : HardCCAilment
{
    private static readonly Tag[] _elements = { Tag.Cold, Tag.Water };

    public FrozenAilment(float activeDuration, float recoveryDuration) : base(activeDuration, recoveryDuration)
    {
    }

    public override Tag[] Elements => _elements;

    public override DebuffType Type => DebuffType.Frozen;
}
