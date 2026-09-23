using Godot;

public class WallCastData
{
    public PackedScene SegmentScene;
    public float SegmentWidth;
    public float SegmentHealth;
    public float WallDuration;
    public float ManaCostPerSegment;
    public float CapacityCostPerSegment;

    // Filled from WallPlacementData during resolve - zero until a wall's endpoints are picked.
    public Vector3 Start;
    public Vector3 End;
}
