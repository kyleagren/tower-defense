using Godot;

[GlobalClass]
public partial class WaterStatusTargetingRune : StatusTargetingRune
{
    public override string Name => "Tidesight";

    protected override Tag? Element => Tag.Water;
}
