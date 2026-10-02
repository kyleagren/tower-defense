using Godot;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
[SaveTag("favorite_loadout")]
public partial class FavoriteLoadout : RuneLoadout
{
    // The first socketed rune - top-left, then clockwise - then the spell: "Shroud Fireball". With
    // nothing socketed it's just the spell's name.
    public static string DefaultName(Spell spell, IEnumerable<Rune> sockets)
    {
        Rune first = sockets.FirstOrDefault(rune => rune != null);
        return first == null ? spell.Name : $"{first.Name} {spell.Name}";
    }
}
