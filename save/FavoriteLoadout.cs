using Godot;
using System.Collections.Generic;

[GlobalClass]
[SaveTag("favorite_loadout")]
public partial class FavoriteLoadout : RuneLoadout
{
    // The rune with the highest cost multiplier, then the spell - "Shroud Fireball". Ties go to the
    // earliest socket; with nothing socketed it's just the spell's name.
    public static string DefaultName(Spell spell, IEnumerable<Rune> runes)
    {
        Rune priciest = null;

        foreach (Rune rune in runes)
        {
            if (rune != null && (priciest == null || rune.ManaCostMultiplier > priciest.ManaCostMultiplier))
            {
                priciest = rune;
            }
        }

        return priciest == null ? spell.Name : $"{priciest.Name} {spell.Name}";
    }
}
