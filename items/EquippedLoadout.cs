using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EquippedLoadout : Resource
{
    [Export] public Weapon Weapon { get; set; }
    [Export] public Helmet Helmet { get; set; }
    [Export] public Chest Chest { get; set; }
    [Export] public Legplates Legplates { get; set; }
    [Export] public Boots Boots { get; set; }
    [Export] public Gloves Gloves { get; set; }
    [Export] public Belt Belt { get; set; }
    [Export] public Amulet Amulet { get; set; }
    [Export] public Ring LeftRing { get; set; }
    [Export] public Ring RightRing { get; set; }
    [Export] public Relic Relic { get; set; }

    // Places item in its matching slot only if that slot is currently empty - used for auto-equip
    // on world pickup, where silently overwriting something already equipped would be a bad surprise.
    public bool TryEquip(Item item)
    {
        switch (item)
        {
            case Weapon weapon when Weapon == null: Weapon = weapon; return true;
            case Helmet helmet when Helmet == null: Helmet = helmet; return true;
            case Chest chest when Chest == null: Chest = chest; return true;
            case Legplates legplates when Legplates == null: Legplates = legplates; return true;
            case Boots boots when Boots == null: Boots = boots; return true;
            case Gloves gloves when Gloves == null: Gloves = gloves; return true;
            case Belt belt when Belt == null: Belt = belt; return true;
            case Amulet amulet when Amulet == null: Amulet = amulet; return true;
            case Ring ring when LeftRing == null: LeftRing = ring; return true;
            case Ring ring when RightRing == null: RightRing = ring; return true;
            case Relic relic when Relic == null: Relic = relic; return true;
            default: return false;
        }
    }

    // Whether TryEquip would place item, without placing it.
    public bool HasOpenSlotFor(Item item) => item switch
    {
        Weapon _ => Weapon == null,
        Helmet _ => Helmet == null,
        Chest _ => Chest == null,
        Legplates _ => Legplates == null,
        Boots _ => Boots == null,
        Gloves _ => Gloves == null,
        Belt _ => Belt == null,
        Amulet _ => Amulet == null,
        Ring _ => LeftRing == null || RightRing == null,
        Relic _ => Relic == null,
        _ => false
    };

    // Force-equips item into its matching slot regardless of whether it was already occupied,
    // returning whatever got displaced (null if the slot was empty). Rings prefer whichever slot
    // is open, bumping LeftRing specifically if both are already occupied - arbitrary but consistent.
    public Item ForceEquip(Item item)
    {
        Item displaced;

        switch (item)
        {
            case Weapon weapon: displaced = Weapon; Weapon = weapon; return displaced;
            case Helmet helmet: displaced = Helmet; Helmet = helmet; return displaced;
            case Chest chest: displaced = Chest; Chest = chest; return displaced;
            case Legplates legplates: displaced = Legplates; Legplates = legplates; return displaced;
            case Boots boots: displaced = Boots; Boots = boots; return displaced;
            case Gloves gloves: displaced = Gloves; Gloves = gloves; return displaced;
            case Belt belt: displaced = Belt; Belt = belt; return displaced;
            case Amulet amulet: displaced = Amulet; Amulet = amulet; return displaced;
            case Ring ring when LeftRing == null: LeftRing = ring; return null;
            case Ring ring when RightRing == null: RightRing = ring; return null;
            case Ring ring: displaced = LeftRing; LeftRing = ring; return displaced;
            case Relic relic: displaced = Relic; Relic = relic; return displaced;
            default: return null;
        }
    }

    // All items currently equipped in the given slot - usually 0 or 1, except a Ring query
    // returns both LeftRing and RightRing (Item.Slot's own arbitrary "Ring reports LeftRing"
    // convention doesn't matter here - either ring slot means "check both"). Used by
    // ItemTooltip's compare-to-equipped view.
    public IEnumerable<Item> GetEquipped(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: if (Weapon != null) yield return Weapon; break;
            case EquipmentSlot.Helmet: if (Helmet != null) yield return Helmet; break;
            case EquipmentSlot.Chest: if (Chest != null) yield return Chest; break;
            case EquipmentSlot.Legplates: if (Legplates != null) yield return Legplates; break;
            case EquipmentSlot.Boots: if (Boots != null) yield return Boots; break;
            case EquipmentSlot.Gloves: if (Gloves != null) yield return Gloves; break;
            case EquipmentSlot.Belt: if (Belt != null) yield return Belt; break;
            case EquipmentSlot.Amulet: if (Amulet != null) yield return Amulet; break;
            case EquipmentSlot.LeftRing:
            case EquipmentSlot.RightRing:
                if (LeftRing != null) yield return LeftRing;
                if (RightRing != null) yield return RightRing;
                break;
            case EquipmentSlot.Relic: if (Relic != null) yield return Relic; break;
        }
    }

    public IEnumerable<Item> EquippedItems()
    {
        if (Weapon != null) yield return Weapon;
        if (Helmet != null) yield return Helmet;
        if (Chest != null) yield return Chest;
        if (Legplates != null) yield return Legplates;
        if (Boots != null) yield return Boots;
        if (Gloves != null) yield return Gloves;
        if (Belt != null) yield return Belt;
        if (Amulet != null) yield return Amulet;
        if (LeftRing != null) yield return LeftRing;
        if (RightRing != null) yield return RightRing;
        if (Relic != null) yield return Relic;
    }
}
