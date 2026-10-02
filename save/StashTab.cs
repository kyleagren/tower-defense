using Godot;

// One named page of the hub stash, a grid like Inventory's.
[GlobalClass]
[SaveTag("stash_tab")]
public partial class StashTab : Resource
{
    [Export] public string Name { get; set; } = "";

    // Each item under its top-left cell - see ItemGrid.
    [Export] public Godot.Collections.Dictionary<int, Item> Items { get; set; } = new();

    public ItemGrid Grid => new ItemGrid(Items, GameRules.StashTabColumns, GameRules.StashTabRows);

    public static StashTab CreateEmpty(string name) => new StashTab { Name = name };
}
