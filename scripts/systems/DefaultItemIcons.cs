using Godot;
using System.Collections.Generic;

public static class DefaultItemIcons
{
    private static readonly Dictionary<EquipmentSlot, string> _iconPaths = new()
    {
        [EquipmentSlot.Weapon] = "res://assets/thumbnails/gun.png",
        [EquipmentSlot.Helmet] = "res://assets/thumbnails/helmet.png",
        [EquipmentSlot.Chest] = "res://assets/thumbnails/chestplate.png",
        [EquipmentSlot.Legs] = "res://assets/thumbnails/pants.png",
        [EquipmentSlot.Boots] = "res://assets/thumbnails/boots.png",
        [EquipmentSlot.Gloves] = "res://assets/thumbnails/gloves.png",
        [EquipmentSlot.Belt] = "res://assets/thumbnails/belt.png",
        [EquipmentSlot.Amulet] = "res://assets/thumbnails/amulet.png",
        [EquipmentSlot.LeftRing] = "res://assets/thumbnails/ring.png",
        [EquipmentSlot.RightRing] = "res://assets/thumbnails/ring.png",
        [EquipmentSlot.Relic] = "res://assets/thumbnails/relic.png"
    };

    public static Texture2D Get(EquipmentSlot slot)
    {
        return _iconPaths.TryGetValue(slot, out string path) ? GD.Load<Texture2D>(path) : null;
    }
}
