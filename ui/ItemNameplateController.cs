using Godot;
using System.Collections.Generic;
using System.Linq;

// Floating, name-only nameplates for every WorldItem within NameplateRange of the local player -
// projected straight from each item's own 3D position into this CanvasLayer's 2D space every
// frame, smoothed rather than snapped so they don't jitter as the player moves. Whichever item the
// crosshair is currently resting on expands into the same rich TooltipPanelView ItemTooltip uses
// for inventory hover - pinned to a fixed screen corner (not the item's own screen position) and
// scaled up, since a tooltip that drifts with your aim is unreadable while you are trying to hold
// the crosshair steady on the item. Moves to the opposite corner while Inventory is open so the two
// don't overlap.
public partial class ItemNameplateController : CanvasLayer
{
    [Export] public float NameplateRange = 12f;
    [Export] public float WorldHeightOffset = 0.4f;
    [Export] public float LabelGap = 6f;
    [Export] public float PositionSmoothing = 15f;
    [Export] public float ExpandedPanelScale = 2f;
    [Export] public float ExpandedPanelMargin = 24f;

    public static ItemNameplateController Instance { get; private set; }

    private static readonly StyleBoxFlat _backgroundStyle = new StyleBoxFlat
    {
        BgColor = new Color(0f, 0f, 0f, 0.75f),
        ContentMarginLeft = 6,
        ContentMarginRight = 6,
        ContentMarginTop = 2,
        ContentMarginBottom = 2
    };

    private readonly Dictionary<WorldItem, PanelContainer> _labels = new();
    private readonly Dictionary<WorldItem, Rect2> _currentRects = new();

    private TooltipPanelView _expandedPanel;
    private Label _pickupHintLabel;
    private WorldItem _expandedItem;
    private WorldItem _expandedPanelItem;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _expandedPanel = new TooltipPanelView(this, (_, _) => { });
        _expandedPanel.Root.Scale = new Vector2(ExpandedPanelScale, ExpandedPanelScale);

        string pickupHintText = $"Press {InputPromptHelper.GetActionKeyName("interact")} to Pick Up";
        _pickupHintLabel = new Label
        {
            Text = pickupHintText,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Scale = new Vector2(ExpandedPanelScale, ExpandedPanelScale)
        };
        _pickupHintLabel.AddThemeColorOverride("font_color", Colors.White);
        AddChild(_pickupHintLabel);
    }

    public override void _Process(double delta)
    {
        PlayerCharacter player = LevelContext.Current?.LocalActiveCharacter;

        if (player == null || !IsInstanceValid(player))
        {
            ClearAllLabels();
            _expandedPanel.Hide();
            _pickupHintLabel.Visible = false;
            _expandedItem = null;
            _expandedPanelItem = null;
            return;
        }

        Camera3D camera = player.Camera;
        List<WorldItem> visible = new();
        _currentRects.Clear();
        float smoothing = Mathf.Min(1f, PositionSmoothing * (float)delta);
        bool expandedShownThisFrame = false;
        PanelContainer targetedNameplate = null;

        foreach (WorldItem item in LevelContext.Current.WorldItems.Active)
        {
            float distance = item.GlobalPosition.DistanceTo(player.GlobalPosition);
            if (distance > NameplateRange)
            {
                continue;
            }

            Vector3 worldPoint = item.GlobalPosition + Vector3.Up * WorldHeightOffset;
            Vector3 toItem = worldPoint - camera.GlobalPosition;

            if (toItem.Dot(-camera.GlobalTransform.Basis.Z) <= 0f)
            {
                continue;
            }

            visible.Add(item);

            // Always maintained, even while hidden behind the expanded panel - this is the item's
            // real screen position, used for hit-testing (what's under the crosshair), never the
            // expanded panel's own pinned position.
            Vector2 desired = camera.UnprojectPosition(worldPoint);
            PanelContainer nameplate = GetOrCreateNameplate(item);
            bool wasHidden = !nameplate.Visible;

            Vector2 nameplateSize = nameplate.Size;
            Vector2 nameplateTarget = desired - new Vector2(nameplateSize.X / 2f, nameplateSize.Y);
            _currentRects[item] = new Rect2(nameplateTarget, nameplateSize);

            nameplate.Position = wasHidden ? nameplateTarget : nameplate.Position.Lerp(nameplateTarget, smoothing);
            nameplate.Visible = true;

            if (item == _expandedItem)
            {
                targetedNameplate = nameplate;

                bool advanced = Input.IsActionPressed("advanced_tooltip");
                _expandedPanel.Show(item.Item, advanced, backgroundColor: RarityColors.Get(item.Item.Rarity));
                _expandedPanelItem = item;
                expandedShownThisFrame = true;

                Vector2 pinnedTarget = GetPinnedExpandedPosition();
                _expandedPanel.SetPosition(pinnedTarget);

                Vector2 scaledSize = _expandedPanel.Size * ExpandedPanelScale;
                Vector2 hintScaledSize = _pickupHintLabel.Size * ExpandedPanelScale;
                _pickupHintLabel.Position = pinnedTarget + new Vector2((scaledSize.X - hintScaledSize.X) / 2f, -hintScaledSize.Y - LabelGap);
                _pickupHintLabel.Visible = true;
            }
        }

        PruneLabelsNotIn(visible);

        // Bring the targeted item's own nameplate in front of its neighbors first, then the expanded
        // panel/hint in front of that - both were created in _Ready, before any nameplate existed,
        // so without this they'd sit earlier in the CanvasLayer's child order and draw underneath
        // nameplates created later. Re-asserted every frame the panel is shown since new nameplates
        // can appear at any point.
        if (expandedShownThisFrame)
        {
            if (targetedNameplate != null)
            {
                MoveChild(targetedNameplate, -1);
            }

            MoveChild(_expandedPanel.Root, -1);
            MoveChild(_pickupHintLabel, -1);
        }

        bool expandedStillVisible = _expandedItem != null && visible.Contains(_expandedItem) && expandedShownThisFrame;
        if (!expandedStillVisible)
        {
            _expandedPanel.Hide();
            _pickupHintLabel.Visible = false;
            _expandedPanelItem = null;
            _expandedItem = null;
        }

        Vector2 crosshair = GetViewport().GetVisibleRect().Size / 2f;
        _expandedItem = GetItemAt(crosshair);
    }

    // Always bottom-right anchored, but pushed left just far enough to clear whatever's currently
    // occupying that corner (Inventory itself, or any ItemTooltip panel it has open) instead of
    // jumping to the opposite side.
    private Vector2 GetPinnedExpandedPosition()
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        Vector2 scaledSize = _expandedPanel.Size * ExpandedPanelScale;

        Vector2 defaultTarget = new Vector2(
            viewportSize.X - scaledSize.X - ExpandedPanelMargin,
            viewportSize.Y - scaledSize.Y - ExpandedPanelMargin);

        Rect2? blocked = GetBlockedScreenRect();
        if (!blocked.HasValue || !new Rect2(defaultTarget, scaledSize).Intersects(blocked.Value))
        {
            return defaultTarget;
        }

        float pushedX = blocked.Value.Position.X - scaledSize.X - ExpandedPanelMargin;
        return new Vector2(Mathf.Min(defaultTarget.X, pushedX), defaultTarget.Y);
    }

    private static Rect2? GetBlockedScreenRect()
    {
        Rect2? blocked = null;

        if (InventoryPanel.Instance != null && InventoryPanel.Instance.Visible)
        {
            blocked = InventoryPanel.Instance.GetGlobalRect();
        }

        Rect2? tooltipRect = ItemTooltip.Instance?.GetOccupiedScreenRect();
        if (tooltipRect.HasValue)
        {
            blocked = blocked.HasValue ? blocked.Value.Merge(tooltipRect.Value) : tooltipRect.Value;
        }

        return blocked;
    }

    private PanelContainer GetOrCreateNameplate(WorldItem item)
    {
        if (_labels.TryGetValue(item, out PanelContainer existing))
        {
            return existing;
        }

        Label label = new Label
        {
            Text = item.Item.TypeLabel,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", RarityColors.GetText(item.Item.Rarity));
        label.AddThemeConstantOverride("outline_size", 0);

        PanelContainer panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        panel.AddThemeStyleboxOverride("panel", _backgroundStyle);
        panel.AddChild(label);

        AddChild(panel);
        _labels[item] = panel;
        return panel;
    }

    private void PruneLabelsNotIn(IEnumerable<WorldItem> stillVisible)
    {
        HashSet<WorldItem> keep = stillVisible.ToHashSet();
        List<WorldItem> toRemove = _labels.Keys.Where(item => !keep.Contains(item) || !IsInstanceValid(item)).ToList();

        foreach (WorldItem item in toRemove)
        {
            _labels[item].QueueFree();
            _labels.Remove(item);
        }
    }

    private void ClearAllLabels()
    {
        foreach (PanelContainer nameplate in _labels.Values)
        {
            nameplate.QueueFree();
        }

        _labels.Clear();
    }

    // Lets ItemInteractor target whatever nameplate the crosshair is actually over, instead of
    // raycasting for a small 3D collider that's fiddly to aim at precisely.
    public WorldItem GetItemAt(Vector2 screenPoint)
    {
        foreach ((WorldItem item, Rect2 rect) in _currentRects)
        {
            if (!IsInstanceValid(item))
            {
                continue;
            }

            if (rect.HasPoint(screenPoint))
            {
                return item;
            }
        }

        return null;
    }
}
