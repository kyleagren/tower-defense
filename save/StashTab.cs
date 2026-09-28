using Godot;

// One named page of the hub stash - a fixed SlotCount slots like Inventory, so an item stays where the
// player put it.
[GlobalClass]
[SaveTag("stash_tab")]
public partial class StashTab : Resource
{
    public const int SlotCount = 100;

    [Export] public string Name { get; set; } = "";
    [Export] public Godot.Collections.Array<Item> Items { get; set; } = new();

    public static StashTab CreateEmpty(string name)
    {
        StashTab tab = new StashTab { Name = name };
        tab.PadToSlotCount();
        return tab;
    }

    public void PadToSlotCount()
    {
        while (Items.Count < SlotCount)
        {
            Items.Add(null);
        }
    }
}
