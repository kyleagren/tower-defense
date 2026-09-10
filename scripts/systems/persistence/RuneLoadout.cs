using Godot;

[GlobalClass]
public partial class RuneLoadout : Resource
{
    // Empty for a token built by hand (falls back to Spell.Name in the HUD); set to the source
    // Favorite's own name when a token is stamped from one, so it reads as that memorable name
    // instead of a generic spell name.
    [Export] public string Name { get; set; } = "";
    [Export] public Spell Spell { get; set; }
    [Export] public Godot.Collections.Array<Rune> Runes { get; set; } = new();
}
