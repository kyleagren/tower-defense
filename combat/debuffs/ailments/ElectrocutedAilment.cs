// Applied by ElectrocuteRune: convulsing, unable to move or attack.
public class ElectrocutedAilment : HardCCAilment
{
    private static readonly Tag[] _elements = { Tag.Water, Tag.Lightning };

    public ElectrocutedAilment(float activeDuration, float recoveryDuration) : base(activeDuration, recoveryDuration)
    {
    }

    public override Tag[] Elements => _elements;

    public override DebuffType Type => DebuffType.Electrocuted;
}
