using Godot;

[GlobalClass]
public partial class WaveDefinition : Resource
{
    [Export] public Godot.Collections.Array<WaveSpawnEntry> Entries { get; private set; } = new();
    [Export] public bool RandomizeSpawnOrder { get; private set; } = false;
}
