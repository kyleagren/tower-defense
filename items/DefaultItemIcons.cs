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
        [typeof(Legplates)] = "res://assets/thumbnails/legplates.png",
        [typeof(Boots)] = "res://assets/thumbnails/boots.png",
        [typeof(Gloves)] = "res://assets/thumbnails/gloves.png",
        [typeof(Belt)] = "res://assets/thumbnails/belt.png",
        [typeof(Amulet)] = "res://assets/thumbnails/amulet.png",
        [typeof(Ring)] = "res://assets/thumbnails/ring.png",
        [typeof(Relic)] = "res://assets/thumbnails/relic.png",
        [typeof(GenericCurrency)] = "res://assets/thumbnails/generic_currency.png",
        [typeof(RuneTome)] = "res://assets/thumbnails/rune_tome.png"
    };

    public static Texture2D Get(Item item)
    {
        if (!_iconPaths.TryGetValue(item.GetType(), out string path))
        {
            return null;
        }

        if (item is Armor armor)
        {
            string suffix = ArmorBaseTypeTable.GetIconSuffix(armor.BaseType);
            if (!string.IsNullOrEmpty(suffix))
            {
                path = path.Replace(".png", suffix + ".png");
            }
        }

        return GD.Load<Texture2D>(path);
    }
}
