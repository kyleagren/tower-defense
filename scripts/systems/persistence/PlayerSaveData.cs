using Godot;

[GlobalClass]
public partial class PlayerSaveData : Resource
{
    [Export] public int Level { get; set; } = 1;
    [Export] public int Xp { get; set; } = 0;
    [Export] public int Gold { get; set; } = 0;
    [Export] public Godot.Collections.Array<Spell> KnownSpells { get; set; } = new();
    [Export] public Godot.Collections.Array<RuneInventoryEntry> KnownRunes { get; set; } = new();
    [Export] public Godot.Collections.Array<Item> Inventory { get; set; } = new();
    [Export] public Godot.Collections.Dictionary<CharacterId, EquippedLoadout> Loadouts { get; set; } = new();
}
