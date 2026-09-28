using System;
using System.Collections.Generic;
using Godot;

// Resolves the string enemy names used in wave JSON files to their scenes - mirrors the small
// hardcoded lookup tables already used for AffixTable/DefaultItemIcons. Throws on an unknown name
// so a typo in a wave file fails loudly at load time instead of silently spawning nothing.
public static class EnemyCatalog
{
    private static readonly Dictionary<string, string> _scenePaths = new()
    {
        ["Grunt"] = "uid://dxw1ycxrs5ipl",
        ["Sapper"] = "uid://68nn4sdmokgf",
        ["Marksman"] = "uid://b14xsyrxfl5md",
        ["Brute"] = "res://enemies/brute.tscn"
    };

    public static PackedScene Get(string name)
    {
        if (!_scenePaths.TryGetValue(name, out string path))
        {
            throw new ArgumentException($"EnemyCatalog: no enemy scene registered for name \"{name}\".");
        }

        return GD.Load<PackedScene>(path);
    }
}
