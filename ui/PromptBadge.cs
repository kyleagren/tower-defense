using Godot;

// A round badge naming the button for one action on the device the player is using right now -
// drawn for the PlayStation face buttons and the d-pad, text for everything else.
public partial class PromptBadge : Panel
{
    private static readonly Color _backgroundColor = new Color(0f, 0f, 0f, 0.85f);

    private Label _label;
    private ButtonGlyph _glyph;

    public static PromptBadge Create(Control parent, Vector2 position, float size)
    {
        PromptBadge badge = new PromptBadge
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Position = position,
            Size = new Vector2(size, size)
        };
        badge.AddThemeStyleboxOverride("panel", HudCircle.BuildCircularStyle(_backgroundColor));

        badge._label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        badge._label.AddThemeFontSizeOverride("font_size", 13);
        badge._label.AddThemeColorOverride("font_color", Colors.White);
        badge._label.SetAnchorsPreset(LayoutPreset.FullRect);
        badge.AddChild(badge._label);

        badge._glyph = new ButtonGlyph { MouseFilter = MouseFilterEnum.Ignore };
        badge._glyph.SetAnchorsPreset(LayoutPreset.FullRect);
        badge.AddChild(badge._glyph);

        parent.AddChild(badge);
        return badge;
    }

    public void ShowAction(string action)
    {
        ButtonGlyph.Shape shape = InputPromptHelper.GetGlyph(action);
        _glyph.Current = shape;
        _glyph.Visible = shape != ButtonGlyph.Shape.None;
        _label.Text = shape == ButtonGlyph.Shape.None ? InputPromptHelper.GetPrompt(action) : "";
    }
}
