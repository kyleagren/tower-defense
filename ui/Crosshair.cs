using Godot;

// Hides the crosshair whenever a UI panel (build menu, inventory, character select, ...) is open -
// CanvasLayer draws above every plain Control regardless of tree order, so without this the
// crosshair shows through any menu instead of being covered by it.
public partial class Crosshair : CanvasLayer
{
    public override void _Process(double delta)
    {
        Visible = !UiPanelStack.AnyOpen;
    }
}
