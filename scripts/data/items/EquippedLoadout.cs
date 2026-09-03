using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EquippedLoadout : Resource
{
    [Export] public Weapon Weapon { get; set; }
    [Export] public Helmet Helmet { get; set; }
    [Export] public Chest Chest { get; set; }
    [Export] public Legs Legs { get; set; }
    [Export] public Boots Boots { get; set; }
    [Export] public Gloves Gloves { get; set; }
    [Export] public Belt Belt { get; set; }
    [Export] public Amulet Amulet { get; set; }
    [Export] public Ring LeftRing { get; set; }
    [Export] public Ring RightRing { get; set; }
    [Export] public Relic Relic { get; set; }

    public IEnumerable<Item> EquippedItems()
    {
        if (Weapon != null) yield return Weapon;
        if (Helmet != null) yield return Helmet;
        if (Chest != null) yield return Chest;
        if (Legs != null) yield return Legs;
        if (Boots != null) yield return Boots;
        if (Gloves != null) yield return Gloves;
        if (Belt != null) yield return Belt;
        if (Amulet != null) yield return Amulet;
        if (LeftRing != null) yield return LeftRing;
        if (RightRing != null) yield return RightRing;
        if (Relic != null) yield return Relic;
    }
}
