using Godot;
using System;
using System.Collections.Generic;

public static class DefaultItemIcons
{
    private static readonly Dictionary<Type, string> _iconPaths = new()
    {
        [typeof(Gun)] = "res://assets/thumbnails/gun.png",
        [typeof(SwordAndShield)] = "res://assets/thumbnails/sword_shield.png",
        [typeof(Helmet)] = "res://assets/thumbnails/helmet.png",
        [typeof(Chest)] = "res://assets/thumbnails/chestplate.png",
        [typeof(Legs)] = "res://assets/thumbnails/pants.png",
        [typeof(Boots)] = "res://assets/thumbnails/boots.png",
        [typeof(Gloves)] = "res://assets/thumbnails/gloves.png",
        [typeof(Belt)] = "res://assets/thumbnails/belt.png",
        [typeof(Amulet)] = "res://assets/thumbnails/amulet.png",
        [typeof(Ring)] = "res://assets/thumbnails/ring.png",
        [typeof(Relic)] = "res://assets/thumbnails/relic.png"
    };

    public static Texture2D Get(Type itemType)
    {
        return _iconPaths.TryGetValue(itemType, out string path) ? GD.Load<Texture2D>(path) : null;
    }
}
