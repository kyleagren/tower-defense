using System.Collections.Generic;

// A panel that names its controls in the hint bar while it's the top panel. Asked every frame, so
// the list can follow whatever the panel is showing right now. Most important first: the bar drops
// whatever doesn't fit from the end.
public interface IHintSource
{
    IEnumerable<Hint> GetHints();
}
