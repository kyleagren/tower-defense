using System;
using Godot;

// Resolves the string enemy names used in wave JSON files to their scenes - mirrors the small
// hardcoded lookup tables already used for AffixTable/DefaultItemIcons. Throws on an unknown name
// so a typo in a wave file fails loudly at load time instead of silently spawning nothing.
// Listed in the order the spawn wave preview shows them.
public static class EnemyCatalog
{
    private static readonly (string Name, string ScenePath)[] _enemies =
    {
        ("Grunt", "uid://dxw1ycxrs5ipl"),
        ("Marksman", "uid://b14xsyrxfl5md"),
        ("Sapper", "uid://68nn4sdmokgf"),
        ("Brute", "res://enemies/brute.tscn")
    };

    public static PackedScene Get(string name)
    {
        return GD.Load<PackedScene>(_enemies[IndexOf(name)].ScenePath);
    }

    public static int IndexOf(string name)
    {
        int index = Array.FindIndex(_enemies, enemy => enemy.Name == name);

        if (index < 0)
        {
            throw new ArgumentException($"EnemyCatalog: no enemy scene registered for name \"{name}\".");
        }

        return index;
    }
}
