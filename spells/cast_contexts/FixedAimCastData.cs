using Godot;

public class FixedAimCastData
{
    // Flat. Filled from AimPlacementData during resolve - zero until the direction is picked.
    public Vector3 Direction;
    public float LaneHalfWidth;
}
