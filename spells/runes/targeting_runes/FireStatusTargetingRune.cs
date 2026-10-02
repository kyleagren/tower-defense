using Godot;

[GlobalClass]
public partial class FireStatusTargetingRune : StatusTargetingRune
{
    public override string Name => "Embersight";

    protected override Tag? Element => Tag.Fire;
}
