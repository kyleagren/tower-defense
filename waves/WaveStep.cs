using System.Collections.Generic;

// A step is either a spawn burst (Spawn set, Wait null) or a pause (Wait set, Spawn null).
public class WaveStep
{
    public List<WaveSpawnGroup> Spawn { get; set; }
    public float? Wait { get; set; }
}
