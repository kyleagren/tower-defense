using System.Collections.Generic;

// Root of a level's wave JSON file - see assets/waves/level_01.json for the format.
public class WaveFile
{
    public List<Wave> Waves { get; set; } = new();
}
