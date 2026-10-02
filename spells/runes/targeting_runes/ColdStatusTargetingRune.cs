using Godot;

[GlobalClass]
public partial class ColdStatusTargetingRune : StatusTargetingRune
{
    public override string Name => "Frostsight";

    protected override Tag? Element => Tag.Cold;
}
