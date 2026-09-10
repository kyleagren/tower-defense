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
        ["Grunt"] = "uid://ql2bmt2vm1o6",
        ["Sapper"] = "uid://bxt8deqq77uuq",
        // Marksman.tscn has never been resaved by the editor, so Godot hasn't assigned it a uid
        // yet - stays a res:// path (and vulnerable to a future folder move) until that happens.
        ["Marksman"] = "res://enemies/Marksman.tscn"
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
