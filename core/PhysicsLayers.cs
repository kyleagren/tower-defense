// Bit masks matching the layer names in project.godot's [layer_names] section. Keep the two in
// sync: renaming or reordering a layer there means updating this file.
public static class PhysicsLayers
{
    public const uint World = 1 << 0;             // layer 1
    public const uint Enemies = 1 << 1;           // layer 2
    public const uint Ragdoll = 1 << 2;           // layer 3
    public const uint Player = 1 << 3;             // layer 4
    public const uint Obstacles = 1 << 4;          // layer 5
    public const uint Rituals = 1 << 5;            // layer 6
    public const uint PlacementBlocked = 1 << 6;   // layer 7
    public const uint Items = 1 << 7;              // layer 8

    // A Ritual's presence for everything that isn't movement - being shot, repair and destroy
    // raycasts, and blocking placement. Separate from Rituals because a shrouded Ritual leaves that
    // layer to stop blocking movement, and must still be hittable and repairable.
    public const uint RitualHitbox = 1 << 8;       // layer 9
}
