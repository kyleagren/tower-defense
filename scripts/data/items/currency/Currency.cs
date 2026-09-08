// Abstract base for stackable, non-equippable drops (PoE-style crafting/exchange currency).
// Extends Item to reuse WorldItem/Inventory/pickup/tooltip for free, not because it shares Item's
// affix/slot machinery in spirit - EquippedLoadout.TryEquip switches on concrete gear types
// (Weapon/Helmet/...), so a Currency never matches any case there and always falls through to
// plain inventory placement, no extra guard needed.
public abstract partial class Currency : Item
{
    public override EquipmentSlot Slot => EquipmentSlot.Currency;
    public override string Name => SpacedTypeName();
    public override int SaleValue => 1;
}
