// Single placeholder concrete Currency until real categories (Essence, Fossil, named currencies
// like PoE's Chaos/Exalt) are actually designed. Extending later is adding sibling classes here -
// no changes needed anywhere else.
[SaveTag("generic_currency")]
public partial class GenericCurrency : Currency
{
    public override int SaleValue => 100;
}
