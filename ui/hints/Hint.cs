// One entry in the hint bar: the button for an action on the device in use, and what pressing it
// does in the current menu.
//
// MouseText stands in for the action's own binding on keyboard and mouse, for an input the action
// doesn't carry - a click, or a right-click a panel reads directly. A "{0}" in it is replaced by the
// action's keyboard binding, so "{0} / Right Click" still follows a rebind.
public readonly record struct Hint(string Action, string Label, bool Hold = false, string MouseText = null);
