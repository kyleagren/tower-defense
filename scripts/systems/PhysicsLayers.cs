// Bit masks matching the layer names in project.godot's [layer_names] section. Keep the two in
// sync: renaming or reordering a layer there means updating this file.
public static class PhysicsLayers
{
    public const uint World = 1 << 0;      // layer 1
    public const uint Enemies = 1 << 1;    // layer 2
    public const uint Ragdoll = 1 << 2;    // layer 3
    public const uint Player = 1 << 3;     // layer 4
    public const uint Obstacles = 1 << 4;  // layer 5
    public const uint Rituals = 1 << 5;    // layer 6
}
