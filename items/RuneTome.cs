using Godot;

// A physical drop that teaches a Rune permanently once learned from Inventory (right-click,
// InventoryPanel) - see RuneCatalog/LootGenerator for how one gets rolled. No affixes, no equip
// slot in spirit, same reasoning as Currency; EquippedLoadout.TryEquip's type switch never matches
// it, so it always falls through to plain inventory placement like any other unequippable Item.
[SaveTag("rune_tome")]
public partial class RuneTome : Item
{
    [Export] public Rune TaughtRune { get; private set; }

    public override EquipmentSlot Slot => EquipmentSlot.RuneTome;
    public override string Name => TaughtRune != null ? $"Rune Tome: {TaughtRune.Name}" : TypeLabel;

    public void SetTaughtRune(Rune rune)
    {
        TaughtRune = rune;
    }

    // Affixes is always empty here, so the base tooltip alone would never say what the rune
    // actually does - append its own description so the player can judge it before learning it.
    public override string BuildTooltip() => TaughtRune != null ? $"{base.BuildTooltip()}\n{TaughtRune.Description}" : base.BuildTooltip();
}
