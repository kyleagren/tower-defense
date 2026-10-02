using Godot;

[GlobalClass]
public partial class LightningStatusTargetingRune : StatusTargetingRune
{
    public override string Name => "Stormsight";

    protected override Tag? Element => Tag.Lightning;
}
