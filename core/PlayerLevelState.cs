using System.Collections.Generic;

// One player's state for the current level that outlives any one body - the Tank's heat, Harvest's
// gauge, deaths so far. Bodies read and write it through LevelContext.StateOf, so swapping bodies
// never loses it; it's freed with the level.
public class PlayerLevelState
{
    public float Heat { get; set; }

    // Harvest's gauge: its total charge, and how much of that each element fed.
    public float HarvestCharge { get; set; }
    public Dictionary<Tag, float> HarvestChargeByElement { get; } = new();

    public int Deaths { get; set; }
}
