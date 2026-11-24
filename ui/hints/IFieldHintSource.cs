using System.Collections.Generic;

// A component of the local character that names its controls in the hint bar while no panel is open -
// placing a Ritual, picking a token. A direct child of the character's body. Same contract as
// IHintSource otherwise.
public interface IFieldHintSource
{
    void AddHints(List<Hint> hints);
}
