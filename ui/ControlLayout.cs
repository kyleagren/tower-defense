using Godot;

// Layout helpers shared by the hand-built Control screens (BuildMenu's socket ring,
// InventoryPanel's equipment silhouette), which position children at fractional points in their
// parent rather than through a container.
public static class ControlLayout
{
    // Pins a control to a single fractional point in its parent and gives it a fixed pixel size
    // centered on that point, so a layout reads as "this slot sits 30% across, 54% down".
    public static void CenterOnAnchor(Control control, Vector2 anchorPoint, Vector2 size)
    {
        control.AnchorLeft = anchorPoint.X;
        control.AnchorRight = anchorPoint.X;
        control.AnchorTop = anchorPoint.Y;
        control.AnchorBottom = anchorPoint.Y;

        control.OffsetLeft = -size.X / 2f;
        control.OffsetRight = size.X / 2f;
        control.OffsetTop = -size.Y / 2f;
        control.OffsetBottom = size.Y / 2f;
    }
}
