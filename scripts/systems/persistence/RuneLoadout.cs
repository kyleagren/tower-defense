using Godot;

[GlobalClass]
public partial class RuneLoadout : Resource
{
    [Export] public Spell Spell { get; set; }
    [Export] public Godot.Collections.Array<Rune> Runes { get; set; } = new();
}
